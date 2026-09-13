using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using BepInEx;
using UnityEngine;

namespace CalendarQuestsPins
{
    [BepInPlugin(PluginGuid, PluginName, PluginVersion)]
    public sealed class AutoInteractionAuditProbePlugin : BaseUnityPlugin
    {
        public const string PluginGuid = "nikich.gyk.calendarquestspins.autointeractionaudit";
        public const string PluginName = "Day Wheel Quest Markers - auto interaction audit";
        public const string PluginVersion = "0.1.0";

        private static readonly string[] NpcIds =
        {
            "npc_astrologer", "npc_inquisitor", "npc_cultist",
            "npc_merchant", "npc_actress", "npc_bishop"
        };

        private Type _mainGameType;
        private Type _worldMapType;
        private Type _controllerType;
        private MethodInfo _worldObjectGetter;
        private bool _finished;
        private float _nextAttempt;
        private int _attempts;

        private sealed class Node
        {
            internal string Id;
            internal string Type;
            internal int TypePosition;
        }

        private sealed class Connection
        {
            internal string SourcePort;
            internal string TargetPort;
            internal string SourceNode;
            internal string TargetNode;
        }

        private sealed class Anchor
        {
            internal string EventIdentifier;
            internal string MultiNodeId;
            internal int AnswerIndex = -1;
        }

        private sealed class ReverseNode
        {
            internal string Id;
            internal int Depth;
        }

        private void Awake()
        {
            _mainGameType = ReflectionUtil.FindType("MainGame");
            _worldMapType = ReflectionUtil.FindType("WorldMap");
            _controllerType = ReflectionUtil.FindType("FlowCanvas.FlowScriptController");
            _worldObjectGetter = FindWorldObjectGetter();
            _nextAttempt = Time.realtimeSinceStartup + 1f;
            Logger.LogInfo(PluginName + " " + PluginVersion + " loaded. Read-only one-shot structural audit; no save/UI mutations.");
        }

        private void Update()
        {
            if (_finished || Time.realtimeSinceStartup < _nextAttempt) return;
            _nextAttempt = Time.realtimeSinceStartup + 1f;
            _attempts++;

            if (!RuntimeReady())
            {
                if (_attempts == 120)
                {
                    Logger.LogError("AUTO_AUDIT_ABORT runtime did not become ready after 120 attempts.");
                    _finished = true;
                }
                return;
            }

            try
            {
                RunAudit();
            }
            catch (Exception ex)
            {
                Logger.LogError("AUTO_AUDIT_FATAL " + ex);
            }
            finally
            {
                _finished = true;
                enabled = false;
            }
        }

        private bool RuntimeReady()
        {
            if (_mainGameType == null || _worldMapType == null || _controllerType == null || _worldObjectGetter == null)
                return false;

            object started;
            if (!TryReadStatic(_mainGameType, "game_started", out started) || !(started is bool) || !(bool)started)
                return false;

            for (var i = 0; i < NpcIds.Length; i++)
            {
                var wgo = GetWorldObject(NpcIds[i]);
                var component = wgo as Component;
                if (component == null) return false;
                var controller = component.GetComponent(_controllerType);
                object graph;
                object serializedValue;
                if (controller == null || !ReflectionUtil.TryRead(controller, "_graph", out graph) || graph == null)
                    return false;
                if (!ReflectionUtil.TryRead(graph, "_serializedGraph", out serializedValue) || string.IsNullOrEmpty(serializedValue as string))
                    return false;
            }
            return true;
        }

        private void RunAudit()
        {
            Logger.LogInfo("AUTO_AUDIT_BEGIN game=1.407 npcs=6 policy=owner-local Complete routes without resolvable selectable-answer anchor");
            var totalOwnerComplete = 0;
            var totalMapped = 0;
            var totalCandidate = 0;

            for (var i = 0; i < NpcIds.Length; i++)
            {
                int ownerComplete;
                int mapped;
                int candidate;
                AuditNpc(NpcIds[i], out ownerComplete, out mapped, out candidate);
                totalOwnerComplete += ownerComplete;
                totalMapped += mapped;
                totalCandidate += candidate;
            }

            Logger.LogInfo("AUTO_AUDIT_SUMMARY ownerComplete=" + totalOwnerComplete +
                           " mappedSelectable=" + totalMapped +
                           " candidateNonSelectable=" + totalCandidate);
            Logger.LogInfo("AUTO_AUDIT_END probe disabled after this snapshot");
        }

        private void AuditNpc(string npcId, out int ownerComplete, out int mappedSelectable, out int candidates)
        {
            ownerComplete = 0;
            mappedSelectable = 0;
            candidates = 0;

            var wgo = GetWorldObject(npcId);
            var component = wgo as Component;
            if (component == null)
            {
                Logger.LogWarning("AUTO_AUDIT_NPC npc=" + npcId + " status=NO_WGO");
                return;
            }

            var controller = component.GetComponent(_controllerType);
            object graph;
            object serializedValue;
            if (controller == null || !ReflectionUtil.TryRead(controller, "_graph", out graph) || graph == null ||
                !ReflectionUtil.TryRead(graph, "_serializedGraph", out serializedValue))
            {
                Logger.LogWarning("AUTO_AUDIT_NPC npc=" + npcId + " status=NO_GRAPH");
                return;
            }

            var serialized = serializedValue as string;
            if (string.IsNullOrEmpty(serialized))
            {
                Logger.LogWarning("AUTO_AUDIT_NPC npc=" + npcId + " status=EMPTY_GRAPH");
                return;
            }

            var nodes = BuildNodeIndex(serialized);
            var connections = ParseConnections(serialized);
            var incomingFlow = BuildConnectionMap(connections, true, true);
            var incomingValue = BuildConnectionMap(connections, true, false);

            foreach (var node in nodes.Values)
            {
                if (!node.Type.EndsWith("Flow_SetTaskState", StringComparison.Ordinal)) continue;
                if (!string.Equals(ReadNodeContent(serialized, node, "State"), "Complete", StringComparison.Ordinal)) continue;

                var taskId = ReadNodeContent(serialized, node, "Task");
                if (string.IsNullOrEmpty(taskId)) continue;
                var ownerNpcId = ReadNodeContent(serialized, node, "NPC id");
                if (!string.IsNullOrEmpty(ownerNpcId) && !string.Equals(ownerNpcId, npcId, StringComparison.Ordinal)) continue;

                ownerComplete++;
                var anchors = FindAnchors(nodes, incomingFlow, serialized, node.Id, 128);
                if (HasResolvableSelectableAnchor(anchors, serialized, nodes))
                {
                    mappedSelectable++;
                    continue;
                }

                candidates++;
                AuditCandidate(npcId, taskId, node, anchors, serialized, nodes, incomingFlow, incomingValue);
            }

            Logger.LogInfo("AUTO_AUDIT_NPC npc=" + npcId +
                           " nodes=" + nodes.Count +
                           " connections=" + connections.Count +
                           " ownerComplete=" + ownerComplete +
                           " mappedSelectable=" + mappedSelectable +
                           " candidateNonSelectable=" + candidates);
        }

        private void AuditCandidate(string npcId, string taskId, Node completeNode, List<Anchor> anchors,
            string serialized, Dictionary<string, Node> nodes,
            Dictionary<string, List<Connection>> incomingFlow,
            Dictionary<string, List<Connection>> incomingValue)
        {
            var closure = BuildReverseClosure(completeNode.Id, incomingFlow, 128, 256);
            var roots = new List<ReverseNode>();
            var hasMenuInClosure = false;
            var hasCustomFunctionRoot = false;
            var hasInteractionLiteralRoot = false;
            var capped = closure.Count >= 256;

            for (var i = 0; i < closure.Count; i++)
            {
                var item = closure[i];
                Node n;
                if (!nodes.TryGetValue(item.Id, out n)) continue;
                if (n.Type.EndsWith("Flow_MultiAnswer", StringComparison.Ordinal)) hasMenuInClosure = true;

                List<Connection> incoming;
                if (!incomingFlow.TryGetValue(item.Id, out incoming) || incoming.Count == 0)
                {
                    roots.Add(item);
                    if (n.Type.EndsWith("CustomFunctionEvent", StringComparison.Ordinal)) hasCustomFunctionRoot = true;
                    if (NodeWindowContains(serialized, n, "interaction")) hasInteractionLiteralRoot = true;
                }
            }

            var classification = hasMenuInClosure ? "UNRESOLVED_MENU_ROUTE" :
                                 hasInteractionLiteralRoot ? "INTERACTION_ROOT_CANDIDATE" :
                                 hasCustomFunctionRoot ? "FUNCTION_ROOT_UNRESOLVED" :
                                 "ROOT_UNRESOLVED";

            Logger.LogInfo("AUTO_CANDIDATE npc=" + npcId +
                           " task=" + taskId +
                           " completeNode=" + completeNode.Id +
                           " class=" + classification +
                           " anchors=" + DescribeAnchors(anchors) +
                           " reverseNodes=" + closure.Count +
                           " roots=" + roots.Count +
                           " capped=" + capped);

            roots.Sort(delegate(ReverseNode a, ReverseNode b) { return a.Depth.CompareTo(b.Depth); });
            for (var i = 0; i < roots.Count; i++)
            {
                Node root;
                if (!nodes.TryGetValue(roots[i].Id, out root)) continue;
                Logger.LogInfo("AUTO_ROOT npc=" + npcId +
                               " task=" + taskId +
                               " node=" + root.Id +
                               " depth=" + roots[i].Depth +
                               " type=" + ShortType(root.Type) +
                               " interactionLiteral=" + NodeWindowContains(serialized, root, "interaction") +
                               " fields=" + DescribeNodeFields(serialized, root) +
                               " excerpt=" + CompactNodeWindow(serialized, root));
            }

            closure.Sort(delegate(ReverseNode a, ReverseNode b) { return b.Depth.CompareTo(a.Depth); });
            var emitted = 0;
            for (var i = 0; i < closure.Count && emitted < 80; i++)
            {
                Node routeNode;
                if (!nodes.TryGetValue(closure[i].Id, out routeNode)) continue;
                Logger.LogInfo("AUTO_PATH npc=" + npcId +
                               " task=" + taskId +
                               " depth=" + closure[i].Depth +
                               " node=" + routeNode.Id +
                               " type=" + ShortType(routeNode.Type) +
                               " fields=" + DescribeNodeFields(serialized, routeNode));
                EmitValueDependencies(npcId, taskId, routeNode.Id, serialized, nodes, incomingValue);
                emitted++;
            }
            if (closure.Count > emitted)
                Logger.LogWarning("AUTO_PATH_TRUNCATED npc=" + npcId + " task=" + taskId + " emitted=" + emitted + " total=" + closure.Count);
        }

        private void EmitValueDependencies(string npcId, string taskId, string routeNodeId, string serialized,
            Dictionary<string, Node> nodes, Dictionary<string, List<Connection>> incomingValue)
        {
            List<Connection> first;
            if (!incomingValue.TryGetValue(routeNodeId, out first) || first.Count == 0) return;

            var queue = new Queue<Tuple<string, int, string>>();
            var seen = new HashSet<string>(StringComparer.Ordinal);
            for (var i = 0; i < first.Count; i++)
                queue.Enqueue(Tuple.Create(first[i].SourceNode, 1, first[i].TargetPort));

            var emitted = 0;
            while (queue.Count > 0 && emitted < 32)
            {
                var current = queue.Dequeue();
                if (!seen.Add(current.Item1)) continue;
                Node source;
                if (!nodes.TryGetValue(current.Item1, out source)) continue;
                Logger.LogInfo("AUTO_VALUE npc=" + npcId +
                               " task=" + taskId +
                               " routeNode=" + routeNodeId +
                               " valueDepth=" + current.Item2 +
                               " targetPort=" + SafeToken(current.Item3) +
                               " sourceNode=" + source.Id +
                               " type=" + ShortType(source.Type) +
                               " fields=" + DescribeNodeFields(serialized, source));
                emitted++;
                if (current.Item2 >= 4) continue;
                List<Connection> upstream;
                if (!incomingValue.TryGetValue(source.Id, out upstream)) continue;
                for (var i = 0; i < upstream.Count; i++)
                    queue.Enqueue(Tuple.Create(upstream[i].SourceNode, current.Item2 + 1, upstream[i].TargetPort));
            }
        }

        private static List<ReverseNode> BuildReverseClosure(string startNodeId,
            Dictionary<string, List<Connection>> incomingFlow, int maxDepth, int maxNodes)
        {
            var result = new List<ReverseNode>();
            var queue = new Queue<ReverseNode>();
            var seen = new HashSet<string>(StringComparer.Ordinal);
            queue.Enqueue(new ReverseNode { Id = startNodeId, Depth = 0 });
            seen.Add(startNodeId);

            while (queue.Count > 0 && result.Count < maxNodes)
            {
                var current = queue.Dequeue();
                result.Add(current);
                if (current.Depth >= maxDepth) continue;
                List<Connection> incoming;
                if (!incomingFlow.TryGetValue(current.Id, out incoming)) continue;
                for (var i = 0; i < incoming.Count; i++)
                    if (seen.Add(incoming[i].SourceNode))
                        queue.Enqueue(new ReverseNode { Id = incoming[i].SourceNode, Depth = current.Depth + 1 });
            }
            return result;
        }

        private static bool HasResolvableSelectableAnchor(List<Anchor> anchors, string serialized, Dictionary<string, Node> nodes)
        {
            for (var i = 0; i < anchors.Count; i++)
            {
                var anchor = anchors[i];
                if (!string.IsNullOrEmpty(anchor.MultiNodeId) && anchor.AnswerIndex >= 0) return true;
                if (string.IsNullOrEmpty(anchor.EventIdentifier)) continue;
                var names = CandidateAnswerNames(anchor.EventIdentifier);
                foreach (var node in nodes.Values)
                {
                    if (!node.Type.EndsWith("Flow_MultiAnswer", StringComparison.Ordinal)) continue;
                    var answers = ReadMultiAnswers(serialized, node);
                    for (var j = 0; j < answers.Count; j++)
                        if (names.Contains(answers[j])) return true;
                }
            }
            return false;
        }

        private static string DescribeAnchors(List<Anchor> anchors)
        {
            if (anchors == null || anchors.Count == 0) return "<none>";
            var sb = new StringBuilder();
            for (var i = 0; i < anchors.Count; i++)
            {
                if (i > 0) sb.Append(';');
                if (!string.IsNullOrEmpty(anchors[i].MultiNodeId))
                    sb.Append("multi:").Append(anchors[i].MultiNodeId).Append('#').Append(anchors[i].AnswerIndex);
                else
                    sb.Append("event:").Append(SafeToken(anchors[i].EventIdentifier));
            }
            return sb.ToString();
        }

        private static string DescribeNodeFields(string serialized, Node node)
        {
            var keys = new[] { "identifier", "Text", "Task", "State", "NPC id", "Phrase", "Phrase ID", "Param name", "id", "res_type", "Event", "event" };
            var sb = new StringBuilder();
            for (var i = 0; i < keys.Length; i++)
            {
                string value;
                if (keys[i] == "identifier") value = ReadNodeIdentifier(serialized, node);
                else value = ReadNodeContent(serialized, node, keys[i]);
                if (string.IsNullOrEmpty(value)) continue;
                if (sb.Length > 0) sb.Append(',');
                sb.Append(keys[i].Replace(' ', '_')).Append('=').Append(SafeToken(value));
            }
            return sb.Length == 0 ? "<none>" : sb.ToString();
        }

        private static bool NodeWindowContains(string serialized, Node node, string needle)
        {
            if (node == null || string.IsNullOrEmpty(needle)) return false;
            var begin = Math.Max(0, node.TypePosition - 1400);
            var length = node.TypePosition - begin;
            if (length <= 0) return false;
            return serialized.IndexOf(needle, begin, length, StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static string CompactNodeWindow(string serialized, Node node)
        {
            if (node == null) return "<none>";
            var begin = Math.Max(0, node.TypePosition - 700);
            var raw = serialized.Substring(begin, node.TypePosition - begin);
            raw = raw.Replace('\r', ' ').Replace('\n', ' ').Replace('\t', ' ');
            while (raw.Contains("  ")) raw = raw.Replace("  ", " ");
            raw = raw.Replace("|", "/");
            if (raw.Length > 650) raw = raw.Substring(raw.Length - 650);
            return raw;
        }

        private static string ShortType(string type)
        {
            if (string.IsNullOrEmpty(type)) return "<unknown>";
            var comma = type.IndexOf(',');
            if (comma >= 0) type = type.Substring(0, comma);
            var plus = type.LastIndexOf('+');
            var dot = type.LastIndexOf('.');
            var cut = Math.Max(plus, dot);
            return cut >= 0 && cut + 1 < type.Length ? type.Substring(cut + 1) : type;
        }

        private static string SafeToken(string value)
        {
            if (string.IsNullOrEmpty(value)) return "<none>";
            return value.Replace("\r", " ").Replace("\n", " ").Replace("\t", " ").Replace("|", "/");
        }

        private static Dictionary<string, List<Connection>> BuildConnectionMap(List<Connection> connections, bool incoming, bool flow)
        {
            var result = new Dictionary<string, List<Connection>>(StringComparer.Ordinal);
            for (var i = 0; i < connections.Count; i++)
            {
                var c = connections[i];
                if (IsFlowConnection(c) != flow) continue;
                var key = incoming ? c.TargetNode : c.SourceNode;
                List<Connection> list;
                if (!result.TryGetValue(key, out list)) result[key] = list = new List<Connection>();
                list.Add(c);
            }
            return result;
        }

        private static List<Anchor> FindAnchors(Dictionary<string, Node> nodes,
            Dictionary<string, List<Connection>> incomingFlow, string serialized, string startNodeId, int maxDepth)
        {
            var result = new List<Anchor>();
            var queue = new Queue<Tuple<string, int>>();
            var seen = new HashSet<string>(StringComparer.Ordinal) { startNodeId };
            queue.Enqueue(Tuple.Create(startNodeId, 0));
            while (queue.Count > 0)
            {
                var current = queue.Dequeue();
                if (current.Item2 >= maxDepth) continue;
                List<Connection> incoming;
                if (!incomingFlow.TryGetValue(current.Item1, out incoming)) continue;
                for (var i = 0; i < incoming.Count; i++)
                {
                    Node source;
                    if (!nodes.TryGetValue(incoming[i].SourceNode, out source)) continue;
                    if (source.Type.EndsWith("CustomFunctionEvent", StringComparison.Ordinal))
                    {
                        AddAnchor(result, new Anchor { EventIdentifier = ReadNodeIdentifier(serialized, source) });
                        continue;
                    }
                    if (source.Type.EndsWith("Flow_MultiAnswer", StringComparison.Ordinal))
                    {
                        AddAnchor(result, new Anchor { MultiNodeId = source.Id, AnswerIndex = ParseOutPortIndex(incoming[i].SourcePort) });
                        continue;
                    }
                    if (seen.Add(source.Id)) queue.Enqueue(Tuple.Create(source.Id, current.Item2 + 1));
                }
            }
            return result;
        }

        private static void AddAnchor(List<Anchor> list, Anchor candidate)
        {
            for (var i = 0; i < list.Count; i++)
                if (list[i].EventIdentifier == candidate.EventIdentifier && list[i].MultiNodeId == candidate.MultiNodeId && list[i].AnswerIndex == candidate.AnswerIndex)
                    return;
            list.Add(candidate);
        }

        private static Dictionary<string, Node> BuildNodeIndex(string serialized)
        {
            var result = new Dictionary<string, Node>(StringComparer.Ordinal);
            const string typeMarker = "\"$type\":\"";
            const string idMarker = "\"$id\":\"";
            var start = 0;
            while (start < serialized.Length)
            {
                var typePos = serialized.IndexOf(typeMarker, start, StringComparison.Ordinal);
                if (typePos < 0) break;
                int typeEnd;
                var type = ReadJsonString(serialized, typePos + typeMarker.Length, out typeEnd);
                if (type == null) break;
                var nextType = serialized.IndexOf(typeMarker, typeEnd, StringComparison.Ordinal);
                var idPos = serialized.IndexOf(idMarker, typeEnd, StringComparison.Ordinal);
                if (idPos >= 0 && (nextType < 0 || idPos < nextType) && idPos - typeEnd < 240)
                {
                    int idEnd;
                    var id = ReadJsonString(serialized, idPos + idMarker.Length, out idEnd);
                    if (!string.IsNullOrEmpty(id)) result[id] = new Node { Id = id, Type = type, TypePosition = typePos };
                }
                start = typeEnd + 1;
            }
            return result;
        }

        private static List<Connection> ParseConnections(string serialized)
        {
            var result = new List<Connection>();
            const string spMarker = "\"_sourcePortName\":\"";
            const string tpMarker = "\"_targetPortName\":\"";
            const string srcMarker = "\"_sourceNode\":{\"$ref\":\"";
            const string dstMarker = "\"_targetNode\":{\"$ref\":\"";
            var start = 0;
            while (start < serialized.Length)
            {
                var spPos = serialized.IndexOf(spMarker, start, StringComparison.Ordinal);
                if (spPos < 0) break;
                int spEnd;
                var sp = ReadJsonString(serialized, spPos + spMarker.Length, out spEnd);
                var tpPos = serialized.IndexOf(tpMarker, spEnd, StringComparison.Ordinal);
                if (tpPos < 0 || tpPos - spEnd > 300) { start = spEnd + 1; continue; }
                int tpEnd;
                var tp = ReadJsonString(serialized, tpPos + tpMarker.Length, out tpEnd);
                var srcPos = serialized.IndexOf(srcMarker, tpEnd, StringComparison.Ordinal);
                if (srcPos < 0 || srcPos - tpEnd > 300) { start = tpEnd + 1; continue; }
                int srcEnd;
                var src = ReadJsonString(serialized, srcPos + srcMarker.Length, out srcEnd);
                var dstPos = serialized.IndexOf(dstMarker, srcEnd, StringComparison.Ordinal);
                if (dstPos < 0 || dstPos - srcEnd > 300) { start = srcEnd + 1; continue; }
                int dstEnd;
                var dst = ReadJsonString(serialized, dstPos + dstMarker.Length, out dstEnd);
                if (!string.IsNullOrEmpty(src) && !string.IsNullOrEmpty(dst))
                    result.Add(new Connection { SourcePort = sp, TargetPort = tp, SourceNode = src, TargetNode = dst });
                start = dstEnd + 1;
            }
            return result;
        }

        private static bool IsFlowConnection(Connection c)
        {
            return c != null && (string.Equals(c.TargetPort, "In", StringComparison.OrdinalIgnoreCase) || string.IsNullOrWhiteSpace(c.TargetPort));
        }

        private static string ReadNodeContent(string serialized, Node node, string key)
        {
            if (node == null) return null;
            var begin = Math.Max(0, node.TypePosition - 2600);
            var window = serialized.Substring(begin, node.TypePosition - begin);
            var marker = "\"" + key + "\":{\"$content\":\"";
            var pos = window.LastIndexOf(marker, StringComparison.Ordinal);
            if (pos < 0) return null;
            int end;
            return ReadJsonString(window, pos + marker.Length, out end);
        }

        private static string ReadNodeIdentifier(string serialized, Node node)
        {
            if (node == null) return null;
            var begin = Math.Max(0, node.TypePosition - 2600);
            var window = serialized.Substring(begin, node.TypePosition - begin);
            const string marker = "\"identifier\":\"";
            var pos = window.LastIndexOf(marker, StringComparison.Ordinal);
            if (pos < 0) return null;
            int end;
            return ReadJsonString(window, pos + marker.Length, out end);
        }

        private static List<string> ReadMultiAnswers(string serialized, Node node)
        {
            var result = new List<string>();
            var begin = Math.Max(0, node.TypePosition - 18000);
            var window = serialized.Substring(begin, node.TypePosition - begin);
            const string marker = "\"answers\":[";
            var pos = window.LastIndexOf(marker, StringComparison.Ordinal);
            if (pos < 0) return result;
            var p = pos + marker.Length;
            while (p < window.Length)
            {
                while (p < window.Length && (char.IsWhiteSpace(window[p]) || window[p] == ',')) p++;
                if (p >= window.Length || window[p] == ']') break;
                if (window[p] != '"') break;
                int end;
                var value = ReadJsonString(window, p + 1, out end);
                if (value == null) break;
                result.Add(value);
                p = end + 1;
            }
            return result;
        }

        private static int ParseOutPortIndex(string port)
        {
            const string prefix = "out_";
            int value;
            return port != null && port.StartsWith(prefix, StringComparison.Ordinal) && int.TryParse(port.Substring(prefix.Length), out value) ? value : -1;
        }

        private static HashSet<string> CandidateAnswerNames(string identifier)
        {
            var result = new HashSet<string>(StringComparer.Ordinal);
            if (string.IsNullOrEmpty(identifier)) return result;
            result.Add(identifier);
            if (identifier.StartsWith("@", StringComparison.Ordinal)) result.Add(identifier.Substring(1));
            else result.Add("@" + identifier);
            return result;
        }

        private static string ReadJsonString(string text, int start, out int end)
        {
            end = start;
            var escaped = false;
            for (var i = start; i < text.Length; i++)
            {
                var ch = text[i];
                if (escaped) { escaped = false; continue; }
                if (ch == '\\') { escaped = true; continue; }
                if (ch != '"') continue;
                end = i;
                var raw = text.Substring(start, i - start);
                try { return Regex.Unescape(raw.Replace("\\/", "/")); }
                catch { return raw; }
            }
            return null;
        }

        private object GetWorldObject(string npcId)
        {
            try { return _worldObjectGetter.Invoke(null, new object[] { npcId, true }); }
            catch { return null; }
        }

        private MethodInfo FindWorldObjectGetter()
        {
            if (_worldMapType == null) return null;
            foreach (var method in _worldMapType.GetMethods(ReflectionUtil.AnyStatic))
            {
                if (method.Name != "GetWorldGameObjectByObjId") continue;
                var p = method.GetParameters();
                if (p.Length == 2 && p[0].ParameterType == typeof(string) && p[1].ParameterType == typeof(bool)) return method;
            }
            return null;
        }

        private static bool TryReadStatic(Type type, string name, out object value)
        {
            value = null;
            if (type == null) return false;
            try
            {
                var field = type.GetField(name, ReflectionUtil.AnyStatic);
                if (field != null) { value = field.GetValue(null); return true; }
                var prop = type.GetProperty(name, ReflectionUtil.AnyStatic);
                if (prop != null && prop.GetIndexParameters().Length == 0)
                {
                    value = prop.GetValue(null, null);
                    return true;
                }
            }
            catch { }
            return false;
        }
    }
}

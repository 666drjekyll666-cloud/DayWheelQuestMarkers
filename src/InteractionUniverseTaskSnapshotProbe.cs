using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using BepInEx;
using UnityEngine;

namespace CalendarQuestsPins
{
    [BepInPlugin(PluginGuid, PluginName, PluginVersion)]
    public sealed class InteractionUniverseTaskSnapshotProbe : BaseUnityPlugin
    {
        public const string PluginGuid = "nikich.gyk.daywheel.interaction-universe-task-snapshot";
        public const string PluginName = "Day Wheel Quest Markers - interaction universe task snapshot";
        public const string PluginVersion = "0.1.2";

        private static readonly string[] NpcIds =
        {
            "npc_astrologer", "npc_inquisitor", "npc_cultist",
            "npc_merchant", "npc_actress", "npc_bishop"
        };

        private Type _mainGameType;
        private Type _worldMapType;
        private Type _controllerType;
        private MethodInfo _worldObjectGetter;
        private object _mainGame;
        private bool _finished;
        private float _nextAttempt;

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
            internal bool SyntheticFunctionLink;
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
            Logger.LogInfo(PluginName + " " + PluginVersion + " loaded. Read-only snapshot; no save/UI mutations. WaitForFlow, CustomFunction UID, and same-graph FireEvent links enabled.");
        }

        private void Update()
        {
            if (_finished || Time.realtimeSinceStartup < _nextAttempt) return;
            _nextAttempt = Time.realtimeSinceStartup + 1f;
            if (!RuntimeReady()) return;

            try { RunAudit(); }
            catch (Exception ex) { Logger.LogError("TASKSNAP_FATAL " + ex); }
            finally { _finished = true; enabled = false; }
        }

        private bool RuntimeReady()
        {
            if (_mainGameType == null || _worldMapType == null || _controllerType == null || _worldObjectGetter == null) return false;
            object started;
            if (!TryReadStatic(_mainGameType, "game_started", out started) || !(started is bool) || !(bool)started) return false;
            if (_mainGame == null || !ReflectionUtil.IsUnityAlive(_mainGame))
                _mainGame = ReflectionUtil.FindActiveUnityInstance(_mainGameType);
            if (_mainGame == null) return false;
            object save;
            if (!ReflectionUtil.TryRead(_mainGame, "save", out save) || save == null) return false;
            for (var i = 0; i < NpcIds.Length; i++)
                if (ReadSerializedGraph(NpcIds[i]) == null) return false;
            return true;
        }

        private void RunAudit()
        {
            Logger.LogInfo("TASKSNAP_BEGIN game=1.407 npcs=6 refinedFlow=WaitForFlow-numbered refinedFunctions=UID-jumps refinedEvents=FireEvent-to-CustomEvent emitAllMapped=True");
            var totalComplete = 0;
            var totalMapped = 0;
            var totalCandidates = 0;
            for (var i = 0; i < NpcIds.Length; i++)
            {
                int complete;
                int mapped;
                int candidates;
                AuditNpc(NpcIds[i], out complete, out mapped, out candidates);
                totalComplete += complete;
                totalMapped += mapped;
                totalCandidates += candidates;
            }
            Logger.LogInfo("TASKSNAP_SUMMARY ownerComplete=" + totalComplete + " mappedSelectable=" + totalMapped + " candidateNonSelectable=" + totalCandidates);
            DumpNavigationSnapshot();
            Logger.LogInfo("TASKSNAP_END probe disabled after this snapshot");
        }

        private void AuditNpc(string npcId, out int ownerComplete, out int mappedSelectable, out int candidates)
        {
            ownerComplete = 0;
            mappedSelectable = 0;
            candidates = 0;
            var serialized = ReadSerializedGraph(npcId);
            if (serialized == null)
            {
                Logger.LogWarning("TASKSNAP_NPC npc=" + npcId + " status=NO_GRAPH");
                return;
            }

            var nodes = BuildNodeIndex(serialized);
            var connections = ParseConnections(serialized);
            var incomingFlow = BuildIncomingFlow(nodes, connections);
            var functionLinks = AddFunctionLinks(serialized, nodes, incomingFlow);
            var eventLinks = AddEventLinks(serialized, nodes, incomingFlow);

            foreach (var node in nodes.Values)
            {
                if (!node.Type.EndsWith("Flow_SetTaskState", StringComparison.Ordinal)) continue;
                if (!string.Equals(ReadNodeContent(serialized, node, "State"), "Complete", StringComparison.Ordinal)) continue;
                var taskId = ReadNodeContent(serialized, node, "Task");
                if (string.IsNullOrEmpty(taskId)) continue;
                var ownerNpcId = ReadNodeContent(serialized, node, "NPC id");
                if (!string.IsNullOrEmpty(ownerNpcId) && !string.Equals(ownerNpcId, npcId, StringComparison.Ordinal)) continue;

                ownerComplete++;
                string answer;
                string trace;
                if (TryResolveSelectable(node.Id, serialized, nodes, incomingFlow, out answer, out trace))
                {
                    mappedSelectable++;
                    Logger.LogInfo("TASKSNAP_ROUTE npc=" + npcId + " task=" + taskId + " kind=SELECTABLE answer=" + Safe(answer) + " completeNode=" + node.Id + " trace=" + Safe(trace));
                    continue;
                }

                candidates++;
                EmitCandidate(npcId, taskId, node.Id, serialized, nodes, incomingFlow, functionLinks);
                Logger.LogInfo("TASKSNAP_ROUTE npc=" + npcId + " task=" + taskId + " kind=EVENT_OR_UNRESOLVED answer=<none> completeNode=" + node.Id + " trace=<none>");
            }

            Logger.LogInfo("TASKSNAP_NPC npc=" + npcId + " ownerComplete=" + ownerComplete + " mappedSelectable=" + mappedSelectable + " candidateNonSelectable=" + candidates + " functionLinks=" + functionLinks + " eventLinks=" + eventLinks);
        }

        private void DumpNavigationSnapshot()
        {
            object save;
            if (_mainGame == null || !ReflectionUtil.TryRead(_mainGame, "save", out save) || save == null)
            {
                Logger.LogError("NAVSNAP_ABORT save unavailable.");
                return;
            }

            var rules = new WeekdayInteractionRuleCache();
            if (!rules.Build(save, _mainGame))
            {
                Logger.LogError("NAVSNAP_ABORT production structural rule cache could not build.");
                return;
            }

            var targets = new Dictionary<string, WeekdayInteractionRuleCache.TargetRules>(StringComparer.Ordinal);
            foreach (var target in rules.AllTargets)
                if (target != null && !string.IsNullOrEmpty(target.NpcId)) targets[target.NpcId] = target;

            var navigation = new NavigationReachabilityCache(rules);
            Logger.LogInfo("NAVSNAP_BEGIN game=1.407 npcs=6 source=production-navigation-derivation snapshotOnly=True");
            for (var n = 0; n < NpcIds.Length; n++)
            {
                var npcId = NpcIds[n];
                WeekdayInteractionRuleCache.TargetRules target;
                var serialized = ReadSerializedGraph(npcId);
                if (serialized == null || !targets.TryGetValue(npcId, out target))
                {
                    Logger.LogError("NAVSNAP_ABORT npc=" + npcId + " graphOrTargetUnavailable=True");
                    return;
                }

                string failure;
                if (!navigation.BuildTarget(npcId, serialized, target.WorldObject, target, out failure))
                {
                    Logger.LogError("NAVSNAP_ABORT npc=" + npcId + " failure=" + Safe(failure));
                    return;
                }

                var answerIds = new HashSet<string>(StringComparer.Ordinal);
                var nodes = BuildNodeIndex(serialized);
                foreach (var node in nodes.Values)
                {
                    if (!node.Type.EndsWith("Flow_MultiAnswer", StringComparison.Ordinal)) continue;
                    var answers = ReadMultiAnswers(serialized, node);
                    for (var i = 0; i < answers.Count; i++)
                        if (!string.IsNullOrEmpty(answers[i])) answerIds.Add(answers[i]);
                }

                var sorted = new List<string>(answerIds);
                sorted.Sort(StringComparer.Ordinal);
                var emitted = 0;
                for (var a = 0; a < sorted.Count; a++)
                {
                    var paths = navigation.GetPathsForCompilation(npcId, sorted[a]);
                    if (paths == null || paths.Count == 0) continue;
                    for (var p = 0; p < paths.Count; p++)
                    {
                        Logger.LogInfo("NAVSNAP_PATH npc=" + npcId +
                                       " answer=" + Safe(sorted[a]) +
                                       " pathIndex=" + p +
                                       " unsupported=" + paths[p].Unsupported +
                                       " ancestors=" + FormatAncestors(paths[p].Ancestors));
                        emitted++;
                    }
                }
                Logger.LogInfo("NAVSNAP_NPC npc=" + npcId + " emittedPaths=" + emitted);
            }

            string verifiedFailure;
            var contractsOk = navigation.ValidateVerifiedContracts(out verifiedFailure);
            Logger.LogInfo("NAVSNAP_SUMMARY answers=" + navigation.AnswerCount +
                           " paths=" + navigation.PathCount +
                           " predicates=" + navigation.PredicateCount +
                           " unsupportedPaths=" + navigation.UnsupportedPathCount +
                           " verifiedContracts=" + contractsOk +
                           " verifiedFailure=" + Safe(verifiedFailure));
        }

        private static string FormatAncestors(List<NavigationReachabilityCache.EntryPredicate> ancestors)
        {
            if (ancestors == null || ancestors.Count == 0) return "<none>";
            var sb = new StringBuilder();
            for (var i = 0; i < ancestors.Count; i++)
            {
                if (i > 0) sb.Append('>');
                var p = ancestors[i];
                sb.Append(Safe(p.AnswerId))
                  .Append("{unlock=").Append(p.RequireUnlocked)
                  .Append(",notBlacklisted=").Append(p.RequireNotBlacklisted)
                  .Append(",unsupported=").Append(p.Unsupported)
                  .Append(",gates=");
                if (p.GateVariants == null || p.GateVariants.Count == 0) sb.Append("<none>");
                else
                {
                    for (var g = 0; g < p.GateVariants.Count; g++)
                    {
                        if (g > 0) sb.Append(';');
                        var v = p.GateVariants[g];
                        sb.Append(v.Unsupported ? "U" : "S")
                          .Append(":P=").Append(FormatRequirement(v.Price))
                          .Append(":L=").Append(FormatRequirement(v.Lock));
                    }
                }
                sb.Append('}');
            }
            return sb.ToString();
        }

        private static string FormatRequirement(WeekdayInteractionRuleCache.Requirement requirement)
        {
            if (requirement == null) return "<none>";
            return Safe(requirement.ResType) + ":" + Safe(requirement.Id) + "=" +
                   requirement.Value.ToString("R", System.Globalization.CultureInfo.InvariantCulture);
        }

        private bool TryResolveSelectable(string startNodeId, string serialized, Dictionary<string, Node> nodes,
            Dictionary<string, List<Connection>> incomingFlow, out string answer, out string trace)
        {
            answer = null;
            trace = null;
            var queue = new Queue<ReverseNode>();
            var seen = new HashSet<string>(StringComparer.Ordinal);
            var parent = new Dictionary<string, string>(StringComparer.Ordinal);
            queue.Enqueue(new ReverseNode { Id = startNodeId, Depth = 0 });
            seen.Add(startNodeId);

            while (queue.Count > 0 && seen.Count <= 512)
            {
                var current = queue.Dequeue();
                if (current.Depth >= 160) continue;
                Node currentNode;
                if (!nodes.TryGetValue(current.Id, out currentNode)) continue;

                List<Connection> incoming;
                if (!incomingFlow.TryGetValue(current.Id, out incoming)) continue;
                for (var i = 0; i < incoming.Count; i++)
                {
                    Node source;
                    if (!nodes.TryGetValue(incoming[i].SourceNode, out source)) continue;
                    if (source.Type.EndsWith("Flow_MultiAnswer", StringComparison.Ordinal))
                    {
                        var index = ParseOutPortIndex(incoming[i].SourcePort);
                        var answers = ReadMultiAnswers(serialized, source);
                        if (index >= 0 && index < answers.Count)
                        {
                            answer = answers[index];
                            trace = BuildTrace(current.Id, source.Id, parent) + (incoming[i].SyntheticFunctionLink ? ">uid" : "");
                            return true;
                        }
                    }

                    if (!seen.Add(source.Id)) continue;
                    parent[source.Id] = current.Id;
                    queue.Enqueue(new ReverseNode { Id = source.Id, Depth = current.Depth + 1 });
                }
            }
            return false;
        }

        private void EmitCandidate(string npcId, string taskId, string completeNodeId, string serialized,
            Dictionary<string, Node> nodes, Dictionary<string, List<Connection>> incomingFlow, int functionLinks)
        {
            var closure = BuildReverseClosure(completeNodeId, incomingFlow, 160, 512);
            var roots = new List<ReverseNode>();
            for (var i = 0; i < closure.Count; i++)
            {
                List<Connection> incoming;
                if (!incomingFlow.TryGetValue(closure[i].Id, out incoming) || incoming.Count == 0) roots.Add(closure[i]);
            }

            Logger.LogInfo("TASKSNAP_CANDIDATE npc=" + npcId + " task=" + taskId + " completeNode=" + completeNodeId + " reverseNodes=" + closure.Count + " roots=" + roots.Count + " functionLinks=" + functionLinks);
            roots.Sort(delegate(ReverseNode a, ReverseNode b) { return b.Depth.CompareTo(a.Depth); });
            for (var i = 0; i < roots.Count; i++)
            {
                Node root;
                if (!nodes.TryGetValue(roots[i].Id, out root)) continue;
                Logger.LogInfo("TASKSNAP_ROOT npc=" + npcId + " task=" + taskId + " node=" + root.Id + " depth=" + roots[i].Depth + " type=" + ShortType(root.Type) + " fields=" + DescribeFields(serialized, root));
            }

            closure.Sort(delegate(ReverseNode a, ReverseNode b) { return b.Depth.CompareTo(a.Depth); });
            for (var i = 0; i < closure.Count && i < 96; i++)
            {
                Node n;
                if (!nodes.TryGetValue(closure[i].Id, out n)) continue;
                if (n.Type.IndexOf("SmartRes", StringComparison.Ordinal) >= 0 ||
                    n.Type.IndexOf("Flow_Answer", StringComparison.Ordinal) >= 0 ||
                    n.Type.IndexOf("CustomFunction", StringComparison.Ordinal) >= 0 ||
                    n.Type.IndexOf("CustomEvent", StringComparison.Ordinal) >= 0 ||
                    n.Type.IndexOf("Flow_WaitForFlow", StringComparison.Ordinal) >= 0 ||
                    n.Type.IndexOf("Flow_AddPhraseToBlacklist", StringComparison.Ordinal) >= 0 ||
                    n.Type.IndexOf("Flow_RemoveItem", StringComparison.Ordinal) >= 0 ||
                    n.Type.IndexOf("Flow_Check", StringComparison.Ordinal) >= 0)
                {
                    Logger.LogInfo("TASKSNAP_SIGNAL npc=" + npcId + " task=" + taskId + " depth=" + closure[i].Depth + " node=" + n.Id + " type=" + ShortType(n.Type) + " fields=" + DescribeFields(serialized, n));
                }
            }
        }

        private static int AddFunctionLinks(string serialized, Dictionary<string, Node> nodes,
            Dictionary<string, List<Connection>> incomingFlow)
        {
            var eventsByUid = new Dictionary<string, string>(StringComparer.Ordinal);
            var calls = new List<Tuple<string, string>>();
            foreach (var node in nodes.Values)
            {
                if (node.Type.EndsWith("CustomFunctionEvent", StringComparison.Ordinal))
                {
                    var uid = ReadRawNodeString(serialized, node, "_UID");
                    if (!string.IsNullOrEmpty(uid)) eventsByUid[uid] = node.Id;
                }
                else if (node.Type.EndsWith("CustomFunctionCall", StringComparison.Ordinal))
                {
                    var uid = ReadRawNodeString(serialized, node, "_sourceOutputUID");
                    if (!string.IsNullOrEmpty(uid)) calls.Add(Tuple.Create(node.Id, uid));
                }
            }

            var added = 0;
            for (var i = 0; i < calls.Count; i++)
            {
                string eventNode;
                if (!eventsByUid.TryGetValue(calls[i].Item2, out eventNode)) continue;
                List<Connection> list;
                if (!incomingFlow.TryGetValue(eventNode, out list)) incomingFlow[eventNode] = list = new List<Connection>();
                list.Add(new Connection { SourceNode = calls[i].Item1, TargetNode = eventNode, SourcePort = "uid", TargetPort = "uid", SyntheticFunctionLink = true });
                added++;
            }
            return added;
        }

        private static int AddEventLinks(string serialized, Dictionary<string, Node> nodes,
            Dictionary<string, List<Connection>> incomingFlow)
        {
            var eventsByName = new Dictionary<string, List<string>>(StringComparer.Ordinal);
            var fires = new List<Tuple<string, string>>();
            foreach (var node in nodes.Values)
            {
                if (node.Type.EndsWith("CustomEvent", StringComparison.Ordinal) &&
                    !node.Type.EndsWith("CustomFunctionEvent", StringComparison.Ordinal))
                {
                    var eventName = ReadNestedRawNodeString(serialized, node, "eventName", "_value");
                    if (string.IsNullOrEmpty(eventName)) continue;
                    List<string> ids;
                    if (!eventsByName.TryGetValue(eventName, out ids)) eventsByName[eventName] = ids = new List<string>();
                    ids.Add(node.Id);
                }
                else if (node.Type.EndsWith("Flow_FireEvent", StringComparison.Ordinal))
                {
                    var eventName = ReadNodeContent(serialized, node, "event") ?? ReadNodeContent(serialized, node, "Event");
                    if (!string.IsNullOrEmpty(eventName)) fires.Add(Tuple.Create(node.Id, eventName));
                }
            }

            var added = 0;
            for (var i = 0; i < fires.Count; i++)
            {
                List<string> targets;
                if (!eventsByName.TryGetValue(fires[i].Item2, out targets)) continue;
                for (var j = 0; j < targets.Count; j++)
                {
                    List<Connection> list;
                    if (!incomingFlow.TryGetValue(targets[j], out list)) incomingFlow[targets[j]] = list = new List<Connection>();
                    list.Add(new Connection
                    {
                        SourceNode = fires[i].Item1,
                        TargetNode = targets[j],
                        SourcePort = "event",
                        TargetPort = "event"
                    });
                    added++;
                }
            }
            return added;
        }

        private static string ReadNestedRawNodeString(string serialized, Node node, string outerKey, string innerKey)
        {
            if (node == null) return null;
            var begin = Math.Max(0, node.TypePosition - 3500);
            var window = serialized.Substring(begin, node.TypePosition - begin);
            var marker = "\"" + outerKey + "\":{\"" + innerKey + "\":\"";
            var pos = window.LastIndexOf(marker, StringComparison.Ordinal);
            if (pos < 0) return null;
            int end;
            return ReadJsonString(window, pos + marker.Length, out end);
        }

        private static Dictionary<string, List<Connection>> BuildIncomingFlow(Dictionary<string, Node> nodes, List<Connection> connections)
        {
            var result = new Dictionary<string, List<Connection>>(StringComparer.Ordinal);
            for (var i = 0; i < connections.Count; i++)
            {
                var c = connections[i];
                Node target;
                if (!nodes.TryGetValue(c.TargetNode, out target)) continue;
                var isFlow = string.Equals(c.TargetPort, "In", StringComparison.OrdinalIgnoreCase) || string.IsNullOrWhiteSpace(c.TargetPort);
                if (!isFlow && target.Type.EndsWith("Flow_WaitForFlow", StringComparison.Ordinal) && IsIntegerPort(c.TargetPort)) isFlow = true;
                if (!isFlow) continue;
                List<Connection> list;
                if (!result.TryGetValue(c.TargetNode, out list)) result[c.TargetNode] = list = new List<Connection>();
                list.Add(c);
            }
            return result;
        }

        private static bool IsIntegerPort(string value)
        {
            if (string.IsNullOrEmpty(value)) return false;
            for (var i = 0; i < value.Length; i++) if (!char.IsDigit(value[i])) return false;
            return true;
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

        private string ReadSerializedGraph(string npcId)
        {
            try
            {
                var wgo = _worldObjectGetter.Invoke(null, new object[] { npcId, true });
                var component = wgo as Component;
                if (component == null) return null;
                var controller = component.GetComponent(_controllerType);
                object graph;
                object serialized;
                if (controller == null || !ReflectionUtil.TryRead(controller, "_graph", out graph) || graph == null) return null;
                if (!ReflectionUtil.TryRead(graph, "_serializedGraph", out serialized)) return null;
                return serialized as string;
            }
            catch { return null; }
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
                if (!string.IsNullOrEmpty(src) && !string.IsNullOrEmpty(dst)) result.Add(new Connection { SourcePort = sp, TargetPort = tp, SourceNode = src, TargetNode = dst });
                start = dstEnd + 1;
            }
            return result;
        }

        private static string ReadNodeContent(string serialized, Node node, string key)
        {
            if (node == null) return null;
            var begin = Math.Max(0, node.TypePosition - 3000);
            var window = serialized.Substring(begin, node.TypePosition - begin);
            var marker = "\"" + key + "\":{\"$content\":\"";
            var pos = window.LastIndexOf(marker, StringComparison.Ordinal);
            if (pos < 0) return null;
            int end;
            return ReadJsonString(window, pos + marker.Length, out end);
        }

        private static string ReadRawNodeString(string serialized, Node node, string key)
        {
            if (node == null) return null;
            var begin = Math.Max(0, node.TypePosition - 3500);
            var window = serialized.Substring(begin, node.TypePosition - begin);
            var marker = "\"" + key + "\":\"";
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

        private static string BuildTrace(string current, string root, Dictionary<string, string> parent)
        {
            var sb = new StringBuilder(root);
            var cursor = root;
            var guard = 0;
            while (parent.ContainsKey(cursor) && guard++ < 24)
            {
                cursor = parent[cursor];
                sb.Append('>').Append(cursor);
                if (cursor == current) break;
            }
            return sb.ToString();
        }

        private static string DescribeFields(string serialized, Node node)
        {
            var keys = new[] { "identifier", "Text", "Task", "State", "NPC id", "Phrase", "Phrase ID", "Param name", "id", "res_type", "Event", "event" };
            var sb = new StringBuilder();
            var identifier = ReadRawNodeString(serialized, node, "identifier");
            if (!string.IsNullOrEmpty(identifier)) sb.Append("identifier=").Append(Safe(identifier));
            for (var i = 1; i < keys.Length; i++)
            {
                var value = ReadNodeContent(serialized, node, keys[i]);
                if (string.IsNullOrEmpty(value)) continue;
                if (sb.Length > 0) sb.Append(',');
                sb.Append(keys[i].Replace(' ', '_')).Append('=').Append(Safe(value));
            }
            var uid = ReadRawNodeString(serialized, node, "_UID");
            var sourceUid = ReadRawNodeString(serialized, node, "_sourceOutputUID");
            if (!string.IsNullOrEmpty(uid)) { if (sb.Length > 0) sb.Append(','); sb.Append("uid=").Append(Safe(uid)); }
            if (!string.IsNullOrEmpty(sourceUid)) { if (sb.Length > 0) sb.Append(','); sb.Append("sourceUid=").Append(Safe(sourceUid)); }
            return sb.Length == 0 ? "<none>" : sb.ToString();
        }

        private static string ShortType(string type)
        {
            if (string.IsNullOrEmpty(type)) return "<unknown>";
            var comma = type.IndexOf(',');
            if (comma >= 0) type = type.Substring(0, comma);
            var dot = type.LastIndexOf('.');
            return dot >= 0 && dot + 1 < type.Length ? type.Substring(dot + 1) : type;
        }

        private static string Safe(string value)
        {
            return string.IsNullOrEmpty(value) ? "<none>" : value.Replace("\r", " ").Replace("\n", " ").Replace("\t", " ").Replace("|", "/");
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
                if (prop != null && prop.GetIndexParameters().Length == 0) { value = prop.GetValue(null, null); return true; }
            }
            catch { }
            return false;
        }
    }
}

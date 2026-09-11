using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Reflection;
using System.Text.RegularExpressions;
using UnityEngine;

namespace CalendarQuestsPins
{
    internal sealed class CrossOwnerRuleCache
    {
        internal sealed class PeriodicTargetRules
        {
            internal string NpcId;
            internal object WorldObject;
            internal readonly List<CrossTaskRules> Tasks = new List<CrossTaskRules>();
        }

        internal sealed class CrossTaskRules
        {
            internal string OwnerNpcId;
            internal string TaskId;
            internal object KnownNpc;
            internal readonly List<TaskRule> Rules = new List<TaskRule>();
        }

        internal sealed class TaskRule
        {
            internal string AnswerId;
            internal bool Unsupported;
            internal Requirement Price;
            internal Requirement Lock;
        }

        internal sealed class Requirement
        {
            internal string ResType;
            internal string Id;
            internal float Value;
            internal object SmartRes;
        }

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

        private readonly List<PeriodicTargetRules> _targets = new List<PeriodicTargetRules>(6);
        private readonly Type _worldMapType = ReflectionUtil.FindType("WorldMap");
        private readonly Type _controllerType = ReflectionUtil.FindType("FlowCanvas.FlowScriptController");
        private readonly Type _flowSmartResType = ReflectionUtil.FindType("FlowCanvas.Nodes.Flow_SmartRes");
        private MethodInfo _worldObjectGetter;
        private MethodInfo _smartResFactory;
        private MethodInfo _isEnough;
        private object _player;
        private object _save;
        private int _knownNpcCount;

        private static readonly string[] PeriodicNpcIds =
        {
            "npc_astrologer", "npc_inquisitor", "npc_cultist", "npc_merchant", "npc_actress", "npc_bishop"
        };

        internal int SupportedRuleCount { get; private set; }
        internal int UnsupportedRuleCount { get; private set; }
        internal int TrackedTaskCount { get; private set; }

        internal IEnumerable<PeriodicTargetRules> AllTargets { get { return _targets; } }

        internal bool Build(object save, object mainGame)
        {
            Clear();
            if (save == null || mainGame == null || _worldMapType == null || _controllerType == null || _flowSmartResType == null) return false;

            _save = save;
            _worldObjectGetter = FindWorldObjectGetter();
            _smartResFactory = FindSmartResFactory();
            object player;
            if (!ReflectionUtil.TryRead(mainGame, "player", out player) || player == null) return false;
            _player = player;

            var smartResType = ReflectionUtil.FindType("SmartRes");
            if (smartResType == null) return false;
            foreach (var method in player.GetType().GetMethods(ReflectionUtil.AnyInstance))
            {
                var ps = method.GetParameters();
                if (method.Name == "IsEnough" && ps.Length == 1 && ps[0].ParameterType.IsAssignableFrom(smartResType))
                {
                    _isEnough = method;
                    break;
                }
            }
            if (_worldObjectGetter == null || _smartResFactory == null || _isEnough == null) return false;

            var knownNpcMap = ReadKnownNpcs(save, out _knownNpcCount);
            for (var i = 0; i < PeriodicNpcIds.Length; i++)
            {
                var npcId = PeriodicNpcIds[i];
                var wgo = GetWorldObject(npcId);
                var component = wgo as Component;
                if (component == null) continue;

                var controller = component.GetComponent(_controllerType);
                object graph;
                if (controller == null || !ReflectionUtil.TryRead(controller, "_graph", out graph) || graph == null) continue;
                object serializedValue;
                if (!ReflectionUtil.TryRead(graph, "_serializedGraph", out serializedValue)) continue;
                var serialized = serializedValue as string;
                if (string.IsNullOrEmpty(serialized)) continue;

                var target = new PeriodicTargetRules { NpcId = npcId, WorldObject = wgo };
                ParseGraph(target, serialized, knownNpcMap);
                _targets.Add(target);
            }
            return _targets.Count > 0;
        }

        internal bool NeedsRebuild()
        {
            if (!ReflectionUtil.IsUnityAlive(_player)) return true;
            foreach (var target in _targets)
                if (!ReflectionUtil.IsUnityAlive(target.WorldObject)) return true;
            int currentCount;
            ReadKnownNpcs(_save, out currentCount);
            return currentCount != _knownNpcCount;
        }

        internal bool IsVisible(CrossTaskRules task)
        {
            if (task == null || task.KnownNpc == null || string.IsNullOrEmpty(task.TaskId)) return false;
            var tasks = ReflectionUtil.EnumerateMember(task.KnownNpc, "tasks");
            if (tasks == null) return false;
            foreach (var savedTask in tasks)
            {
                if (!string.Equals(ReflectionUtil.ReadString(savedTask, "id"), task.TaskId, StringComparison.Ordinal)) continue;
                object state;
                if (!ReflectionUtil.TryRead(savedTask, "state", out state) || state == null) return false;
                try { return Convert.ToInt32(state) == 0; }
                catch { return false; }
            }
            return false;
        }

        internal bool IsActionable(CrossTaskRules task, object unlockedPhrases, object blacklistedPhrases)
        {
            if (task == null) return false;
            foreach (var rule in task.Rules)
            {
                if (rule == null || rule.Unsupported || string.IsNullOrEmpty(rule.AnswerId)) continue;
                if (!PhraseOpen(rule.AnswerId, unlockedPhrases, blacklistedPhrases)) continue;
                if (rule.Price != null && !IsEnough(rule.Price)) continue;
                if (rule.Lock != null && !IsEnough(rule.Lock)) continue;
                return true;
            }
            return false;
        }

        internal void Clear()
        {
            _targets.Clear();
            SupportedRuleCount = 0;
            UnsupportedRuleCount = 0;
            TrackedTaskCount = 0;
            _player = null;
            _save = null;
            _isEnough = null;
            _knownNpcCount = 0;
        }

        private void ParseGraph(PeriodicTargetRules target, string serialized, Dictionary<string, object> knownNpcMap)
        {
            var nodes = BuildNodeIndex(serialized);
            var connections = ParseConnections(serialized);
            var incomingFlow = new Dictionary<string, List<Connection>>(StringComparer.Ordinal);
            var incomingValue = new Dictionary<string, List<Connection>>(StringComparer.Ordinal);
            foreach (var c in connections)
            {
                var map = IsFlowConnection(c) ? incomingFlow : incomingValue;
                List<Connection> list;
                if (!map.TryGetValue(c.TargetNode, out list)) map[c.TargetNode] = list = new List<Connection>();
                list.Add(c);
            }

            var byTask = new Dictionary<string, CrossTaskRules>(StringComparer.Ordinal);
            foreach (var node in nodes.Values)
            {
                if (!node.Type.EndsWith("Flow_SetTaskState", StringComparison.Ordinal)) continue;
                if (!string.Equals(ReadNodeContent(serialized, node, "State"), "Complete", StringComparison.Ordinal)) continue;

                var ownerNpcId = ReadNodeContent(serialized, node, "NPC id");
                var taskId = ReadNodeContent(serialized, node, "Task");
                if (string.IsNullOrEmpty(ownerNpcId) || string.IsNullOrEmpty(taskId)) continue;
                if (string.Equals(ownerNpcId, target.NpcId, StringComparison.Ordinal)) continue;

                object knownNpc;
                if (!knownNpcMap.TryGetValue(ownerNpcId, out knownNpc)) continue;

                var key = ownerNpcId + "\n" + taskId;
                CrossTaskRules task;
                if (!byTask.TryGetValue(key, out task))
                {
                    task = new CrossTaskRules { OwnerNpcId = ownerNpcId, TaskId = taskId, KnownNpc = knownNpc };
                    byTask[key] = task;
                    target.Tasks.Add(task);
                    TrackedTaskCount++;
                }

                var anchors = FindAnchors(nodes, incomingFlow, serialized, node.Id, 64);
                foreach (var anchor in anchors)
                    AddRulesForAnchor(target, task, anchor, serialized, nodes, connections, incomingValue);
            }
        }

        private void AddRulesForAnchor(PeriodicTargetRules target, CrossTaskRules task, Anchor anchor, string serialized,
            Dictionary<string, Node> nodes, List<Connection> connections, Dictionary<string, List<Connection>> incomingValue)
        {
            if (!string.IsNullOrEmpty(anchor.MultiNodeId) && anchor.AnswerIndex >= 0)
            {
                AddRuleForAnswer(target, task, anchor.MultiNodeId, anchor.AnswerIndex, serialized, nodes, connections, incomingValue);
                return;
            }
            if (string.IsNullOrEmpty(anchor.EventIdentifier)) return;

            var names = CandidateAnswerNames(anchor.EventIdentifier);
            foreach (var multi in nodes.Values)
            {
                if (!multi.Type.EndsWith("Flow_MultiAnswer", StringComparison.Ordinal)) continue;
                var answers = ReadMultiAnswers(serialized, multi);
                for (var i = 0; i < answers.Count; i++)
                    if (names.Contains(answers[i]))
                        AddRuleForAnswer(target, task, multi.Id, i, serialized, nodes, connections, incomingValue);
            }
        }

        private void AddRuleForAnswer(PeriodicTargetRules target, CrossTaskRules task, string multiId, int index, string serialized,
            Dictionary<string, Node> nodes, List<Connection> connections, Dictionary<string, List<Connection>> incomingValue)
        {
            Node multi;
            if (!nodes.TryGetValue(multiId, out multi)) return;
            var answers = ReadMultiAnswers(serialized, multi);
            if (index < 0 || index >= answers.Count || string.IsNullOrEmpty(answers[index])) return;
            var answerId = answers[index];

            foreach (var existing in task.Rules)
                if (string.Equals(existing.AnswerId, answerId, StringComparison.Ordinal)) return;

            var answerConnections = new List<Connection>();
            foreach (var c in connections)
                if (c.TargetNode == multiId && ParseAnswerPortIndex(c.TargetPort) == index) answerConnections.Add(c);

            if (answerConnections.Count == 0)
            {
                task.Rules.Add(new TaskRule { AnswerId = answerId });
                SupportedRuleCount++;
                return;
            }

            var anySupported = false;
            foreach (var c in answerConnections)
            {
                Node answerNode;
                if (!nodes.TryGetValue(c.SourceNode, out answerNode)) continue;
                if (answerNode.Type.IndexOf("Flow_Answer", StringComparison.Ordinal) < 0) continue;

                var rule = new TaskRule { AnswerId = answerId };
                List<Connection> gates;
                if (incomingValue.TryGetValue(answerNode.Id, out gates))
                {
                    foreach (var gate in gates)
                    {
                        if (!string.Equals(gate.TargetPort, "price", StringComparison.OrdinalIgnoreCase) &&
                            !string.Equals(gate.TargetPort, "lock", StringComparison.OrdinalIgnoreCase)) continue;

                        Node source;
                        if (!nodes.TryGetValue(gate.SourceNode, out source) || source.Type.IndexOf("Flow_SmartRes", StringComparison.Ordinal) < 0)
                        {
                            rule.Unsupported = true;
                            break;
                        }

                        var req = ParseRequirement(serialized, source, target.WorldObject);
                        if (req == null)
                        {
                            rule.Unsupported = true;
                            break;
                        }
                        if (string.Equals(gate.TargetPort, "price", StringComparison.OrdinalIgnoreCase)) rule.Price = req;
                        else rule.Lock = req;
                    }
                }

                task.Rules.Add(rule);
                if (rule.Unsupported) UnsupportedRuleCount++;
                else
                {
                    SupportedRuleCount++;
                    anySupported = true;
                }
            }

            if (!anySupported)
            {
                task.Rules.Add(new TaskRule { AnswerId = answerId, Unsupported = true });
                UnsupportedRuleCount++;
            }
        }

        private Requirement ParseRequirement(string serialized, Node node, object linkedWgo)
        {
            var type = ReadNodeContentAny(serialized, node, "res_type", "Res type");
            var id = ReadNodeContentAny(serialized, node, "id", "Id");
            float value;
            if (string.IsNullOrEmpty(type) || string.IsNullOrEmpty(id) || !TryReadNodeNumber(serialized, node, out value)) return null;
            var req = new Requirement { ResType = type, Id = id, Value = value };
            req.SmartRes = CreateSmartRes(req, linkedWgo);
            return req.SmartRes == null ? null : req;
        }

        private object CreateSmartRes(Requirement req, object linkedWgo)
        {
            try
            {
                var target = _smartResFactory.IsStatic ? null : Activator.CreateInstance(_smartResFactory.DeclaringType);
                var parameters = _smartResFactory.GetParameters();
                var args = new object[parameters.Length];
                for (var i = 0; i < parameters.Length; i++)
                {
                    var t = parameters[i].ParameterType;
                    if (t.IsEnum) args[i] = Enum.Parse(t, req.ResType, false);
                    else if (t == typeof(string)) args[i] = req.Id;
                    else if (t == typeof(float)) args[i] = req.Value;
                    else if (t == typeof(double)) args[i] = (double)req.Value;
                    else if (t == typeof(int)) args[i] = (int)Math.Round(req.Value);
                    else return null;
                }

                var smartRes = _smartResFactory.Invoke(target, args);
                if (smartRes == null) return null;
                if (linkedWgo != null)
                {
                    var field = smartRes.GetType().GetField("_linked_wgo", ReflectionUtil.AnyInstance);
                    if (field != null && field.FieldType.IsInstanceOfType(linkedWgo)) field.SetValue(smartRes, linkedWgo);
                }
                return smartRes;
            }
            catch { return null; }
        }

        private bool IsEnough(Requirement req)
        {
            if (req == null || req.SmartRes == null || _player == null || _isEnough == null) return false;
            try
            {
                var result = _isEnough.Invoke(_player, new[] { req.SmartRes });
                return result is bool && (bool)result;
            }
            catch { return false; }
        }

        private object GetWorldObject(string npcId)
        {
            try { return _worldObjectGetter.Invoke(null, new object[] { npcId, true }); }
            catch { return null; }
        }

        private MethodInfo FindWorldObjectGetter()
        {
            foreach (var method in _worldMapType.GetMethods(ReflectionUtil.AnyStatic))
            {
                if (method.Name != "GetWorldGameObjectByObjId") continue;
                var p = method.GetParameters();
                if (p.Length == 2 && p[0].ParameterType == typeof(string) && p[1].ParameterType == typeof(bool)) return method;
            }
            return null;
        }

        private MethodInfo FindSmartResFactory()
        {
            foreach (var method in _flowSmartResType.GetMethods(ReflectionUtil.AnyInstance | ReflectionUtil.AnyStatic | BindingFlags.DeclaredOnly))
                if (method.Name == "Invoke" && method.GetParameters().Length == 3) return method;
            return null;
        }

        private static Dictionary<string, object> ReadKnownNpcs(object save, out int count)
        {
            count = 0;
            var result = new Dictionary<string, object>(StringComparer.Ordinal);
            if (save == null) return result;
            object known;
            if (!ReflectionUtil.TryRead(save, "known_npcs", out known) || known == null) return result;
            var npcs = ReflectionUtil.EnumerateMember(known, "npcs");
            if (npcs == null) return result;
            foreach (var npc in npcs)
            {
                if (npc == null) continue;
                count++;
                var id = ReflectionUtil.ReadString(npc, "npc_id");
                if (!string.IsNullOrEmpty(id)) result[id] = npc;
            }
            return result;
        }

        private static bool PhraseOpen(string answerId, object unlockedObj, object blacklistedObj)
        {
            if (ContainsString(blacklistedObj, answerId)) return false;
            return !answerId.StartsWith("@", StringComparison.Ordinal) || ContainsString(unlockedObj, answerId);
        }

        private static bool ContainsString(object collection, string value)
        {
            var enumerable = collection as IEnumerable;
            if (enumerable == null) return false;
            foreach (var item in enumerable)
                if (string.Equals(item as string, value, StringComparison.Ordinal)) return true;
            return false;
        }

        private static List<Anchor> FindAnchors(Dictionary<string, Node> nodes, Dictionary<string, List<Connection>> incomingFlow,
            string serialized, string startNodeId, int maxDepth)
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

                foreach (var c in incoming)
                {
                    Node source;
                    if (!nodes.TryGetValue(c.SourceNode, out source)) continue;
                    if (source.Type.EndsWith("CustomFunctionEvent", StringComparison.Ordinal))
                    {
                        AddAnchor(result, new Anchor { EventIdentifier = ReadNodeIdentifier(serialized, source) });
                        continue;
                    }
                    if (source.Type.EndsWith("Flow_MultiAnswer", StringComparison.Ordinal))
                    {
                        AddAnchor(result, new Anchor { MultiNodeId = source.Id, AnswerIndex = ParseOutPortIndex(c.SourcePort) });
                        continue;
                    }
                    if (seen.Add(source.Id)) queue.Enqueue(Tuple.Create(source.Id, current.Item2 + 1));
                }
            }
            return result;
        }

        private static void AddAnchor(List<Anchor> list, Anchor candidate)
        {
            foreach (var a in list)
                if (a.EventIdentifier == candidate.EventIdentifier && a.MultiNodeId == candidate.MultiNodeId && a.AnswerIndex == candidate.AnswerIndex) return;
            list.Add(candidate);
        }

        private static bool IsFlowConnection(Connection c)
        {
            return c != null && (string.Equals(c.TargetPort, "In", StringComparison.OrdinalIgnoreCase) || string.IsNullOrWhiteSpace(c.TargetPort));
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

        private static string ReadNodeContentAny(string serialized, Node node, string first, string second)
        {
            return ReadNodeContent(serialized, node, first) ?? ReadNodeContent(serialized, node, second);
        }

        private static bool TryReadNodeNumber(string serialized, Node node, out float value)
        {
            return TryReadNodeNumber(serialized, node, "v", out value) || TryReadNodeNumber(serialized, node, "V", out value);
        }

        private static bool TryReadNodeNumber(string serialized, Node node, string key, out float value)
        {
            value = 0f;
            if (node == null) return false;
            var begin = Math.Max(0, node.TypePosition - 2600);
            var window = serialized.Substring(begin, node.TypePosition - begin);
            var marker = "\"" + key + "\":{\"$content\":";
            var pos = window.LastIndexOf(marker, StringComparison.Ordinal);
            if (pos < 0) return false;
            pos += marker.Length;
            while (pos < window.Length && char.IsWhiteSpace(window[pos])) pos++;
            var end = pos;
            while (end < window.Length && (char.IsDigit(window[end]) || window[end] == '-' || window[end] == '+' || window[end] == '.' || window[end] == 'e' || window[end] == 'E')) end++;
            return end > pos && float.TryParse(window.Substring(pos, end - pos), NumberStyles.Float, CultureInfo.InvariantCulture, out value);
        }

        private static string ReadNodeIdentifier(string serialized, Node node)
        {
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
            var i = pos + marker.Length;
            while (i < window.Length)
            {
                while (i < window.Length && (char.IsWhiteSpace(window[i]) || window[i] == ',')) i++;
                if (i >= window.Length || window[i] == ']') break;
                if (window[i] != '"') break;
                int end;
                var value = ReadJsonString(window, i + 1, out end);
                if (value == null) break;
                result.Add(value);
                i = end + 1;
            }
            return result;
        }

        private static int ParseAnswerPortIndex(string port)
        {
            if (string.IsNullOrEmpty(port)) return -1;
            var hash = port.LastIndexOf('#');
            if (hash < 0 || hash + 1 >= port.Length) return -1;
            var i = hash + 1;
            var value = 0;
            var digits = 0;
            while (i < port.Length && char.IsDigit(port[i]))
            {
                value = value * 10 + (port[i] - '0');
                digits++;
                i++;
            }
            return digits == 0 ? -1 : value;
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
    }
}

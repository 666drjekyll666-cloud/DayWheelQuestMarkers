using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Reflection;
using System.Text.RegularExpressions;
using UnityEngine;

namespace CalendarQuestsPins
{
    internal sealed class QuestRuleCache
    {
        internal sealed class NpcRules
        {
            internal string NpcId;
            internal object KnownNpc;
            internal object WorldObject;
            internal readonly Dictionary<string, List<TaskRule>> Rules = new Dictionary<string, List<TaskRule>>(StringComparer.Ordinal);
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
            internal string AuthoritativeZoneId;
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

        private readonly Dictionary<string, NpcRules> _byNpc = new Dictionary<string, NpcRules>(StringComparer.Ordinal);
        private readonly Dictionary<string, string> _zoneQualityMirrors = new Dictionary<string, string>(StringComparer.Ordinal);
        private readonly HashSet<string> _ambiguousZoneQualityMirrors = new HashSet<string>(StringComparer.Ordinal);
        private readonly Type _worldMapType = ReflectionUtil.FindType("WorldMap");
        private readonly Type _controllerType = ReflectionUtil.FindType("FlowCanvas.FlowScriptController");
        private readonly Type _flowSmartResType = ReflectionUtil.FindType("FlowCanvas.Nodes.Flow_SmartRes");
        private readonly Type _worldZoneType = ReflectionUtil.FindType("WorldZone");
        private MethodInfo _worldObjectGetter;
        private MethodInfo _smartResFactory;
        private MethodInfo _isEnough;
        private MethodInfo _getZoneById;
        private MethodInfo _getTotalQuality;
        private object _player;

        private static readonly string[] NpcIds =
        {
            "npc_astrologer", "npc_inquisitor", "npc_cultist", "npc_merchant", "npc_actress", "npc_bishop"
        };

        internal int SupportedRuleCount { get; private set; }
        internal int UnsupportedRuleCount { get; private set; }
        internal int ZoneQualityMirrorCount { get { return _zoneQualityMirrors.Count; } }

        internal bool Build(object save, object mainGame)
        {
            Clear();
            if (save == null || mainGame == null || _worldMapType == null || _controllerType == null || _flowSmartResType == null) return false;
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
                if (method.Name == "IsEnough" && ps.Length == 1 && ps[0].ParameterType.IsAssignableFrom(smartResType)) { _isEnough = method; break; }
            }
            if (_worldObjectGetter == null || _smartResFactory == null || _isEnough == null) return false;

            var knownNpcMap = ReadPeriodicKnownNpcs(save);
            for (var i = 0; i < NpcIds.Length; i++)
            {
                var npcId = NpcIds[i];
                object knownNpc;
                if (!knownNpcMap.TryGetValue(npcId, out knownNpc)) continue;
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

                var npc = new NpcRules { NpcId = npcId, KnownNpc = knownNpc, WorldObject = wgo };
                ParseNpcGraph(npc, serialized);
                _byNpc[npcId] = npc;
            }
            return _byNpc.Count > 0;
        }

        internal IEnumerable<NpcRules> AllNpcRules { get { return _byNpc.Values; } }

        internal bool NeedsRebuild()
        {
            if (!ReflectionUtil.IsUnityAlive(_player)) return true;
            foreach (var npc in _byNpc.Values) if (!ReflectionUtil.IsUnityAlive(npc.WorldObject)) return true;
            return false;
        }

        internal bool IsTaskActionable(NpcRules npc, string taskId, object unlockedPhrases, object blacklistedPhrases)
        {
            if (npc == null || taskId == null) return false;
            List<TaskRule> rules;
            if (!npc.Rules.TryGetValue(taskId, out rules)) return false;
            foreach (var rule in rules)
            {
                if (rule.Unsupported || string.IsNullOrEmpty(rule.AnswerId)) continue;
                if (!PhraseOpen(rule.AnswerId, unlockedPhrases, blacklistedPhrases)) continue;
                if (rule.Price != null && !IsEnough(rule.Price)) continue;
                if (rule.Lock != null && !IsEnough(rule.Lock)) continue;
                return true;
            }
            return false;
        }

        internal static bool IsVisibleTask(object task, out string taskId)
        {
            taskId = null;
            if (task == null) return false;
            object state;
            if (!ReflectionUtil.TryRead(task, "state", out state) || state == null) return false;
            try { if (Convert.ToInt32(state) != 0) return false; } catch { return false; }
            taskId = ReflectionUtil.ReadString(task, "id");
            return !string.IsNullOrEmpty(taskId);
        }

        internal void Clear()
        {
            _byNpc.Clear();
            _zoneQualityMirrors.Clear();
            _ambiguousZoneQualityMirrors.Clear();
            SupportedRuleCount = 0;
            UnsupportedRuleCount = 0;
            _player = null;
            _isEnough = null;
        }

        private Dictionary<string, object> ReadPeriodicKnownNpcs(object save)
        {
            var result = new Dictionary<string, object>(StringComparer.Ordinal);
            object known;
            if (!ReflectionUtil.TryRead(save, "known_npcs", out known) || known == null) return result;
            var npcs = ReflectionUtil.EnumerateMember(known, "npcs");
            if (npcs == null) return result;
            foreach (var npc in npcs)
            {
                var id = ReflectionUtil.ReadString(npc, "npc_id");
                if (id == null) continue;
                for (var i = 0; i < NpcIds.Length; i++) if (id == NpcIds[i]) { result[id] = npc; break; }
            }
            return result;
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

        private void ParseNpcGraph(NpcRules npc, string serialized)
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

            RegisterZoneQualityMirrors(serialized, nodes, incomingValue);

            foreach (var node in nodes.Values)
            {
                if (!node.Type.EndsWith("Flow_SetTaskState", StringComparison.Ordinal)) continue;
                if (!string.Equals(ReadNodeContent(serialized, node, "State"), "Complete", StringComparison.Ordinal)) continue;
                var taskId = ReadNodeContent(serialized, node, "Task");
                if (string.IsNullOrEmpty(taskId)) continue;
                var explicitNpcId = ReadNodeContent(serialized, node, "NPC id");
                if (!string.IsNullOrEmpty(explicitNpcId) && !string.Equals(explicitNpcId, npc.NpcId, StringComparison.Ordinal)) continue;
                var anchors = FindAnchors(nodes, incomingFlow, serialized, node.Id, 64);
                foreach (var anchor in anchors) AddRulesForAnchor(npc, taskId, anchor, serialized, nodes, connections, incomingValue);
            }
        }

        private void RegisterZoneQualityMirrors(string serialized, Dictionary<string, Node> nodes,
            Dictionary<string, List<Connection>> incomingValue)
        {
            foreach (var node in nodes.Values)
            {
                if (!node.Type.EndsWith("Flow_SetPlayerParam", StringComparison.Ordinal)) continue;
                var paramId = ReadNodeContent(serialized, node, "Param name");
                if (string.IsNullOrEmpty(paramId) || _ambiguousZoneQualityMirrors.Contains(paramId)) continue;

                List<Connection> valueInputs;
                if (!incomingValue.TryGetValue(node.Id, out valueInputs)) continue;
                string zoneId = null;
                var ambiguous = false;
                foreach (var c in valueInputs)
                {
                    if (!string.Equals(c.TargetPort, "Value", StringComparison.OrdinalIgnoreCase)) continue;
                    Node source;
                    if (!nodes.TryGetValue(c.SourceNode, out source) ||
                        source.Type.IndexOf("Flow_GetQualityOfZone", StringComparison.Ordinal) < 0) continue;
                    var candidate = ReadNodeContentAny(serialized, source, "zone_id", "Zone id");
                    if (string.IsNullOrEmpty(candidate)) continue;
                    if (zoneId == null) zoneId = candidate;
                    else if (!string.Equals(zoneId, candidate, StringComparison.Ordinal)) { ambiguous = true; break; }
                }
                if (ambiguous || string.IsNullOrEmpty(zoneId))
                {
                    if (ambiguous)
                    {
                        _zoneQualityMirrors.Remove(paramId);
                        _ambiguousZoneQualityMirrors.Add(paramId);
                    }
                    continue;
                }

                string existing;
                if (_zoneQualityMirrors.TryGetValue(paramId, out existing))
                {
                    if (string.Equals(existing, zoneId, StringComparison.Ordinal)) continue;
                    _zoneQualityMirrors.Remove(paramId);
                    _ambiguousZoneQualityMirrors.Add(paramId);
                    continue;
                }
                _zoneQualityMirrors[paramId] = zoneId;
            }
        }

        private void AddRulesForAnchor(NpcRules npc, string taskId, Anchor anchor, string serialized, Dictionary<string, Node> nodes,
            List<Connection> connections, Dictionary<string, List<Connection>> incomingValue)
        {
            if (!string.IsNullOrEmpty(anchor.MultiNodeId) && anchor.AnswerIndex >= 0)
            {
                AddRuleForAnswer(npc, taskId, anchor.MultiNodeId, anchor.AnswerIndex, serialized, nodes, connections, incomingValue);
                return;
            }
            if (string.IsNullOrEmpty(anchor.EventIdentifier)) return;
            var names = CandidateAnswerNames(anchor.EventIdentifier);
            foreach (var multi in nodes.Values)
            {
                if (!multi.Type.EndsWith("Flow_MultiAnswer", StringComparison.Ordinal)) continue;
                var answers = ReadMultiAnswers(serialized, multi);
                for (var i = 0; i < answers.Count; i++) if (names.Contains(answers[i])) AddRuleForAnswer(npc, taskId, multi.Id, i, serialized, nodes, connections, incomingValue);
            }
        }

        private void AddRuleForAnswer(NpcRules npc, string taskId, string multiId, int index, string serialized,
            Dictionary<string, Node> nodes, List<Connection> connections, Dictionary<string, List<Connection>> incomingValue)
        {
            Node multi;
            if (!nodes.TryGetValue(multiId, out multi)) return;
            var answers = ReadMultiAnswers(serialized, multi);
            if (index < 0 || index >= answers.Count || string.IsNullOrEmpty(answers[index])) return;
            var answerId = answers[index];
            List<TaskRule> taskRules;
            if (!npc.Rules.TryGetValue(taskId, out taskRules)) npc.Rules[taskId] = taskRules = new List<TaskRule>();
            var answerConnections = new List<Connection>();
            foreach (var c in connections) if (c.TargetNode == multiId && ParseAnswerPortIndex(c.TargetPort) == index) answerConnections.Add(c);
            if (answerConnections.Count == 0)
            {
                taskRules.Add(new TaskRule { AnswerId = answerId });
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
                        var req = ParseRequirement(serialized, source, npc.WorldObject);
                        if (req == null) { rule.Unsupported = true; break; }
                        if (string.Equals(gate.TargetPort, "price", StringComparison.OrdinalIgnoreCase)) rule.Price = req;
                        else rule.Lock = req;
                    }
                }
                taskRules.Add(rule);
                if (rule.Unsupported) UnsupportedRuleCount++; else { SupportedRuleCount++; anySupported = true; }
            }
            if (!anySupported)
            {
                taskRules.Add(new TaskRule { AnswerId = answerId, Unsupported = true });
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
            if (string.Equals(type, "GameRes", StringComparison.Ordinal))
            {
                string zoneId;
                if (_zoneQualityMirrors.TryGetValue(id, out zoneId) && !_ambiguousZoneQualityMirrors.Contains(id))
                    req.AuthoritativeZoneId = zoneId;
            }
            req.SmartRes = CreateSmartRes(req, linkedWgo);
            return req.SmartRes == null && string.IsNullOrEmpty(req.AuthoritativeZoneId) ? null : req;
        }

        private object CreateSmartRes(Requirement req, object linkedWgo)
        {
            try
            {
                var target = _smartResFactory.IsStatic ? null : Activator.CreateInstance(_smartResFactory.DeclaringType);
                var p = _smartResFactory.GetParameters();
                var args = new object[p.Length];
                for (var i = 0; i < p.Length; i++)
                {
                    var t = p[i].ParameterType;
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
            if (req == null) return false;
            if (!string.IsNullOrEmpty(req.AuthoritativeZoneId)) return IsZoneQualityEnough(req.AuthoritativeZoneId, req.Value);
            if (req.SmartRes == null || _player == null || _isEnough == null) return false;
            try { var value = _isEnough.Invoke(_player, new[] { req.SmartRes }); return value is bool && (bool)value; }
            catch { return false; }
        }

        private bool IsZoneQualityEnough(string zoneId, float required)
        {
            try
            {
                if (_worldZoneType == null) return false;
                if (_getZoneById == null)
                {
                    foreach (var method in _worldZoneType.GetMethods(ReflectionUtil.AnyStatic))
                    {
                        var ps = method.GetParameters();
                        if (method.Name == "GetZoneByID" && ps.Length == 2 &&
                            ps[0].ParameterType == typeof(string) && ps[1].ParameterType == typeof(bool))
                        {
                            _getZoneById = method;
                            break;
                        }
                    }
                }
                if (_getZoneById == null) return false;
                var zone = _getZoneById.Invoke(null, new object[] { zoneId, false });
                if (zone == null) return false;

                if (_getTotalQuality == null || !_getTotalQuality.DeclaringType.IsInstanceOfType(zone))
                {
                    _getTotalQuality = null;
                    foreach (var method in zone.GetType().GetMethods(ReflectionUtil.AnyInstance))
                    {
                        if (method.Name == "GetTotalQuality" && method.GetParameters().Length == 0)
                        {
                            _getTotalQuality = method;
                            break;
                        }
                    }
                }
                if (_getTotalQuality == null) return false;
                var raw = _getTotalQuality.Invoke(zone, null);
                if (raw == null) return false;
                return Convert.ToSingle(raw, CultureInfo.InvariantCulture) >= required;
            }
            catch { return false; }
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
            foreach (var item in enumerable) if (string.Equals(item as string, value, StringComparison.Ordinal)) return true;
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
            foreach (var a in list) if (a.EventIdentifier == candidate.EventIdentifier && a.MultiNodeId == candidate.MultiNodeId && a.AnswerIndex == candidate.AnswerIndex) return;
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
                if (!string.IsNullOrEmpty(src) && !string.IsNullOrEmpty(dst)) result.Add(new Connection { SourcePort = sp, TargetPort = tp, SourceNode = src, TargetNode = dst });
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
            while (i < port.Length && char.IsDigit(port[i])) { value = value * 10 + (port[i] - '0'); digits++; i++; }
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
            if (identifier.StartsWith("@", StringComparison.Ordinal)) result.Add(identifier.Substring(1)); else result.Add("@" + identifier);
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
                try { return Regex.Unescape(raw.Replace("\\/", "/")); } catch { return raw; }
            }
            return null;
        }
    }
}

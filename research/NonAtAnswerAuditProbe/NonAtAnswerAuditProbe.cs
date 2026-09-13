using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Reflection;
using System.Text.RegularExpressions;
using BepInEx;
using CalendarQuestsPins;
using UnityEngine;

namespace DayWheelNonAtAnswerAuditProbe
{
    [BepInPlugin(PluginGuid, PluginName, PluginVersion)]
    public sealed class NonAtAnswerAuditProbe : BaseUnityPlugin
    {
        private const string PluginGuid = "nikich.gyk.daywheel.nonat-answer-audit";
        private const string PluginName = "Day Wheel Quest Markers - non-@ answer universe audit";
        private const string PluginVersion = "0.1.0";

        private static readonly string[] NpcIds =
        {
            "npc_astrologer", "npc_inquisitor", "npc_cultist", "npc_merchant", "npc_actress", "npc_bishop"
        };

        private sealed class Node
        {
            internal string Id;
            internal string Type;
            internal int TypePosition;
            internal string Uid;
            internal string SourceOutputUid;
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
            internal string MultiNodeId;
            internal int AnswerIndex;
            internal int FunctionJumps;
        }

        private sealed class Candidate
        {
            internal string NpcId;
            internal string AnswerId;
            internal string MultiNodeId;
            internal int AnswerIndex;
            internal int FunctionJumps;
            internal string Gate;
            internal bool Reversible;
            internal bool BlacklistedNow;
            internal bool UtilityLike;
        }

        private Type _mainGameType;
        private Type _worldMapType;
        private Type _controllerType;
        private MethodInfo _worldObjectGetter;
        private bool _done;
        private float _nextTry;

        private void Awake()
        {
            _mainGameType = ReflectionUtil.FindType("MainGame");
            _worldMapType = ReflectionUtil.FindType("WorldMap");
            _controllerType = ReflectionUtil.FindType("FlowCanvas.FlowScriptController");
            _worldObjectGetter = FindWorldObjectGetter();
            _nextTry = Time.realtimeSinceStartup + 1f;
            Logger.LogInfo("NONAT_AUDIT_PROBE loaded version=" + PluginVersion + " readOnly=True");
        }

        private void Update()
        {
            if (_done || Time.realtimeSinceStartup < _nextTry) return;
            _nextTry = Time.realtimeSinceStartup + 1f;

            var mainGame = ReflectionUtil.FindActiveUnityInstance(_mainGameType);
            if (mainGame == null || _worldMapType == null || _controllerType == null || _worldObjectGetter == null) return;

            object started;
            if (!TryReadStatic(_mainGameType, "game_started", out started) || !(started is bool) || !(bool)started) return;
            object starting;
            if (TryReadStatic(_mainGameType, "game_starting", out starting) && starting is bool && (bool)starting) return;

            object save;
            if (!ReflectionUtil.TryRead(mainGame, "save", out save) || save == null || !HasLoadedKnownNpcs(save)) return;

            try
            {
                RunAudit(save);
                _done = true;
            }
            catch (Exception ex)
            {
                Logger.LogError("NONAT_AUDIT_FATAL " + ex);
                _done = true;
            }
        }

        private void RunAudit(object save)
        {
            object blacklisted = null;
            ReflectionUtil.TryRead(save, "black_list_of_phrases", out blacklisted);

            var allCandidates = new List<Candidate>();
            var overallAnswers = 0;
            var overallNonAt = 0;
            var overallUniqueNonAt = new HashSet<string>(StringComparer.Ordinal);
            var loadedGraphs = 0;

            Logger.LogInfo("NONAT_AUDIT_BEGIN game=1.407 npcs=6 policy=exact self-blacklist census for every non-@ authored MultiAnswer entry");

            for (var n = 0; n < NpcIds.Length; n++)
            {
                var npcId = NpcIds[n];
                string serialized;
                if (!TryGetSerializedGraph(npcId, out serialized))
                {
                    Logger.LogWarning("NONAT_AUDIT_GRAPH npc=" + npcId + " loaded=False");
                    continue;
                }

                loadedGraphs++;
                var nodes = BuildNodeIndex(serialized);
                var connections = ParseConnections(serialized);
                var incomingFlow = BuildIncomingFlow(nodes, connections);
                var callsByUid = BuildCallsByUid(nodes);
                var removedIds = FindBlacklistRemovals(nodes, serialized);
                var npcCandidates = new List<Candidate>();
                var nonAtUnique = new HashSet<string>(StringComparer.Ordinal);
                var answerOccurrences = 0;
                var nonAtOccurrences = 0;

                foreach (var node in nodes.Values)
                {
                    if (!node.Type.EndsWith("Flow_MultiAnswer", StringComparison.Ordinal)) continue;
                    var answers = ReadMultiAnswers(serialized, node);
                    answerOccurrences += answers.Count;
                    for (var i = 0; i < answers.Count; i++)
                    {
                        var answer = answers[i];
                        if (string.IsNullOrEmpty(answer) || answer.StartsWith("@", StringComparison.Ordinal)) continue;
                        nonAtOccurrences++;
                        nonAtUnique.Add(answer);
                        overallUniqueNonAt.Add(answer);
                    }
                }

                foreach (var node in nodes.Values)
                {
                    string phraseId;
                    if (!TryReadBlacklistAdd(node, serialized, out phraseId)) continue;
                    if (string.IsNullOrEmpty(phraseId) || phraseId.StartsWith("@", StringComparison.Ordinal)) continue;

                    var anchors = FindExactAnswerAnchors(nodes, incomingFlow, callsByUid, serialized, node.Id, 96);
                    for (var a = 0; a < anchors.Count; a++)
                    {
                        var anchor = anchors[a];
                        Node multi;
                        if (!nodes.TryGetValue(anchor.MultiNodeId, out multi)) continue;
                        var answers = ReadMultiAnswers(serialized, multi);
                        if (anchor.AnswerIndex < 0 || anchor.AnswerIndex >= answers.Count) continue;
                        var answerId = answers[anchor.AnswerIndex];
                        if (!string.Equals(answerId, phraseId, StringComparison.Ordinal)) continue;
                        if (ContainsCandidate(npcCandidates, answerId, anchor.MultiNodeId, anchor.AnswerIndex)) continue;

                        var candidate = new Candidate
                        {
                            NpcId = npcId,
                            AnswerId = answerId,
                            MultiNodeId = anchor.MultiNodeId,
                            AnswerIndex = anchor.AnswerIndex,
                            FunctionJumps = anchor.FunctionJumps,
                            Gate = DescribeGate(serialized, nodes, connections, anchor.MultiNodeId, anchor.AnswerIndex),
                            Reversible = removedIds.Contains(answerId),
                            BlacklistedNow = ContainsString(blacklisted, answerId),
                            UtilityLike = IsUtilityLike(answerId)
                        };
                        npcCandidates.Add(candidate);
                        allCandidates.Add(candidate);
                    }
                }

                overallAnswers += answerOccurrences;
                overallNonAt += nonAtOccurrences;
                Logger.LogInfo("NONAT_AUDIT_NPC npc=" + npcId +
                               " nodes=" + nodes.Count +
                               " connections=" + connections.Count +
                               " answerOccurrences=" + answerOccurrences +
                               " nonAtOccurrences=" + nonAtOccurrences +
                               " nonAtUnique=" + nonAtUnique.Count +
                               " exactSelfNonAt=" + npcCandidates.Count);

                npcCandidates.Sort(CompareCandidates);
                for (var i = 0; i < npcCandidates.Count; i++) LogCandidate(npcCandidates[i]);
            }

            var uniqueSelf = new HashSet<string>(StringComparer.Ordinal);
            var reversible = 0;
            var utility = 0;
            var astrologerPortal = false;
            var snakePersuade = false;
            for (var i = 0; i < allCandidates.Count; i++)
            {
                var c = allCandidates[i];
                uniqueSelf.Add(c.NpcId + "\n" + c.AnswerId);
                if (c.Reversible) reversible++;
                if (c.UtilityLike) utility++;
                if (c.NpcId == "npc_astrologer" && c.AnswerId == "astrologer_2a_1b_6c") astrologerPortal = true;
                if (c.NpcId == "npc_cultist" && c.AnswerId == "snake_1a") snakePersuade = true;
            }

            Logger.LogInfo("NONAT_AUDIT_SUMMARY graphs=" + loadedGraphs + "/6" +
                           " answerOccurrences=" + overallAnswers +
                           " nonAtOccurrences=" + overallNonAt +
                           " nonAtUnique=" + overallUniqueNonAt.Count +
                           " exactSelfNonAtOccurrences=" + allCandidates.Count +
                           " exactSelfNonAtUniqueNpcAnswer=" + uniqueSelf.Count +
                           " reversibleCandidates=" + reversible +
                           " utilityLikeCandidates=" + utility);
            Logger.LogInfo("NONAT_AUDIT_KNOWN npc=npc_astrologer answer=astrologer_2a_1b_6c exactSelf=" + astrologerPortal);
            Logger.LogInfo("NONAT_AUDIT_KNOWN npc=npc_cultist answer=snake_1a exactSelf=" + snakePersuade);
            Logger.LogInfo("NONAT_AUDIT_END");
        }

        private void LogCandidate(Candidate c)
        {
            Logger.LogInfo("NONAT_SELF npc=" + c.NpcId +
                           " answer=" + EscapeLog(c.AnswerId) +
                           " multi=" + c.MultiNodeId +
                           " index=" + c.AnswerIndex +
                           " functionJumps=" + c.FunctionJumps +
                           " gate=" + c.Gate +
                           " blacklistedNow=" + c.BlacklistedNow +
                           " reversible=" + c.Reversible +
                           " utilityLike=" + c.UtilityLike);
        }

        private bool TryGetSerializedGraph(string npcId, out string serialized)
        {
            serialized = null;
            object wgo;
            try { wgo = _worldObjectGetter.Invoke(null, new object[] { npcId, true }); }
            catch { return false; }
            var component = wgo as Component;
            if (component == null) return false;
            var controller = component.GetComponent(_controllerType);
            if (controller == null) return false;
            object graph;
            if (!ReflectionUtil.TryRead(controller, "_graph", out graph) || graph == null) return false;
            object value;
            if (!ReflectionUtil.TryRead(graph, "_serializedGraph", out value)) return false;
            serialized = value as string;
            return !string.IsNullOrEmpty(serialized);
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

        private static bool HasLoadedKnownNpcs(object save)
        {
            object known;
            if (!ReflectionUtil.TryRead(save, "known_npcs", out known) || known == null) return false;
            var npcs = ReflectionUtil.EnumerateMember(known, "npcs");
            if (npcs == null) return false;
            foreach (var ignored in npcs) return true;
            return false;
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
                    if (!string.IsNullOrEmpty(id))
                    {
                        var node = new Node { Id = id, Type = type, TypePosition = typePos };
                        node.Uid = ReadRawStringProperty(serialized, node, "_UID");
                        node.SourceOutputUid = ReadRawStringProperty(serialized, node, "_sourceOutputUID");
                        result[id] = node;
                    }
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

        private static Dictionary<string, List<Connection>> BuildIncomingFlow(Dictionary<string, Node> nodes, List<Connection> connections)
        {
            var result = new Dictionary<string, List<Connection>>(StringComparer.Ordinal);
            for (var i = 0; i < connections.Count; i++)
            {
                var c = connections[i];
                if (!IsFlowConnection(nodes, c)) continue;
                List<Connection> list;
                if (!result.TryGetValue(c.TargetNode, out list)) result[c.TargetNode] = list = new List<Connection>();
                list.Add(c);
            }
            return result;
        }

        private static bool IsFlowConnection(Dictionary<string, Node> nodes, Connection c)
        {
            if (c == null) return false;
            if (string.IsNullOrWhiteSpace(c.TargetPort) || string.Equals(c.TargetPort, "In", StringComparison.OrdinalIgnoreCase)) return true;
            Node target;
            if (!nodes.TryGetValue(c.TargetNode, out target)) return false;
            if (!target.Type.EndsWith("Flow_WaitForFlow", StringComparison.Ordinal)) return false;
            int ignored;
            return int.TryParse(c.TargetPort, NumberStyles.Integer, CultureInfo.InvariantCulture, out ignored);
        }

        private static Dictionary<string, List<string>> BuildCallsByUid(Dictionary<string, Node> nodes)
        {
            var result = new Dictionary<string, List<string>>(StringComparer.Ordinal);
            foreach (var node in nodes.Values)
            {
                if (!node.Type.EndsWith("CustomFunctionCall", StringComparison.Ordinal) || string.IsNullOrEmpty(node.SourceOutputUid)) continue;
                List<string> list;
                if (!result.TryGetValue(node.SourceOutputUid, out list)) result[node.SourceOutputUid] = list = new List<string>();
                list.Add(node.Id);
            }
            return result;
        }

        private static List<Anchor> FindExactAnswerAnchors(Dictionary<string, Node> nodes,
            Dictionary<string, List<Connection>> incomingFlow, Dictionary<string, List<string>> callsByUid,
            string serialized, string startNodeId, int maxDepth)
        {
            var result = new List<Anchor>();
            var queue = new Queue<Tuple<string, int, int>>();
            var seen = new HashSet<string>(StringComparer.Ordinal) { startNodeId };
            queue.Enqueue(Tuple.Create(startNodeId, 0, 0));

            while (queue.Count > 0)
            {
                var current = queue.Dequeue();
                if (current.Item2 >= maxDepth) continue;
                List<Connection> incoming;
                if (!incomingFlow.TryGetValue(current.Item1, out incoming)) continue;
                for (var i = 0; i < incoming.Count; i++)
                {
                    var c = incoming[i];
                    Node source;
                    if (!nodes.TryGetValue(c.SourceNode, out source)) continue;
                    if (source.Type.EndsWith("Flow_MultiAnswer", StringComparison.Ordinal))
                    {
                        AddAnchor(result, new Anchor
                        {
                            MultiNodeId = source.Id,
                            AnswerIndex = ParseOutPortIndex(c.SourcePort),
                            FunctionJumps = current.Item3
                        });
                        continue;
                    }
                    if (source.Type.EndsWith("CustomFunctionEvent", StringComparison.Ordinal) && !string.IsNullOrEmpty(source.Uid))
                    {
                        List<string> callers;
                        if (callsByUid.TryGetValue(source.Uid, out callers))
                        {
                            for (var j = 0; j < callers.Count; j++)
                            {
                                var callId = callers[j];
                                if (seen.Add(callId)) queue.Enqueue(Tuple.Create(callId, current.Item2 + 1, current.Item3 + 1));
                            }
                        }
                        continue;
                    }
                    if (seen.Add(source.Id)) queue.Enqueue(Tuple.Create(source.Id, current.Item2 + 1, current.Item3));
                }
            }
            return result;
        }

        private static void AddAnchor(List<Anchor> list, Anchor candidate)
        {
            if (candidate == null || string.IsNullOrEmpty(candidate.MultiNodeId) || candidate.AnswerIndex < 0) return;
            for (var i = 0; i < list.Count; i++)
            {
                var a = list[i];
                if (a.MultiNodeId == candidate.MultiNodeId && a.AnswerIndex == candidate.AnswerIndex)
                {
                    if (candidate.FunctionJumps < a.FunctionJumps) a.FunctionJumps = candidate.FunctionJumps;
                    return;
                }
            }
            list.Add(candidate);
        }

        private static HashSet<string> FindBlacklistRemovals(Dictionary<string, Node> nodes, string serialized)
        {
            var result = new HashSet<string>(StringComparer.Ordinal);
            foreach (var node in nodes.Values)
            {
                if (!node.Type.EndsWith("Flow_AddPhraseToBlacklist", StringComparison.Ordinal)) continue;
                bool remove;
                if (!TryReadNodeBool(serialized, node, "remove", out remove) || !remove) continue;
                var id = ReadNodeContent(serialized, node, "Phrase ID") ?? ReadNodeContent(serialized, node, "in_phrase");
                if (!string.IsNullOrEmpty(id)) result.Add(id);
            }
            return result;
        }

        private static bool TryReadBlacklistAdd(Node node, string serialized, out string phraseId)
        {
            phraseId = null;
            if (node == null) return false;
            if (node.Type.EndsWith("Flow_BlackListPhrase", StringComparison.Ordinal))
            {
                phraseId = ReadNodeContent(serialized, node, "phrase") ?? ReadNodeContent(serialized, node, "Phrase");
                return !string.IsNullOrEmpty(phraseId);
            }
            if (!node.Type.EndsWith("Flow_AddPhraseToBlacklist", StringComparison.Ordinal)) return false;
            bool remove;
            if (TryReadNodeBool(serialized, node, "remove", out remove) && remove) return false;
            phraseId = ReadNodeContent(serialized, node, "Phrase ID") ?? ReadNodeContent(serialized, node, "in_phrase");
            return !string.IsNullOrEmpty(phraseId);
        }

        private static string DescribeGate(string serialized, Dictionary<string, Node> nodes, List<Connection> connections,
            string multiId, int answerIndex)
        {
            var answerDataNodes = new List<Node>();
            for (var i = 0; i < connections.Count; i++)
            {
                var c = connections[i];
                if (!string.Equals(c.TargetNode, multiId, StringComparison.Ordinal) || ParseAnswerPortIndex(c.TargetPort) != answerIndex) continue;
                Node source;
                if (!nodes.TryGetValue(c.SourceNode, out source)) continue;
                if (source.Type.IndexOf("Flow_Answer", StringComparison.Ordinal) >= 0) answerDataNodes.Add(source);
            }
            if (answerDataNodes.Count == 0) return "none";

            var parts = new List<string>();
            for (var n = 0; n < answerDataNodes.Count; n++)
            {
                var answerNode = answerDataNodes[n];
                var hadGate = false;
                for (var i = 0; i < connections.Count; i++)
                {
                    var c = connections[i];
                    if (!string.Equals(c.TargetNode, answerNode.Id, StringComparison.Ordinal)) continue;
                    if (!string.Equals(c.TargetPort, "price", StringComparison.OrdinalIgnoreCase) &&
                        !string.Equals(c.TargetPort, "lock", StringComparison.OrdinalIgnoreCase)) continue;
                    Node smart;
                    if (!nodes.TryGetValue(c.SourceNode, out smart) || smart.Type.IndexOf("Flow_SmartRes", StringComparison.Ordinal) < 0)
                    {
                        parts.Add(c.TargetPort + ":unsupported");
                        hadGate = true;
                        continue;
                    }
                    var resType = ReadNodeContentAny(serialized, smart, "res_type", "Res type") ?? "?";
                    var id = ReadNodeContentAny(serialized, smart, "id", "Id") ?? "?";
                    float value;
                    var valueText = TryReadNodeNumber(serialized, smart, out value)
                        ? value.ToString("0.####", CultureInfo.InvariantCulture)
                        : "?";
                    parts.Add(c.TargetPort + ":" + resType + ":" + id + "=" + valueText);
                    hadGate = true;
                }
                if (!hadGate) parts.Add("answerData:no-gate");
            }
            return parts.Count == 0 ? "answerData:unresolved" : string.Join(",", parts.ToArray());
        }

        private static bool ContainsCandidate(List<Candidate> candidates, string answerId, string multiId, int index)
        {
            for (var i = 0; i < candidates.Count; i++)
            {
                var c = candidates[i];
                if (c.AnswerId == answerId && c.MultiNodeId == multiId && c.AnswerIndex == index) return true;
            }
            return false;
        }

        private static int CompareCandidates(Candidate a, Candidate b)
        {
            var c = string.CompareOrdinal(a.AnswerId, b.AnswerId);
            if (c != 0) return c;
            c = string.CompareOrdinal(a.MultiNodeId, b.MultiNodeId);
            return c != 0 ? c : a.AnswerIndex.CompareTo(b.AnswerIndex);
        }

        private static bool IsUtilityLike(string id)
        {
            return string.Equals(id, "Leave", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(id, "Back", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(id, "Trade", StringComparison.OrdinalIgnoreCase);
        }

        private static string EscapeLog(string value)
        {
            return value == null ? "<null>" : value.Replace(" ", "_");
        }

        private static bool ContainsString(object collection, string value)
        {
            var enumerable = collection as IEnumerable;
            if (enumerable == null) return false;
            foreach (var item in enumerable)
                if (string.Equals(item as string, value, StringComparison.Ordinal)) return true;
            return false;
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

        private static string ReadRawStringProperty(string serialized, Node node, string key)
        {
            if (node == null) return null;
            var begin = Math.Max(0, node.TypePosition - 2600);
            var window = serialized.Substring(begin, node.TypePosition - begin);
            var marker = "\"" + key + "\":\"";
            var pos = window.LastIndexOf(marker, StringComparison.Ordinal);
            if (pos < 0) return null;
            int end;
            return ReadJsonString(window, pos + marker.Length, out end);
        }

        private static bool TryReadNodeBool(string serialized, Node node, string key, out bool value)
        {
            value = false;
            if (node == null) return false;
            var begin = Math.Max(0, node.TypePosition - 2600);
            var window = serialized.Substring(begin, node.TypePosition - begin);
            var marker = "\"" + key + "\":{\"$content\":";
            var pos = window.LastIndexOf(marker, StringComparison.Ordinal);
            if (pos < 0) return false;
            pos += marker.Length;
            while (pos < window.Length && char.IsWhiteSpace(window[pos])) pos++;
            if (window.IndexOf("true", pos, StringComparison.Ordinal) == pos) { value = true; return true; }
            if (window.IndexOf("false", pos, StringComparison.Ordinal) == pos) { value = false; return true; }
            return false;
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

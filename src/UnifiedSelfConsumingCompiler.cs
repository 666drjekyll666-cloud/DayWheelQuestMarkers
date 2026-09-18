using System;
using System.Collections.Generic;
using System.Globalization;
using System.Reflection;
using System.Text.RegularExpressions;

namespace CalendarQuestsPins
{
    /// <summary>
    /// Bootstrap-only compiler for the verified non-@ exact-self-consuming answer universe.
    ///
    /// The accepted 1.0.35 supplement proved that @ is only an identifier convention. This compiler
    /// converts the same verified non-@ class directly into WeekdayInteractionRuleCache.TopicRule
    /// instances so schema 4 can persist one unified self-consuming rule set. It deliberately does
    /// not broaden the accepted owner/cross task-completion traversal.
    /// </summary>
    internal sealed class UnifiedSelfConsumingCompiler
    {
        internal sealed class Stats
        {
            internal int GraphCount;
            internal int NonAtUnique;
            internal int ExactSelf;
            internal int Reversible;
            internal int Utility;
            internal int CompletionExcluded;
            internal int NavigationExcluded;
            internal int AdmittedTopics;
            internal int SupportedVariants;
            internal int UnsupportedVariants;
        }

        private const int ExpectedGraphCount = 6;
        private const int ExpectedNonAtUnique = 77;
        private const int ExpectedExactSelf = 19;

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

        private sealed class ExactAnchor
        {
            internal string MultiNodeId;
            internal int AnswerIndex = -1;
        }

        private sealed class CompletionAnchor
        {
            internal string EventIdentifier;
            internal string MultiNodeId;
            internal int AnswerIndex = -1;
        }

        private readonly MethodInfo _createSmartRes;

        internal UnifiedSelfConsumingCompiler()
        {
            _createSmartRes = typeof(WeekdayInteractionRuleCache).GetMethod(
                "CreateSmartRes", BindingFlags.Instance | BindingFlags.NonPublic);
        }

        internal bool Compile(IList<WeekdayInteractionRuleCache.TargetRules> targets,
            IDictionary<string, string> graphs, WeekdayInteractionRuleCache cache,
            NavigationReachabilityCache navigation, out Stats stats, out string failure)
        {
            stats = new Stats();
            failure = null;
            if (targets == null || graphs == null || cache == null || navigation == null || _createSmartRes == null)
            {
                failure = "unified self-consuming compiler inputs are incomplete";
                return false;
            }

            var targetsByNpc = new Dictionary<string, WeekdayInteractionRuleCache.TargetRules>(StringComparer.Ordinal);
            for (var i = 0; i < targets.Count; i++)
            {
                var target = targets[i];
                if (target != null && !string.IsNullOrEmpty(target.NpcId)) targetsByNpc[target.NpcId] = target;
            }

            var allNonAt = new HashSet<string>(StringComparer.Ordinal);
            var rawExactSelf = new HashSet<string>(StringComparer.Ordinal);

            for (var n = 0; n < NpcIds.Length; n++)
            {
                var npcId = NpcIds[n];
                string serialized;
                WeekdayInteractionRuleCache.TargetRules target;
                if (!graphs.TryGetValue(npcId, out serialized) || string.IsNullOrEmpty(serialized) ||
                    !targetsByNpc.TryGetValue(npcId, out target) || target == null || target.WorldObject == null)
                {
                    failure = "unified self-consuming graph/target unavailable: " + npcId;
                    return false;
                }
                stats.GraphCount++;

                var nodes = BuildNodeIndex(serialized);
                var connections = ParseConnections(serialized);
                if (nodes.Count == 0 || connections.Count == 0)
                {
                    failure = "unified self-consuming graph index is empty: " + npcId;
                    return false;
                }

                var productionIncoming = BuildIncomingFlow(nodes, connections, false);
                var exactIncoming = BuildIncomingFlow(nodes, connections, true);
                var callsByUid = BuildCallsByUid(nodes);
                var removals = FindBlacklistRemovals(nodes, serialized);
                var completionAnswerIds = FindProductionCompletionAnswerIds(nodes, productionIncoming, serialized);
                AddProductionTaskRuleAnswerIds(completionAnswerIds, target);
                var admitted = new Dictionary<string, WeekdayInteractionRuleCache.TopicRule>(StringComparer.Ordinal);

                foreach (var node in nodes.Values)
                {
                    if (!node.Type.EndsWith("Flow_MultiAnswer", StringComparison.Ordinal)) continue;
                    var answers = ReadMultiAnswers(serialized, node);
                    for (var i = 0; i < answers.Count; i++)
                    {
                        var id = answers[i];
                        if (!string.IsNullOrEmpty(id) && !id.StartsWith("@", StringComparison.Ordinal)) allNonAt.Add(id);
                    }
                }

                foreach (var node in nodes.Values)
                {
                    string blacklistedId;
                    if (!TryReadBlacklistAdd(node, serialized, out blacklistedId) ||
                        string.IsNullOrEmpty(blacklistedId) || blacklistedId.StartsWith("@", StringComparison.Ordinal)) continue;

                    var anchors = FindExactAnswerAnchors(nodes, exactIncoming, callsByUid, node.Id, 96);
                    for (var a = 0; a < anchors.Count; a++)
                    {
                        var anchor = anchors[a];
                        Node multi;
                        if (!nodes.TryGetValue(anchor.MultiNodeId, out multi)) continue;
                        var answers = ReadMultiAnswers(serialized, multi);
                        if (anchor.AnswerIndex < 0 || anchor.AnswerIndex >= answers.Count) continue;
                        var answerId = answers[anchor.AnswerIndex];
                        if (!string.Equals(answerId, blacklistedId, StringComparison.Ordinal)) continue;

                        var universeKey = npcId + "\n" + answerId;
                        if (!rawExactSelf.Add(universeKey)) continue;
                        if (removals.Contains(answerId)) stats.Reversible++;
                        if (IsUtilityLike(answerId)) stats.Utility++;
                        if (completionAnswerIds.Contains(answerId))
                        {
                            stats.CompletionExcluded++;
                            continue;
                        }
                        // A descendant reached only after selecting an already task-owned answer is
                        // part of that same NPC visit, not an independent reminder. Keep ordinary
                        // submenu ancestors valid; only known production task answers block independence.
                        if (!navigation.HasInteractionRootPathWithoutAncestors(npcId, answerId, completionAnswerIds))
                        {
                            stats.NavigationExcluded++;
                            continue;
                        }

                        var topic = BuildTopic(answerId, anchor.MultiNodeId, anchor.AnswerIndex,
                            serialized, nodes, connections, target.WorldObject, cache);
                        admitted[answerId] = topic;
                    }
                }

                var ids = new List<string>(admitted.Keys);
                ids.Sort(StringComparer.Ordinal);
                for (var i = 0; i < ids.Count; i++)
                {
                    var topic = admitted[ids[i]];
                    if (topic == null || topic.Variants.Count == 0) continue;
                    target.Topics.Add(topic);
                    stats.AdmittedTopics++;
                    for (var v = 0; v < topic.Variants.Count; v++)
                    {
                        if (topic.Variants[v] != null && topic.Variants[v].Unsupported) stats.UnsupportedVariants++;
                        else stats.SupportedVariants++;
                    }
                }
            }

            stats.NonAtUnique = allNonAt.Count;
            stats.ExactSelf = rawExactSelf.Count;
            if (!Validate(stats, out failure)) return false;
            return true;
        }

        internal static bool Validate(Stats stats, out string failure)
        {
            failure = null;
            if (stats == null)
            {
                failure = "unified self-consuming stats are null";
                return false;
            }
            if (stats.GraphCount != ExpectedGraphCount || stats.NonAtUnique != ExpectedNonAtUnique ||
                stats.ExactSelf != ExpectedExactSelf || stats.Reversible != 0 || stats.Utility != 0 ||
                stats.AdmittedTopics + stats.CompletionExcluded + stats.NavigationExcluded != ExpectedExactSelf)
            {
                failure = "unified self-consuming universe mismatch: graphs=" + stats.GraphCount + "/" + ExpectedGraphCount +
                          ", nonAtUnique=" + stats.NonAtUnique + "/" + ExpectedNonAtUnique +
                          ", exactSelf=" + stats.ExactSelf + "/" + ExpectedExactSelf +
                          ", reversible=" + stats.Reversible +
                          ", utility=" + stats.Utility +
                          ", admitted=" + stats.AdmittedTopics +
                          ", completionExcluded=" + stats.CompletionExcluded +
                          ", navigationExcluded=" + stats.NavigationExcluded;
                return false;
            }
            return true;
        }

        private WeekdayInteractionRuleCache.TopicRule BuildTopic(string answerId, string multiId, int answerIndex,
            string serialized, Dictionary<string, Node> nodes, List<Connection> connections,
            object worldObject, WeekdayInteractionRuleCache cache)
        {
            var topic = new WeekdayInteractionRuleCache.TopicRule { AnswerId = answerId };
            var answerConnections = FindAnswerConnections(connections, multiId, answerIndex);
            if (answerConnections.Count == 0)
            {
                topic.Variants.Add(new WeekdayInteractionRuleCache.RuleVariant { AnswerId = answerId });
                return topic;
            }

            var found = false;
            for (var i = 0; i < answerConnections.Count; i++)
            {
                Node answerNode;
                if (!nodes.TryGetValue(answerConnections[i].SourceNode, out answerNode) ||
                    answerNode.Type.IndexOf("Flow_Answer", StringComparison.Ordinal) < 0) continue;
                found = true;
                AddVariant(topic, BuildVariant(answerId, serialized, answerNode, nodes, connections, worldObject, cache));
            }
            if (!found)
                topic.Variants.Add(new WeekdayInteractionRuleCache.RuleVariant { AnswerId = answerId, Unsupported = true });
            return topic;
        }

        private WeekdayInteractionRuleCache.RuleVariant BuildVariant(string answerId, string serialized, Node answerNode,
            Dictionary<string, Node> nodes, List<Connection> connections, object worldObject,
            WeekdayInteractionRuleCache cache)
        {
            var variant = new WeekdayInteractionRuleCache.RuleVariant { AnswerId = answerId };
            for (var i = 0; i < connections.Count; i++)
            {
                var c = connections[i];
                if (!string.Equals(c.TargetNode, answerNode.Id, StringComparison.Ordinal)) continue;
                var isPrice = string.Equals(c.TargetPort, "price", StringComparison.OrdinalIgnoreCase);
                var isLock = string.Equals(c.TargetPort, "lock", StringComparison.OrdinalIgnoreCase);
                if (!isPrice && !isLock) continue;
                Node smart;
                if (!nodes.TryGetValue(c.SourceNode, out smart) || smart.Type.IndexOf("Flow_SmartRes", StringComparison.Ordinal) < 0)
                {
                    variant.Unsupported = true;
                    break;
                }
                var requirement = ParseRequirement(serialized, smart, worldObject, cache);
                if (requirement == null)
                {
                    variant.Unsupported = true;
                    break;
                }
                if (isPrice) variant.Price = requirement; else variant.Lock = requirement;
            }
            return variant;
        }

        private WeekdayInteractionRuleCache.Requirement ParseRequirement(string serialized, Node node,
            object linkedWgo, WeekdayInteractionRuleCache cache)
        {
            var type = ReadNodeContentAny(serialized, node, "res_type", "Res type");
            var id = ReadNodeContentAny(serialized, node, "id", "Id");
            float value;
            if (string.IsNullOrEmpty(type) || string.IsNullOrEmpty(id) || !TryReadNodeNumber(serialized, node, out value)) return null;
            var requirement = new WeekdayInteractionRuleCache.Requirement { ResType = type, Id = id, Value = value };
            try { requirement.SmartRes = _createSmartRes.Invoke(cache, new object[] { requirement, linkedWgo }); }
            catch { requirement.SmartRes = null; }
            return requirement.SmartRes == null ? null : requirement;
        }

        private static void AddVariant(WeekdayInteractionRuleCache.TopicRule topic,
            WeekdayInteractionRuleCache.RuleVariant candidate)
        {
            for (var i = 0; i < topic.Variants.Count; i++)
            {
                var existing = topic.Variants[i];
                if (existing.Unsupported != candidate.Unsupported) continue;
                if (SameRequirement(existing.Price, candidate.Price) && SameRequirement(existing.Lock, candidate.Lock)) return;
            }
            topic.Variants.Add(candidate);
        }

        private static bool SameRequirement(WeekdayInteractionRuleCache.Requirement a,
            WeekdayInteractionRuleCache.Requirement b)
        {
            if (ReferenceEquals(a, b)) return true;
            if (a == null || b == null) return false;
            return string.Equals(a.ResType, b.ResType, StringComparison.Ordinal) &&
                   string.Equals(a.Id, b.Id, StringComparison.Ordinal) && Math.Abs(a.Value - b.Value) < 0.0001f;
        }

        private static void AddProductionTaskRuleAnswerIds(HashSet<string> destination,
            WeekdayInteractionRuleCache.TargetRules target)
        {
            if (destination == null || target == null) return;
            foreach (var pair in target.OwnerTaskRules) AddSupportedRuleAnswerIds(destination, pair.Value);
            for (var i = 0; i < target.CrossTasks.Count; i++)
                if (target.CrossTasks[i] != null) AddSupportedRuleAnswerIds(destination, target.CrossTasks[i].Rules);
        }

        private static void AddSupportedRuleAnswerIds(HashSet<string> destination,
            List<WeekdayInteractionRuleCache.RuleVariant> variants)
        {
            if (destination == null || variants == null) return;
            for (var i = 0; i < variants.Count; i++)
            {
                var variant = variants[i];
                if (variant == null || variant.Unsupported || string.IsNullOrEmpty(variant.AnswerId)) continue;
                destination.Add(variant.AnswerId);
            }
        }

        private static HashSet<string> FindProductionCompletionAnswerIds(Dictionary<string, Node> nodes,
            Dictionary<string, List<Connection>> incomingFlow, string serialized)
        {
            var ids = new HashSet<string>(StringComparer.Ordinal);
            foreach (var node in nodes.Values)
            {
                if (!node.Type.EndsWith("Flow_SetTaskState", StringComparison.Ordinal)) continue;
                if (!string.Equals(ReadNodeContent(serialized, node, "State"), "Complete", StringComparison.Ordinal)) continue;
                var anchors = FindCompletionAnchors(nodes, incomingFlow, serialized, node.Id, 64);
                for (var i = 0; i < anchors.Count; i++) AddCompletionAnswerIds(ids, anchors[i], serialized, nodes);
            }
            return ids;
        }

        private static List<CompletionAnchor> FindCompletionAnchors(Dictionary<string, Node> nodes,
            Dictionary<string, List<Connection>> incomingFlow, string serialized, string startNodeId, int maxDepth)
        {
            var result = new List<CompletionAnchor>();
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
                    var c = incoming[i];
                    Node source;
                    if (!nodes.TryGetValue(c.SourceNode, out source)) continue;
                    if (source.Type.EndsWith("CustomFunctionEvent", StringComparison.Ordinal))
                    {
                        AddCompletionAnchor(result, new CompletionAnchor { EventIdentifier = ReadNodeIdentifier(serialized, source) });
                        continue;
                    }
                    if (source.Type.EndsWith("Flow_MultiAnswer", StringComparison.Ordinal))
                    {
                        AddCompletionAnchor(result, new CompletionAnchor { MultiNodeId = source.Id, AnswerIndex = ParseOutPortIndex(c.SourcePort) });
                        continue;
                    }
                    if (seen.Add(source.Id)) queue.Enqueue(Tuple.Create(source.Id, current.Item2 + 1));
                }
            }
            return result;
        }

        private static void AddCompletionAnchor(List<CompletionAnchor> list, CompletionAnchor candidate)
        {
            for (var i = 0; i < list.Count; i++)
            {
                var existing = list[i];
                if (existing.EventIdentifier == candidate.EventIdentifier && existing.MultiNodeId == candidate.MultiNodeId &&
                    existing.AnswerIndex == candidate.AnswerIndex) return;
            }
            list.Add(candidate);
        }

        private static void AddCompletionAnswerIds(HashSet<string> ids, CompletionAnchor anchor, string serialized,
            Dictionary<string, Node> nodes)
        {
            if (anchor == null) return;
            if (!string.IsNullOrEmpty(anchor.MultiNodeId) && anchor.AnswerIndex >= 0)
            {
                Node multi;
                if (!nodes.TryGetValue(anchor.MultiNodeId, out multi)) return;
                var answers = ReadMultiAnswers(serialized, multi);
                if (anchor.AnswerIndex < answers.Count && !string.IsNullOrEmpty(answers[anchor.AnswerIndex])) ids.Add(answers[anchor.AnswerIndex]);
                return;
            }
            if (string.IsNullOrEmpty(anchor.EventIdentifier)) return;
            var names = CandidateAnswerNames(anchor.EventIdentifier);
            foreach (var multi in nodes.Values)
            {
                if (!multi.Type.EndsWith("Flow_MultiAnswer", StringComparison.Ordinal)) continue;
                var answers = ReadMultiAnswers(serialized, multi);
                for (var i = 0; i < answers.Count; i++) if (names.Contains(answers[i])) ids.Add(answers[i]);
            }
        }

        private static List<ExactAnchor> FindExactAnswerAnchors(Dictionary<string, Node> nodes,
            Dictionary<string, List<Connection>> incomingFlow, Dictionary<string, List<string>> callsByUid,
            string startNodeId, int maxDepth)
        {
            var result = new List<ExactAnchor>();
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
                    var c = incoming[i];
                    Node source;
                    if (!nodes.TryGetValue(c.SourceNode, out source)) continue;
                    if (source.Type.EndsWith("Flow_MultiAnswer", StringComparison.Ordinal))
                    {
                        AddExactAnchor(result, new ExactAnchor { MultiNodeId = source.Id, AnswerIndex = ParseOutPortIndex(c.SourcePort) });
                        continue;
                    }
                    if (source.Type.EndsWith("CustomFunctionEvent", StringComparison.Ordinal) && !string.IsNullOrEmpty(source.Uid))
                    {
                        List<string> callers;
                        if (callsByUid.TryGetValue(source.Uid, out callers))
                        {
                            for (var j = 0; j < callers.Count; j++)
                                if (seen.Add(callers[j])) queue.Enqueue(Tuple.Create(callers[j], current.Item2 + 1));
                        }
                        continue;
                    }
                    if (seen.Add(source.Id)) queue.Enqueue(Tuple.Create(source.Id, current.Item2 + 1));
                }
            }
            return result;
        }

        private static void AddExactAnchor(List<ExactAnchor> list, ExactAnchor candidate)
        {
            if (candidate == null || string.IsNullOrEmpty(candidate.MultiNodeId) || candidate.AnswerIndex < 0) return;
            for (var i = 0; i < list.Count; i++)
                if (list[i].MultiNodeId == candidate.MultiNodeId && list[i].AnswerIndex == candidate.AnswerIndex) return;
            list.Add(candidate);
        }

        private static Dictionary<string, List<Connection>> BuildIncomingFlow(Dictionary<string, Node> nodes,
            List<Connection> connections, bool includeWaitForFlowNumberedInputs)
        {
            var result = new Dictionary<string, List<Connection>>(StringComparer.Ordinal);
            for (var i = 0; i < connections.Count; i++)
            {
                var c = connections[i];
                var isFlow = string.IsNullOrWhiteSpace(c.TargetPort) || string.Equals(c.TargetPort, "In", StringComparison.OrdinalIgnoreCase);
                if (!isFlow && includeWaitForFlowNumberedInputs)
                {
                    Node target;
                    int ignored;
                    isFlow = nodes.TryGetValue(c.TargetNode, out target) && target.Type.EndsWith("Flow_WaitForFlow", StringComparison.Ordinal) &&
                             int.TryParse(c.TargetPort, NumberStyles.Integer, CultureInfo.InvariantCulture, out ignored);
                }
                if (!isFlow) continue;
                List<Connection> list;
                if (!result.TryGetValue(c.TargetNode, out list)) result[c.TargetNode] = list = new List<Connection>();
                list.Add(c);
            }
            return result;
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

        private static List<Connection> FindAnswerConnections(List<Connection> connections, string multiId, int index)
        {
            var result = new List<Connection>();
            for (var i = 0; i < connections.Count; i++)
                if (connections[i].TargetNode == multiId && ParseAnswerPortIndex(connections[i].TargetPort) == index) result.Add(connections[i]);
            return result;
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

        private static HashSet<string> CandidateAnswerNames(string identifier)
        {
            var result = new HashSet<string>(StringComparer.Ordinal);
            if (string.IsNullOrEmpty(identifier)) return result;
            result.Add(identifier);
            if (identifier.StartsWith("@", StringComparison.Ordinal)) result.Add(identifier.Substring(1));
            else result.Add("@" + identifier);
            return result;
        }

        private static bool IsUtilityLike(string id)
        {
            return string.Equals(id, "Leave", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(id, "Back", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(id, "Trade", StringComparison.OrdinalIgnoreCase);
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

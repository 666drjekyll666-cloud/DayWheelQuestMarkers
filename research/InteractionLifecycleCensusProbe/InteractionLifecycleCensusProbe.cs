using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using BepInEx;
using CalendarQuestsPins;
using UnityEngine;

namespace DayWheelInteractionLifecycleCensus
{
    [BepInPlugin(PluginGuid, PluginName, PluginVersion)]
    public sealed class InteractionLifecycleCensusProbe : BaseUnityPlugin
    {
        private const string PluginGuid = "nikich.gyk.daywheel.interaction-lifecycle-census";
        private const string PluginName = "Day Wheel Quest Markers - interaction lifecycle census";
        private const string PluginVersion = "0.1.0";

        private static readonly string[] NpcIds =
        {
            "npc_astrologer", "npc_inquisitor", "npc_cultist",
            "npc_merchant", "npc_actress", "npc_bishop"
        };

        // Exact answer-backed completion supplements already accepted by production.
        private static readonly Dictionary<string, string[]> SupplementalTaskOwned =
            new Dictionary<string, string[]>(StringComparer.Ordinal)
            {
                { "npc_astrologer", new[] { "@souls_s_s30_ask" } },
                { "npc_cultist", new[] { "@snake_give_key", "@souls_s_s33_ask", "snake_stone_ready" } },
                { "npc_actress", new[] { "@souls_s_s31_ask" } },
                { "npc_bishop", new[] { "@bishop_get_citezen" } }
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

        private sealed class BranchKey : IEquatable<BranchKey>
        {
            internal string AnswerId;
            internal string MultiNodeId;
            internal int AnswerIndex;

            public bool Equals(BranchKey other)
            {
                return other != null &&
                       string.Equals(AnswerId, other.AnswerId, StringComparison.Ordinal) &&
                       string.Equals(MultiNodeId, other.MultiNodeId, StringComparison.Ordinal) &&
                       AnswerIndex == other.AnswerIndex;
            }

            public override bool Equals(object obj) { return Equals(obj as BranchKey); }

            public override int GetHashCode()
            {
                unchecked
                {
                    var hash = 17;
                    hash = hash * 31 + (AnswerId == null ? 0 : AnswerId.GetHashCode());
                    hash = hash * 31 + (MultiNodeId == null ? 0 : MultiNodeId.GetHashCode());
                    hash = hash * 31 + AnswerIndex;
                    return hash;
                }
            }
        }

        private sealed class BranchEffects
        {
            internal BranchKey Key;
            internal readonly HashSet<string> Blacklists = new HashSet<string>(StringComparer.Ordinal);
            internal int MaxFunctionJumps;
        }

        private Type _mainGameType;
        private Type _controllerType;
        private bool _done;
        private float _nextTry;

        private void Awake()
        {
            _mainGameType = ReflectionUtil.FindType("MainGame");
            _controllerType = ReflectionUtil.FindType("FlowCanvas.FlowScriptController");
            _nextTry = Time.realtimeSinceStartup + 1f;
            Logger.LogInfo("LIFECYCLE_CENSUS_PROBE loaded version=" + PluginVersion + " readOnly=True");
        }

        private void Update()
        {
            if (_done || Time.realtimeSinceStartup < _nextTry) return;
            _nextTry = Time.realtimeSinceStartup + 1f;

            var mainGame = ReflectionUtil.FindActiveUnityInstance(_mainGameType);
            if (mainGame == null || _controllerType == null) return;

            object started;
            if (!TryReadStatic(_mainGameType, "game_started", out started) || !(started is bool) || !(bool)started) return;
            object starting;
            if (TryReadStatic(_mainGameType, "game_starting", out starting) && starting is bool && (bool)starting) return;

            object save;
            if (!ReflectionUtil.TryRead(mainGame, "save", out save) || save == null || !HasLoadedKnownNpcs(save)) return;

            try
            {
                RunCensus(save, mainGame);
            }
            catch (Exception ex)
            {
                Logger.LogError("LIFECYCLE_CENSUS_FATAL " + ex);
            }
            _done = true;
        }

        private void RunCensus(object save, object mainGame)
        {
            var rules = new WeekdayInteractionRuleCache();
            if (!rules.Build(save, mainGame))
            {
                Logger.LogError("LIFECYCLE_CENSUS_ABORT production rule cache build failed");
                return;
            }

            var targets = new Dictionary<string, WeekdayInteractionRuleCache.TargetRules>(StringComparer.Ordinal);
            foreach (var target in rules.AllTargets)
                if (target != null && !string.IsNullOrEmpty(target.NpcId)) targets[target.NpcId] = target;

            if (targets.Count != 6)
            {
                Logger.LogError("LIFECYCLE_CENSUS_ABORT expected 6 weekday targets, got " + targets.Count);
                return;
            }

            var navigation = new NavigationReachabilityCache(rules);
            var graphs = new Dictionary<string, string>(StringComparer.Ordinal);

            for (var i = 0; i < NpcIds.Length; i++)
            {
                var npcId = NpcIds[i];
                WeekdayInteractionRuleCache.TargetRules target;
                if (!targets.TryGetValue(npcId, out target))
                {
                    Logger.LogError("LIFECYCLE_CENSUS_ABORT target missing " + npcId);
                    return;
                }

                string serialized;
                if (!TryGetSerializedGraph(target.WorldObject, out serialized))
                {
                    Logger.LogError("LIFECYCLE_CENSUS_ABORT serialized graph missing " + npcId);
                    return;
                }

                string failure;
                if (!navigation.BuildTarget(npcId, serialized, target.WorldObject, target, out failure))
                {
                    Logger.LogError("LIFECYCLE_CENSUS_ABORT navigation build failed " + npcId + " reason=" + failure);
                    return;
                }
                graphs[npcId] = serialized;
            }

            var navTargets = ReadNavigationTargets(navigation);
            if (navTargets == null || navTargets.Count != 6)
            {
                Logger.LogError("LIFECYCLE_CENSUS_ABORT navigation target reflection failed");
                return;
            }

            var totalAnswers = 0;
            var totalBranchesWithBlacklist = 0;
            var totalSelfPathOwners = 0;
            var totalAncestorPathOwners = 0;
            var totalTaskOwnedSuppressed = 0;
            var totalNonIndependentSuppressed = 0;
            var totalReversibleSuppressed = 0;
            var totalAdmittedSelf = 0;
            var totalAdmittedAncestor = 0;
            var uniqueOwners = new HashSet<string>(StringComparer.Ordinal);

            Logger.LogInfo("LIFECYCLE_CENSUS_BEGIN game=1.407 npcs=6 policy=path-local nearest persistent self-or-ancestor owner; readOnly=True");

            for (var i = 0; i < NpcIds.Length; i++)
            {
                var npcId = NpcIds[i];
                var serialized = graphs[npcId];
                var target = targets[npcId];
                var navTarget = navTargets[npcId];

                var nodes = BuildNodeIndex(serialized);
                var connections = ParseConnections(serialized);
                var incoming = BuildIncomingFlow(nodes, connections);
                var callsByUid = BuildCallsByUid(nodes);
                var removals = FindBlacklistRemovals(nodes, serialized);
                var branches = BuildBranchEffects(nodes, incoming, callsByUid, serialized);
                var taskOwned = CollectTaskOwned(target, npcId);

                var answerOccurrences = 0;
                foreach (var node in nodes.Values)
                    if (node.Type.EndsWith("Flow_MultiAnswer", StringComparison.Ordinal))
                        answerOccurrences += ReadMultiAnswers(serialized, node).Count;
                totalAnswers += answerOccurrences;
                totalBranchesWithBlacklist += branches.Count;

                var npcSelf = 0;
                var npcAncestor = 0;
                var npcAdmittedSelf = 0;
                var npcAdmittedAncestor = 0;
                var npcTaskSuppressed = 0;
                var npcNonIndependent = 0;
                var npcReversible = 0;

                var sorted = new List<BranchEffects>(branches.Values);
                sorted.Sort(CompareBranchEffects);
                for (var b = 0; b < sorted.Count; b++)
                {
                    var branch = sorted[b];
                    List<NavigationReachabilityCache.NavigationPath> paths;
                    if (!navTarget.PathsByAnswer.TryGetValue(branch.Key.AnswerId, out paths) || paths == null || paths.Count == 0)
                    {
                        continue; // no interaction-root path: not a player-selectable reminder candidate
                    }

                    for (var p = 0; p < paths.Count; p++)
                    {
                        var path = paths[p];
                        if (path == null) continue;

                        string owner = null;
                        var ownerKind = "none";

                        if (branch.Blacklists.Contains(branch.Key.AnswerId))
                        {
                            owner = branch.Key.AnswerId;
                            ownerKind = "self";
                        }
                        else
                        {
                            for (var a = path.Ancestors.Count - 1; a >= 0; a--)
                            {
                                var ancestor = path.Ancestors[a];
                                if (ancestor == null || string.IsNullOrEmpty(ancestor.AnswerId)) continue;
                                if (!branch.Blacklists.Contains(ancestor.AnswerId)) continue;
                                owner = ancestor.AnswerId;
                                ownerKind = "ancestor";
                                break;
                            }
                        }

                        if (string.IsNullOrEmpty(owner)) continue;

                        if (ownerKind == "self") { npcSelf++; totalSelfPathOwners++; }
                        else { npcAncestor++; totalAncestorPathOwners++; }

                        var reversible = removals.Contains(owner);
                        var taskSuppressed = taskOwned.Contains(owner);
                        var independent = HasIndependentOwnerRootPath(navTarget, owner, taskOwned);

                        var disposition = "ADMIT";
                        if (reversible)
                        {
                            disposition = "SUPPRESS_REVERSIBLE";
                            npcReversible++;
                            totalReversibleSuppressed++;
                        }
                        else if (taskSuppressed)
                        {
                            disposition = "SUPPRESS_TASK_OWNED";
                            npcTaskSuppressed++;
                            totalTaskOwnedSuppressed++;
                        }
                        else if (!independent)
                        {
                            disposition = "SUPPRESS_SAME_VISIT";
                            npcNonIndependent++;
                            totalNonIndependentSuppressed++;
                        }
                        else
                        {
                            if (ownerKind == "self") { npcAdmittedSelf++; totalAdmittedSelf++; }
                            else { npcAdmittedAncestor++; totalAdmittedAncestor++; }
                            uniqueOwners.Add(npcId + "\n" + owner);
                        }

                        Logger.LogInfo(
                            "LIFECYCLE_PATH npc=" + npcId +
                            " branch=" + EscapeLog(branch.Key.AnswerId) +
                            " multi=" + branch.Key.MultiNodeId +
                            " index=" + branch.Key.AnswerIndex +
                            " path=" + DescribePath(path, branch.Key.AnswerId) +
                            " blacklists=" + JoinSorted(branch.Blacklists) +
                            " owner=" + EscapeLog(owner) +
                            " ownerKind=" + ownerKind +
                            " taskOwned=" + taskSuppressed +
                            " independentOwnerRoot=" + independent +
                            " reversible=" + reversible +
                            " jumps=" + branch.MaxFunctionJumps +
                            " disposition=" + disposition);
                    }
                }

                Logger.LogInfo(
                    "LIFECYCLE_NPC npc=" + npcId +
                    " answers=" + answerOccurrences +
                    " branchesWithBlacklist=" + branches.Count +
                    " selfPathOwners=" + npcSelf +
                    " ancestorPathOwners=" + npcAncestor +
                    " admittedSelfPaths=" + npcAdmittedSelf +
                    " admittedAncestorPaths=" + npcAdmittedAncestor +
                    " taskOwnedSuppressedPaths=" + npcTaskSuppressed +
                    " sameVisitSuppressedPaths=" + npcNonIndependent +
                    " reversibleSuppressedPaths=" + npcReversible);
            }

            Logger.LogInfo(
                "LIFECYCLE_CENSUS_SUMMARY graphs=" + graphs.Count + "/6" +
                " answerOccurrences=" + totalAnswers +
                " branchesWithBlacklist=" + totalBranchesWithBlacklist +
                " selfPathOwners=" + totalSelfPathOwners +
                " ancestorPathOwners=" + totalAncestorPathOwners +
                " admittedSelfPaths=" + totalAdmittedSelf +
                " admittedAncestorPaths=" + totalAdmittedAncestor +
                " taskOwnedSuppressedPaths=" + totalTaskOwnedSuppressed +
                " sameVisitSuppressedPaths=" + totalNonIndependentSuppressed +
                " reversibleSuppressedPaths=" + totalReversibleSuppressed +
                " uniqueAdmittedOwners=" + uniqueOwners.Count);

            Logger.LogInfo("LIFECYCLE_EXPECT npc=npc_cultist owner=@snake_1с expected=ADMIT_ANCESTOR");
            Logger.LogInfo("LIFECYCLE_EXPECT npc=npc_merchant owner=@merchant_2b expected=SUPPRESS_TASK_OWNED_IF_PATH_LOCAL");
            Logger.LogInfo("LIFECYCLE_EXPECT npc=npc_merchant owner=@merchant_2e_1e expected=EVIDENCE_DECIDES_PATH_LOCALITY");
            Logger.LogInfo("LIFECYCLE_CENSUS_END");
        }

        private bool TryGetSerializedGraph(object worldObject, out string serialized)
        {
            serialized = null;
            var component = worldObject as Component;
            if (component == null || _controllerType == null) return false;
            var controller = component.GetComponent(_controllerType);
            if (controller == null) return false;
            object graph;
            if (!ReflectionUtil.TryRead(controller, "_graph", out graph) || graph == null) return false;
            object value;
            if (!ReflectionUtil.TryRead(graph, "_serializedGraph", out value)) return false;
            serialized = value as string;
            return !string.IsNullOrEmpty(serialized);
        }

        private static Dictionary<string, NavigationReachabilityCache.TargetNavigation> ReadNavigationTargets(
            NavigationReachabilityCache navigation)
        {
            try
            {
                var field = typeof(NavigationReachabilityCache).GetField(
                    "_targets", BindingFlags.Instance | BindingFlags.NonPublic);
                return field == null
                    ? null
                    : field.GetValue(navigation) as Dictionary<string, NavigationReachabilityCache.TargetNavigation>;
            }
            catch { return null; }
        }

        private static HashSet<string> CollectTaskOwned(WeekdayInteractionRuleCache.TargetRules target, string npcId)
        {
            var result = new HashSet<string>(StringComparer.Ordinal);
            if (target != null)
            {
                foreach (var pair in target.OwnerTaskRules)
                    AddRuleAnswers(result, pair.Value);

                for (var i = 0; i < target.CrossTasks.Count; i++)
                    if (target.CrossTasks[i] != null) AddRuleAnswers(result, target.CrossTasks[i].Rules);
            }

            string[] supplemental;
            if (SupplementalTaskOwned.TryGetValue(npcId, out supplemental))
                for (var i = 0; i < supplemental.Length; i++) result.Add(supplemental[i]);
            return result;
        }

        private static void AddRuleAnswers(HashSet<string> set, List<WeekdayInteractionRuleCache.RuleVariant> variants)
        {
            if (variants == null) return;
            for (var i = 0; i < variants.Count; i++)
            {
                var variant = variants[i];
                if (variant != null && !string.IsNullOrEmpty(variant.AnswerId)) set.Add(variant.AnswerId);
            }
        }

        private static bool HasIndependentOwnerRootPath(
            NavigationReachabilityCache.TargetNavigation navTarget, string owner, HashSet<string> taskOwned)
        {
            List<NavigationReachabilityCache.NavigationPath> paths;
            if (navTarget == null || string.IsNullOrEmpty(owner) ||
                !navTarget.PathsByAnswer.TryGetValue(owner, out paths) || paths == null || paths.Count == 0)
                return false;

            for (var p = 0; p < paths.Count; p++)
            {
                var path = paths[p];
                if (path == null) continue;
                var blocked = false;
                for (var a = 0; a < path.Ancestors.Count; a++)
                {
                    var ancestor = path.Ancestors[a];
                    if (ancestor != null && !string.IsNullOrEmpty(ancestor.AnswerId) &&
                        taskOwned.Contains(ancestor.AnswerId))
                    {
                        blocked = true;
                        break;
                    }
                }
                if (!blocked) return true;
            }
            return false;
        }

        private static string DescribePath(NavigationReachabilityCache.NavigationPath path, string answerId)
        {
            var sb = new StringBuilder();
            for (var i = 0; i < path.Ancestors.Count; i++)
            {
                var id = path.Ancestors[i] == null ? null : path.Ancestors[i].AnswerId;
                if (string.IsNullOrEmpty(id)) continue;
                if (sb.Length > 0) sb.Append(">");
                sb.Append(EscapeLog(id));
            }
            if (sb.Length > 0) sb.Append(">");
            sb.Append(EscapeLog(answerId));
            return sb.ToString();
        }

        private static Dictionary<BranchKey, BranchEffects> BuildBranchEffects(
            Dictionary<string, Node> nodes,
            Dictionary<string, List<Connection>> incomingFlow,
            Dictionary<string, List<string>> callsByUid,
            string serialized)
        {
            var result = new Dictionary<BranchKey, BranchEffects>();

            foreach (var node in nodes.Values)
            {
                string blacklistedId;
                if (!TryReadBlacklistAdd(node, serialized, out blacklistedId) || string.IsNullOrEmpty(blacklistedId))
                    continue;

                var anchors = FindExactAnswerAnchors(nodes, incomingFlow, callsByUid, node.Id, 96);
                for (var a = 0; a < anchors.Count; a++)
                {
                    var anchor = anchors[a];
                    Node multi;
                    if (!nodes.TryGetValue(anchor.MultiNodeId, out multi)) continue;
                    var answers = ReadMultiAnswers(serialized, multi);
                    if (anchor.AnswerIndex < 0 || anchor.AnswerIndex >= answers.Count) continue;
                    var answerId = answers[anchor.AnswerIndex];
                    if (string.IsNullOrEmpty(answerId)) continue;

                    var key = new BranchKey
                    {
                        AnswerId = answerId,
                        MultiNodeId = anchor.MultiNodeId,
                        AnswerIndex = anchor.AnswerIndex
                    };
                    BranchEffects effects;
                    if (!result.TryGetValue(key, out effects))
                    {
                        effects = new BranchEffects { Key = key };
                        result.Add(key, effects);
                    }
                    effects.Blacklists.Add(blacklistedId);
                    if (anchor.FunctionJumps > effects.MaxFunctionJumps)
                        effects.MaxFunctionJumps = anchor.FunctionJumps;
                }
            }
            return result;
        }

        private static int CompareBranchEffects(BranchEffects a, BranchEffects b)
        {
            var c = string.CompareOrdinal(a.Key.AnswerId, b.Key.AnswerId);
            if (c != 0) return c;
            c = string.CompareOrdinal(a.Key.MultiNodeId, b.Key.MultiNodeId);
            return c != 0 ? c : a.Key.AnswerIndex.CompareTo(b.Key.AnswerIndex);
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

        private static Dictionary<string, List<Connection>> BuildIncomingFlow(
            Dictionary<string, Node> nodes, List<Connection> connections)
        {
            var result = new Dictionary<string, List<Connection>>(StringComparer.Ordinal);
            for (var i = 0; i < connections.Count; i++)
            {
                var c = connections[i];
                if (!IsFlowConnection(nodes, c)) continue;
                List<Connection> list;
                if (!result.TryGetValue(c.TargetNode, out list))
                    result[c.TargetNode] = list = new List<Connection>();
                list.Add(c);
            }
            return result;
        }

        private static bool IsFlowConnection(Dictionary<string, Node> nodes, Connection c)
        {
            if (c == null) return false;
            if (string.IsNullOrWhiteSpace(c.TargetPort) ||
                string.Equals(c.TargetPort, "In", StringComparison.OrdinalIgnoreCase)) return true;

            Node target;
            if (!nodes.TryGetValue(c.TargetNode, out target) ||
                !target.Type.EndsWith("Flow_WaitForFlow", StringComparison.Ordinal)) return false;

            int ignored;
            return int.TryParse(c.TargetPort, NumberStyles.Integer, CultureInfo.InvariantCulture, out ignored);
        }

        private static Dictionary<string, List<string>> BuildCallsByUid(Dictionary<string, Node> nodes)
        {
            var result = new Dictionary<string, List<string>>(StringComparer.Ordinal);
            foreach (var node in nodes.Values)
            {
                if (!node.Type.EndsWith("CustomFunctionCall", StringComparison.Ordinal) ||
                    string.IsNullOrEmpty(node.SourceOutputUid)) continue;
                List<string> list;
                if (!result.TryGetValue(node.SourceOutputUid, out list))
                    result[node.SourceOutputUid] = list = new List<string>();
                list.Add(node.Id);
            }
            return result;
        }

        private static List<Anchor> FindExactAnswerAnchors(
            Dictionary<string, Node> nodes,
            Dictionary<string, List<Connection>> incomingFlow,
            Dictionary<string, List<string>> callsByUid,
            string startNodeId, int maxDepth)
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

                    if (source.Type.EndsWith("CustomFunctionEvent", StringComparison.Ordinal) &&
                        !string.IsNullOrEmpty(source.Uid))
                    {
                        List<string> callers;
                        if (callsByUid.TryGetValue(source.Uid, out callers))
                        {
                            for (var j = 0; j < callers.Count; j++)
                            {
                                var callId = callers[j];
                                if (seen.Add(callId))
                                    queue.Enqueue(Tuple.Create(callId, current.Item2 + 1, current.Item3 + 1));
                            }
                        }
                        continue;
                    }

                    if (seen.Add(source.Id))
                        queue.Enqueue(Tuple.Create(source.Id, current.Item2 + 1, current.Item3));
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
                if (a.MultiNodeId != candidate.MultiNodeId || a.AnswerIndex != candidate.AnswerIndex) continue;
                if (candidate.FunctionJumps < a.FunctionJumps) a.FunctionJumps = candidate.FunctionJumps;
                return;
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
                var id = ReadNodeContent(serialized, node, "Phrase ID") ??
                         ReadNodeContent(serialized, node, "in_phrase");
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
                phraseId = ReadNodeContent(serialized, node, "phrase") ??
                           ReadNodeContent(serialized, node, "Phrase");
                return !string.IsNullOrEmpty(phraseId);
            }

            if (!node.Type.EndsWith("Flow_AddPhraseToBlacklist", StringComparison.Ordinal)) return false;
            bool remove;
            if (TryReadNodeBool(serialized, node, "remove", out remove) && remove) return false;
            phraseId = ReadNodeContent(serialized, node, "Phrase ID") ??
                       ReadNodeContent(serialized, node, "in_phrase");
            return !string.IsNullOrEmpty(phraseId);
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

        private static int ParseOutPortIndex(string port)
        {
            const string prefix = "out_";
            int value;
            return port != null && port.StartsWith(prefix, StringComparison.Ordinal) &&
                   int.TryParse(port.Substring(prefix.Length), out value)
                ? value
                : -1;
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
                if (prop != null && prop.GetIndexParameters().Length == 0)
                {
                    value = prop.GetValue(null, null);
                    return true;
                }
            }
            catch { }
            return false;
        }

        private static string JoinSorted(HashSet<string> values)
        {
            var list = new List<string>(values);
            list.Sort(StringComparer.Ordinal);
            for (var i = 0; i < list.Count; i++) list[i] = EscapeLog(list[i]);
            return string.Join(",", list.ToArray());
        }

        private static string EscapeLog(string value)
        {
            return value == null ? "<null>" : value.Replace(" ", "_");
        }
    }
}

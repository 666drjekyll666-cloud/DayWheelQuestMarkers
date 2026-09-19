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

namespace DayWheelInteractionLifecycleAudit
{
    [BepInPlugin(PluginGuid, PluginName, PluginVersion)]
    public sealed class InteractionLifecycleAuditProbe : BaseUnityPlugin
    {
        private const string PluginGuid = "nikich.gyk.daywheel.interaction-lifecycle-audit";
        private const string PluginName = "Day Wheel Quest Markers - interaction lifecycle audit";
        private const string PluginVersion = "0.1.0";

        private static readonly string[] NpcIds =
        {
            "npc_astrologer", "npc_inquisitor", "npc_cultist",
            "npc_merchant", "npc_actress", "npc_bishop"
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

        private sealed class AnswerRef
        {
            internal string MenuId;
            internal int Index;
            internal string AnswerId;

            internal string Key
            {
                get { return MenuId + "#" + Index.ToString(CultureInfo.InvariantCulture); }
            }
        }

        private sealed class Menu
        {
            internal string NodeId;
            internal readonly List<string> Answers = new List<string>();
            internal readonly List<List<string>> NextMenus = new List<List<string>>();
        }

        private sealed class AnswerPath
        {
            internal readonly List<AnswerRef> Ancestors = new List<AnswerRef>();
        }

        private sealed class Anchor
        {
            internal string MultiNodeId;
            internal int AnswerIndex;
            internal int FunctionJumps;
        }

        private sealed class BlacklistRelation
        {
            internal string NpcId;
            internal AnswerRef Consumer;
            internal AnswerRef Owner;
            internal string BlacklistedId;
            internal bool Self;
            internal int OwnerDepth;
            internal int MatchingPathCount;
            internal int TotalConsumerPathCount;
            internal int IndependentPathCount;
            internal bool Reversible;
            internal string OwnerGate;
            internal string ConsumerTaskEffects;
            internal string OwnerTaskEffects;
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
            Logger.LogInfo("LIFECYCLE_AUDIT_PROBE loaded version=" + PluginVersion + " readOnly=True");
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
                RunAudit();
                _done = true;
            }
            catch (Exception ex)
            {
                Logger.LogError("LIFECYCLE_AUDIT_FATAL " + ex);
                _done = true;
            }
        }

        private void RunAudit()
        {
            Logger.LogInfo("LIFECYCLE_AUDIT_BEGIN game=1.407 npcs=6 policy=all authored answer occurrences; exact root paths; persistent self-or-ancestor blacklist; readOnly=True");

            var loaded = 0;
            var totalMenus = 0;
            var totalAnswerOccurrences = 0;
            var totalPaths = 0;
            var totalBlacklistAnchors = 0;
            var totalSelfRelations = 0;
            var totalAncestorRelations = 0;
            var totalIndependentAncestorRelations = 0;
            var totalReversibleRelations = 0;
            var allRelations = new List<BlacklistRelation>();

            for (var n = 0; n < NpcIds.Length; n++)
            {
                var npcId = NpcIds[n];
                string serialized;
                if (!TryGetSerializedGraph(npcId, out serialized))
                {
                    Logger.LogWarning("LIFECYCLE_GRAPH npc=" + npcId + " loaded=False");
                    continue;
                }

                loaded++;
                var nodes = BuildNodeIndex(serialized);
                var connections = ParseConnections(serialized);
                var outgoingFlow = BuildFlowConnectionMap(nodes, connections, true);
                var incomingFlow = BuildFlowConnectionMap(nodes, connections, false);
                var callsByUid = BuildCallsByUid(nodes);
                var functionEvents = BuildFunctionEventMap(nodes);
                var menus = BuildMenus(nodes, outgoingFlow, functionEvents, serialized);
                var roots = FindInteractionRoots(nodes, outgoingFlow, functionEvents, serialized, menus);
                var paths = new Dictionary<string, List<AnswerPath>>(StringComparer.Ordinal);
                var occurrenceByKey = new Dictionary<string, AnswerRef>(StringComparer.Ordinal);

                for (var r = 0; r < roots.Count; r++)
                {
                    BuildPathsDepthFirst(roots[r], menus, paths, occurrenceByKey,
                        new List<AnswerRef>(), new HashSet<string>(StringComparer.Ordinal), 0);
                }

                var taskEffects = BuildTaskEffects(nodes, incomingFlow, callsByUid, serialized);
                var removals = FindBlacklistRemovals(nodes, serialized);
                var npcRelations = new List<BlacklistRelation>();
                var blacklistAnchorCount = 0;

                foreach (var node in nodes.Values)
                {
                    string blacklistedId;
                    if (!TryReadBlacklistAdd(node, serialized, out blacklistedId) || string.IsNullOrEmpty(blacklistedId)) continue;
                    var anchors = FindExactAnswerAnchors(nodes, incomingFlow, callsByUid, node.Id, 96);
                    for (var a = 0; a < anchors.Count; a++)
                    {
                        var anchor = anchors[a];
                        AnswerRef consumer;
                        if (!TryResolveAnswerRef(anchor, menus, out consumer)) continue;
                        blacklistAnchorCount++;

                        List<AnswerPath> consumerPaths;
                        paths.TryGetValue(consumer.Key, out consumerPaths);
                        if (consumerPaths == null || consumerPaths.Count == 0) continue;

                        if (string.Equals(consumer.AnswerId, blacklistedId, StringComparison.Ordinal))
                        {
                            var independent = CountIndependentPaths(consumerPaths, taskEffects, null);
                            AddRelation(npcRelations, new BlacklistRelation
                            {
                                NpcId = npcId,
                                Consumer = consumer,
                                Owner = consumer,
                                BlacklistedId = blacklistedId,
                                Self = true,
                                OwnerDepth = 0,
                                MatchingPathCount = consumerPaths.Count,
                                TotalConsumerPathCount = consumerPaths.Count,
                                IndependentPathCount = independent,
                                Reversible = removals.Contains(blacklistedId),
                                OwnerGate = DescribeGate(serialized, nodes, connections, consumer.MenuId, consumer.Index),
                                ConsumerTaskEffects = DescribeTaskEffects(taskEffects, consumer.Key),
                                OwnerTaskEffects = DescribeTaskEffects(taskEffects, consumer.Key)
                            });
                            continue;
                        }

                        var ownerMatches = new Dictionary<string, Tuple<AnswerRef, int, int>>(StringComparer.Ordinal);
                        for (var p = 0; p < consumerPaths.Count; p++)
                        {
                            var path = consumerPaths[p];
                            AnswerRef owner = null;
                            var ownerIndex = -1;
                            for (var i = path.Ancestors.Count - 1; i >= 0; i--)
                            {
                                if (!string.Equals(path.Ancestors[i].AnswerId, blacklistedId, StringComparison.Ordinal)) continue;
                                owner = path.Ancestors[i];
                                ownerIndex = i;
                                break;
                            }
                            if (owner == null) continue;

                            Tuple<AnswerRef, int, int> existing;
                            if (!ownerMatches.TryGetValue(owner.Key, out existing))
                                ownerMatches[owner.Key] = Tuple.Create(owner, 1, ownerIndex);
                            else
                                ownerMatches[owner.Key] = Tuple.Create(existing.Item1, existing.Item2 + 1,
                                    Math.Max(existing.Item3, ownerIndex));
                        }

                        foreach (var pair in ownerMatches)
                        {
                            var owner = pair.Value.Item1;
                            var matched = pair.Value.Item2;
                            var independent = CountIndependentPathsForOwner(consumerPaths, owner, blacklistedId, taskEffects);
                            AddRelation(npcRelations, new BlacklistRelation
                            {
                                NpcId = npcId,
                                Consumer = consumer,
                                Owner = owner,
                                BlacklistedId = blacklistedId,
                                Self = false,
                                OwnerDepth = FindMinimumOwnerDistance(consumerPaths, owner, blacklistedId),
                                MatchingPathCount = matched,
                                TotalConsumerPathCount = consumerPaths.Count,
                                IndependentPathCount = independent,
                                Reversible = removals.Contains(blacklistedId),
                                OwnerGate = DescribeGate(serialized, nodes, connections, owner.MenuId, owner.Index),
                                ConsumerTaskEffects = DescribeTaskEffects(taskEffects, consumer.Key),
                                OwnerTaskEffects = DescribeTaskEffects(taskEffects, owner.Key)
                            });
                        }
                    }
                }

                npcRelations.Sort(CompareRelations);
                for (var i = 0; i < npcRelations.Count; i++)
                {
                    var rel = npcRelations[i];
                    if (rel.Self) totalSelfRelations++; else
                    {
                        totalAncestorRelations++;
                        if (rel.IndependentPathCount > 0) totalIndependentAncestorRelations++;
                    }
                    if (rel.Reversible) totalReversibleRelations++;
                    LogRelation(rel);
                    allRelations.Add(rel);
                }

                var answerOccurrences = 0;
                foreach (var menu in menus.Values) answerOccurrences += menu.Answers.Count;
                var pathCount = 0;
                foreach (var list in paths.Values) pathCount += list.Count;

                totalMenus += menus.Count;
                totalAnswerOccurrences += answerOccurrences;
                totalPaths += pathCount;
                totalBlacklistAnchors += blacklistAnchorCount;

                Logger.LogInfo("LIFECYCLE_NPC npc=" + npcId +
                               " nodes=" + nodes.Count +
                               " connections=" + connections.Count +
                               " menus=" + menus.Count +
                               " roots=" + roots.Count +
                               " answerOccurrences=" + answerOccurrences +
                               " rootedOccurrences=" + paths.Count +
                               " rootPaths=" + pathCount +
                               " blacklistAnchors=" + blacklistAnchorCount +
                               " selfRelations=" + CountRelations(npcRelations, true) +
                               " ancestorRelations=" + CountRelations(npcRelations, false) +
                               " independentAncestorRelations=" + CountIndependentAncestorRelations(npcRelations));
            }

            Logger.LogInfo("LIFECYCLE_AUDIT_SUMMARY graphs=" + loaded + "/6" +
                           " menus=" + totalMenus +
                           " answerOccurrences=" + totalAnswerOccurrences +
                           " rootPaths=" + totalPaths +
                           " blacklistAnchors=" + totalBlacklistAnchors +
                           " selfRelations=" + totalSelfRelations +
                           " ancestorRelations=" + totalAncestorRelations +
                           " independentAncestorRelations=" + totalIndependentAncestorRelations +
                           " reversibleRelations=" + totalReversibleRelations);

            LogKnownCheck(allRelations, "npc_cultist", "snake_1с_4a", "@snake_1с");
            LogKnownCheck(allRelations, "npc_cultist", "snake_1с_4b", "@snake_1с");
            LogKnownCheck(allRelations, "npc_merchant", "merchant_2e_1d_4a", "@merchant_2e_1e");
            LogKnownCheck(allRelations, "npc_merchant", "merchant_2b_5b_2b_4a", "@merchant_2b");
            Logger.LogInfo("LIFECYCLE_AUDIT_END");
        }

        private void LogRelation(BlacklistRelation r)
        {
            Logger.LogInfo("LIFECYCLE_REL npc=" + r.NpcId +
                           " scope=" + (r.Self ? "SELF" : "ANCESTOR") +
                           " owner=" + EscapeLog(r.Owner.AnswerId) +
                           " ownerOccurrence=" + r.Owner.Key +
                           " consumer=" + EscapeLog(r.Consumer.AnswerId) +
                           " consumerOccurrence=" + r.Consumer.Key +
                           " depth=" + r.OwnerDepth +
                           " paths=" + r.MatchingPathCount + "/" + r.TotalConsumerPathCount +
                           " independentPaths=" + r.IndependentPathCount +
                           " reversible=" + r.Reversible +
                           " ownerGate=" + r.OwnerGate +
                           " ownerTask=" + r.OwnerTaskEffects +
                           " consumerTask=" + r.ConsumerTaskEffects);
        }

        private void LogKnownCheck(List<BlacklistRelation> relations, string npcId, string consumerId, string ownerId)
        {
            var found = false;
            for (var i = 0; i < relations.Count; i++)
            {
                var r = relations[i];
                if (r.NpcId == npcId && !r.Self &&
                    r.Consumer.AnswerId == consumerId && r.Owner.AnswerId == ownerId)
                {
                    found = true;
                    break;
                }
            }
            Logger.LogInfo("LIFECYCLE_KNOWN npc=" + npcId +
                           " consumer=" + EscapeLog(consumerId) +
                           " owner=" + EscapeLog(ownerId) +
                           " found=" + found);
        }

        private static void AddRelation(List<BlacklistRelation> list, BlacklistRelation candidate)
        {
            for (var i = 0; i < list.Count; i++)
            {
                var r = list[i];
                if (r.Consumer.Key == candidate.Consumer.Key && r.Owner.Key == candidate.Owner.Key &&
                    r.BlacklistedId == candidate.BlacklistedId && r.Self == candidate.Self)
                {
                    r.MatchingPathCount = Math.Max(r.MatchingPathCount, candidate.MatchingPathCount);
                    r.TotalConsumerPathCount = Math.Max(r.TotalConsumerPathCount, candidate.TotalConsumerPathCount);
                    r.IndependentPathCount = Math.Max(r.IndependentPathCount, candidate.IndependentPathCount);
                    return;
                }
            }
            list.Add(candidate);
        }

        private static int CompareRelations(BlacklistRelation a, BlacklistRelation b)
        {
            var c = string.CompareOrdinal(a.Owner.AnswerId, b.Owner.AnswerId);
            if (c != 0) return c;
            c = string.CompareOrdinal(a.Consumer.AnswerId, b.Consumer.AnswerId);
            if (c != 0) return c;
            c = string.CompareOrdinal(a.Owner.Key, b.Owner.Key);
            if (c != 0) return c;
            return string.CompareOrdinal(a.Consumer.Key, b.Consumer.Key);
        }

        private static int CountRelations(List<BlacklistRelation> list, bool self)
        {
            var count = 0;
            for (var i = 0; i < list.Count; i++) if (list[i].Self == self) count++;
            return count;
        }

        private static int CountIndependentAncestorRelations(List<BlacklistRelation> list)
        {
            var count = 0;
            for (var i = 0; i < list.Count; i++)
                if (!list[i].Self && list[i].IndependentPathCount > 0) count++;
            return count;
        }

        private static int CountIndependentPaths(List<AnswerPath> paths,
            Dictionary<string, HashSet<string>> taskEffects, AnswerRef stopOwner)
        {
            var count = 0;
            for (var p = 0; p < paths.Count; p++)
            {
                var blocked = false;
                var path = paths[p];
                for (var i = 0; i < path.Ancestors.Count; i++)
                {
                    var a = path.Ancestors[i];
                    if (taskEffects.ContainsKey(a.Key)) { blocked = true; break; }
                    if (stopOwner != null && a.Key == stopOwner.Key) break;
                }
                if (!blocked) count++;
            }
            return count;
        }

        private static int CountIndependentPathsForOwner(List<AnswerPath> paths, AnswerRef owner,
            string blacklistedId, Dictionary<string, HashSet<string>> taskEffects)
        {
            var count = 0;
            for (var p = 0; p < paths.Count; p++)
            {
                var path = paths[p];
                var ownerIndex = -1;
                for (var i = path.Ancestors.Count - 1; i >= 0; i--)
                {
                    if (path.Ancestors[i].Key == owner.Key &&
                        string.Equals(path.Ancestors[i].AnswerId, blacklistedId, StringComparison.Ordinal))
                    {
                        ownerIndex = i;
                        break;
                    }
                }
                if (ownerIndex < 0) continue;

                var blocked = taskEffects.ContainsKey(owner.Key);
                for (var i = 0; i < ownerIndex && !blocked; i++)
                    if (taskEffects.ContainsKey(path.Ancestors[i].Key)) blocked = true;
                if (!blocked) count++;
            }
            return count;
        }

        private static int FindMinimumOwnerDistance(List<AnswerPath> paths, AnswerRef owner, string blacklistedId)
        {
            var best = int.MaxValue;
            for (var p = 0; p < paths.Count; p++)
            {
                var path = paths[p];
                for (var i = path.Ancestors.Count - 1; i >= 0; i--)
                {
                    if (path.Ancestors[i].Key != owner.Key ||
                        !string.Equals(path.Ancestors[i].AnswerId, blacklistedId, StringComparison.Ordinal)) continue;
                    var distance = path.Ancestors.Count - i;
                    if (distance < best) best = distance;
                    break;
                }
            }
            return best == int.MaxValue ? -1 : best;
        }

        private static string DescribeTaskEffects(Dictionary<string, HashSet<string>> taskEffects, string occurrenceKey)
        {
            HashSet<string> effects;
            if (!taskEffects.TryGetValue(occurrenceKey, out effects) || effects.Count == 0) return "<none>";
            var list = new List<string>(effects);
            list.Sort(StringComparer.Ordinal);
            return string.Join("|", list.ToArray());
        }

        private static Dictionary<string, HashSet<string>> BuildTaskEffects(Dictionary<string, Node> nodes,
            Dictionary<string, List<Connection>> incomingFlow, Dictionary<string, List<string>> callsByUid,
            string serialized)
        {
            var result = new Dictionary<string, HashSet<string>>(StringComparer.Ordinal);
            foreach (var node in nodes.Values)
            {
                if (!node.Type.EndsWith("Flow_SetTaskState", StringComparison.Ordinal)) continue;
                var state = ReadNodeContent(serialized, node, "State") ?? "?";
                var task = ReadNodeContent(serialized, node, "Task") ?? "?";
                var owner = ReadNodeContent(serialized, node, "NPC id") ?? "<local>";
                var effect = owner + "/" + task + ":" + state;
                var anchors = FindExactAnswerAnchors(nodes, incomingFlow, callsByUid, node.Id, 96);
                for (var i = 0; i < anchors.Count; i++)
                {
                    var key = anchors[i].MultiNodeId + "#" +
                              anchors[i].AnswerIndex.ToString(CultureInfo.InvariantCulture);
                    HashSet<string> effects;
                    if (!result.TryGetValue(key, out effects))
                        result[key] = effects = new HashSet<string>(StringComparer.Ordinal);
                    effects.Add(effect);
                }
            }
            return result;
        }

        private static bool TryResolveAnswerRef(Anchor anchor, Dictionary<string, Menu> menus, out AnswerRef answer)
        {
            answer = null;
            if (anchor == null) return false;
            Menu menu;
            if (!menus.TryGetValue(anchor.MultiNodeId, out menu) ||
                anchor.AnswerIndex < 0 || anchor.AnswerIndex >= menu.Answers.Count) return false;
            answer = new AnswerRef
            {
                MenuId = anchor.MultiNodeId,
                Index = anchor.AnswerIndex,
                AnswerId = menu.Answers[anchor.AnswerIndex]
            };
            return !string.IsNullOrEmpty(answer.AnswerId);
        }

        private static void BuildPathsDepthFirst(string menuId, Dictionary<string, Menu> menus,
            Dictionary<string, List<AnswerPath>> paths, Dictionary<string, AnswerRef> occurrenceByKey,
            List<AnswerRef> ancestors, HashSet<string> visitedMenus, int depth)
        {
            if (depth > 24 || !visitedMenus.Add(menuId)) return;
            Menu menu;
            if (!menus.TryGetValue(menuId, out menu))
            {
                visitedMenus.Remove(menuId);
                return;
            }

            for (var i = 0; i < menu.Answers.Count; i++)
            {
                var answerId = menu.Answers[i];
                if (string.IsNullOrEmpty(answerId)) continue;
                var answer = new AnswerRef { MenuId = menu.NodeId, Index = i, AnswerId = answerId };
                occurrenceByKey[answer.Key] = answer;
                AddAnswerPath(paths, answer.Key, ancestors);

                if (i >= menu.NextMenus.Count || menu.NextMenus[i] == null || menu.NextMenus[i].Count == 0) continue;
                var nextAncestors = new List<AnswerRef>(ancestors.Count + 1);
                for (var a = 0; a < ancestors.Count; a++) nextAncestors.Add(CloneAnswerRef(ancestors[a]));
                nextAncestors.Add(CloneAnswerRef(answer));

                for (var m = 0; m < menu.NextMenus[i].Count; m++)
                {
                    var nextMenu = menu.NextMenus[i][m];
                    if (string.IsNullOrEmpty(nextMenu) || string.Equals(nextMenu, menuId, StringComparison.Ordinal)) continue;
                    BuildPathsDepthFirst(nextMenu, menus, paths, occurrenceByKey, nextAncestors,
                        visitedMenus, depth + 1);
                }
            }

            visitedMenus.Remove(menuId);
        }

        private static void AddAnswerPath(Dictionary<string, List<AnswerPath>> paths, string key,
            List<AnswerRef> ancestors)
        {
            List<AnswerPath> list;
            if (!paths.TryGetValue(key, out list)) paths[key] = list = new List<AnswerPath>();
            var candidate = new AnswerPath();
            for (var i = 0; i < ancestors.Count; i++) candidate.Ancestors.Add(CloneAnswerRef(ancestors[i]));
            for (var p = 0; p < list.Count; p++) if (SamePath(list[p], candidate)) return;
            list.Add(candidate);
        }

        private static bool SamePath(AnswerPath a, AnswerPath b)
        {
            if (a.Ancestors.Count != b.Ancestors.Count) return false;
            for (var i = 0; i < a.Ancestors.Count; i++)
                if (a.Ancestors[i].Key != b.Ancestors[i].Key) return false;
            return true;
        }

        private static AnswerRef CloneAnswerRef(AnswerRef a)
        {
            return new AnswerRef { MenuId = a.MenuId, Index = a.Index, AnswerId = a.AnswerId };
        }

        private static Dictionary<string, Menu> BuildMenus(Dictionary<string, Node> nodes,
            Dictionary<string, List<Connection>> outgoingFlow, Dictionary<string, string> functionEvents,
            string serialized)
        {
            var result = new Dictionary<string, Menu>(StringComparer.Ordinal);
            foreach (var node in nodes.Values)
            {
                if (!node.Type.EndsWith("Flow_MultiAnswer", StringComparison.Ordinal)) continue;
                var menu = new Menu { NodeId = node.Id };
                var answers = ReadMultiAnswers(serialized, node);
                for (var i = 0; i < answers.Count; i++)
                {
                    menu.Answers.Add(answers[i]);
                    menu.NextMenus.Add(FindFirstMenusFromAnswer(node.Id, i, nodes, outgoingFlow, functionEvents, serialized));
                }
                result[menu.NodeId] = menu;
            }
            return result;
        }

        private static List<string> FindInteractionRoots(Dictionary<string, Node> nodes,
            Dictionary<string, List<Connection>> outgoingFlow, Dictionary<string, string> functionEvents,
            string serialized, Dictionary<string, Menu> menus)
        {
            var result = new List<string>();
            foreach (var node in nodes.Values)
            {
                if (!node.Type.EndsWith("CustomEvent", StringComparison.Ordinal) ||
                    node.Type.EndsWith("CustomFunctionEvent", StringComparison.Ordinal)) continue;
                if (!string.Equals(ReadCustomEventName(serialized, node), "interaction", StringComparison.Ordinal)) continue;
                var roots = FindFirstMenusFromNode(node.Id, nodes, outgoingFlow, functionEvents, serialized, 0,
                    new HashSet<string>(StringComparer.Ordinal));
                for (var i = 0; i < roots.Count; i++)
                    if (menus.ContainsKey(roots[i]) && !result.Contains(roots[i])) result.Add(roots[i]);
            }
            return result;
        }

        private static List<string> FindFirstMenusFromAnswer(string menuId, int index, Dictionary<string, Node> nodes,
            Dictionary<string, List<Connection>> outgoingFlow, Dictionary<string, string> functionEvents,
            string serialized)
        {
            var result = new List<string>();
            List<Connection> outgoing;
            if (!outgoingFlow.TryGetValue(menuId, out outgoing)) return result;
            for (var i = 0; i < outgoing.Count; i++)
            {
                var connection = outgoing[i];
                if (ParseOutPortIndex(connection.SourcePort) != index) continue;
                AddUnique(result, FindFirstMenusFromNode(connection.TargetNode, nodes, outgoingFlow, functionEvents,
                    serialized, 0, new HashSet<string>(StringComparer.Ordinal)));
            }
            return result;
        }

        private static List<string> FindFirstMenusFromNode(string nodeId, Dictionary<string, Node> nodes,
            Dictionary<string, List<Connection>> outgoingFlow, Dictionary<string, string> functionEvents,
            string serialized, int depth, HashSet<string> path)
        {
            var result = new List<string>();
            if (depth > 96 || string.IsNullOrEmpty(nodeId) || !path.Add(nodeId)) return result;
            Node node;
            if (!nodes.TryGetValue(nodeId, out node)) return result;
            if (node.Type.EndsWith("Flow_MultiAnswer", StringComparison.Ordinal))
            {
                result.Add(nodeId);
                return result;
            }
            if (node.Type.EndsWith("Return", StringComparison.Ordinal)) return result;

            if (node.Type.EndsWith("CustomFunctionCall", StringComparison.Ordinal))
            {
                string eventNodeId;
                if (!string.IsNullOrEmpty(node.SourceOutputUid) &&
                    functionEvents.TryGetValue(node.SourceOutputUid, out eventNodeId))
                {
                    var jumped = FindFirstMenusFromNode(eventNodeId, nodes, outgoingFlow, functionEvents,
                        serialized, depth + 1, new HashSet<string>(path, StringComparer.Ordinal));
                    if (jumped.Count > 0) return jumped;
                }
            }

            List<Connection> outgoing;
            if (!outgoingFlow.TryGetValue(nodeId, out outgoing)) return result;
            for (var i = 0; i < outgoing.Count; i++)
                AddUnique(result, FindFirstMenusFromNode(outgoing[i].TargetNode, nodes, outgoingFlow, functionEvents,
                    serialized, depth + 1, new HashSet<string>(path, StringComparer.Ordinal)));
            return result;
        }

        private static void AddUnique(List<string> target, List<string> source)
        {
            for (var i = 0; i < source.Count; i++)
                if (!target.Contains(source[i])) target.Add(source[i]);
        }

        private static List<Anchor> FindExactAnswerAnchors(Dictionary<string, Node> nodes,
            Dictionary<string, List<Connection>> incomingFlow, Dictionary<string, List<string>> callsByUid,
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
                if (a.MultiNodeId == candidate.MultiNodeId && a.AnswerIndex == candidate.AnswerIndex)
                {
                    if (candidate.FunctionJumps < a.FunctionJumps) a.FunctionJumps = candidate.FunctionJumps;
                    return;
                }
            }
            list.Add(candidate);
        }

        private static Dictionary<string, List<Connection>> BuildFlowConnectionMap(Dictionary<string, Node> nodes,
            List<Connection> connections, bool outgoing)
        {
            var result = new Dictionary<string, List<Connection>>(StringComparer.Ordinal);
            for (var i = 0; i < connections.Count; i++)
            {
                var c = connections[i];
                if (!IsFlowConnection(nodes, c)) continue;
                var key = outgoing ? c.SourceNode : c.TargetNode;
                List<Connection> list;
                if (!result.TryGetValue(key, out list)) result[key] = list = new List<Connection>();
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
                if (!node.Type.EndsWith("CustomFunctionCall", StringComparison.Ordinal) ||
                    string.IsNullOrEmpty(node.SourceOutputUid)) continue;
                List<string> list;
                if (!result.TryGetValue(node.SourceOutputUid, out list))
                    result[node.SourceOutputUid] = list = new List<string>();
                list.Add(node.Id);
            }
            return result;
        }

        private static Dictionary<string, string> BuildFunctionEventMap(Dictionary<string, Node> nodes)
        {
            var result = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (var node in nodes.Values)
            {
                if (!node.Type.EndsWith("CustomFunctionEvent", StringComparison.Ordinal) ||
                    string.IsNullOrEmpty(node.Uid)) continue;
                result[node.Uid] = node.Id;
            }
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

        private static string DescribeGate(string serialized, Dictionary<string, Node> nodes,
            List<Connection> connections, string multiId, int answerIndex)
        {
            var parts = new List<string>();
            var foundAnswerData = false;
            for (var i = 0; i < connections.Count; i++)
            {
                var c = connections[i];
                if (!string.Equals(c.TargetNode, multiId, StringComparison.Ordinal) ||
                    ParseAnswerPortIndex(c.TargetPort) != answerIndex) continue;
                Node answerNode;
                if (!nodes.TryGetValue(c.SourceNode, out answerNode) ||
                    answerNode.Type.IndexOf("Flow_Answer", StringComparison.Ordinal) < 0) continue;
                foundAnswerData = true;

                var gateFound = false;
                for (var j = 0; j < connections.Count; j++)
                {
                    var g = connections[j];
                    if (!string.Equals(g.TargetNode, answerNode.Id, StringComparison.Ordinal)) continue;
                    var isPrice = string.Equals(g.TargetPort, "price", StringComparison.OrdinalIgnoreCase);
                    var isLock = string.Equals(g.TargetPort, "lock", StringComparison.OrdinalIgnoreCase);
                    if (!isPrice && !isLock) continue;
                    Node smart;
                    if (!nodes.TryGetValue(g.SourceNode, out smart) ||
                        smart.Type.IndexOf("Flow_SmartRes", StringComparison.Ordinal) < 0)
                    {
                        parts.Add((isPrice ? "price" : "lock") + ":unsupported");
                        gateFound = true;
                        continue;
                    }
                    var type = ReadNodeContentAny(serialized, smart, "res_type", "Res type") ?? "?";
                    var id = ReadNodeContentAny(serialized, smart, "id", "Id") ?? "?";
                    float value;
                    var amount = TryReadNodeNumber(serialized, smart, out value)
                        ? value.ToString("0.####", CultureInfo.InvariantCulture)
                        : "?";
                    parts.Add((isPrice ? "price" : "lock") + ":" + type + ":" + id + "=" + amount);
                    gateFound = true;
                }
                if (!gateFound) parts.Add("answerData:no-gate");
            }
            if (!foundAnswerData) return "none";
            return parts.Count == 0 ? "answerData:unresolved" : string.Join(",", parts.ToArray());
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
                if (p.Length == 2 && p[0].ParameterType == typeof(string) && p[1].ParameterType == typeof(bool))
                    return method;
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
                        node.Uid = ReadRawStringBefore(serialized, node, "\"_UID\":\"");
                        node.SourceOutputUid = ReadRawStringBefore(serialized, node, "\"_sourceOutputUID\":\"");
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
            return TryReadNodeNumber(serialized, node, "v", out value) ||
                   TryReadNodeNumber(serialized, node, "V", out value);
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
            while (end < window.Length &&
                  (char.IsDigit(window[end]) || window[end] == '-' || window[end] == '+' ||
                   window[end] == '.' || window[end] == 'e' || window[end] == 'E')) end++;
            return end > pos && float.TryParse(window.Substring(pos, end - pos),
                NumberStyles.Float, CultureInfo.InvariantCulture, out value);
        }

        private static string ReadCustomEventName(string serialized, Node node)
        {
            return ReadRawStringBefore(serialized, node, "\"eventName\":{\"_value\":\"");
        }

        private static string ReadRawStringBefore(string serialized, Node node, string marker)
        {
            if (node == null) return null;
            var begin = Math.Max(0, node.TypePosition - 3000);
            var window = serialized.Substring(begin, node.TypePosition - begin);
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
            return port != null && port.StartsWith(prefix, StringComparison.Ordinal) &&
                   int.TryParse(port.Substring(prefix.Length), out value) ? value : -1;
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

        private static string EscapeLog(string value)
        {
            if (value == null) return "<null>";
            return value.Replace(" ", "_").Replace("\r", "").Replace("\n", "");
        }
    }
}

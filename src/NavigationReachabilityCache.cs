using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Text.RegularExpressions;

namespace CalendarQuestsPins
{
    /// <summary>
    /// Loading-derived navigation reachability for reminder-bearing dialogue answers.
    /// FlowCanvas traversal is bootstrap-only; gameplay evaluates compact persisted predicates.
    /// </summary>
    internal sealed class NavigationReachabilityCache
    {
        internal sealed class TargetNavigation
        {
            internal string NpcId;
            internal readonly Dictionary<string, List<NavigationPath>> PathsByAnswer =
                new Dictionary<string, List<NavigationPath>>(StringComparer.Ordinal);
        }

        internal sealed class NavigationPath
        {
            internal bool Unsupported;
            internal readonly List<EntryPredicate> Ancestors = new List<EntryPredicate>();
        }

        internal sealed class EntryPredicate
        {
            internal string AnswerId;
            internal bool RequireUnlocked;
            internal bool RequireNotBlacklisted;
            internal bool Unsupported;
            internal readonly List<GateVariant> GateVariants = new List<GateVariant>();
        }

        internal sealed class GateVariant
        {
            internal bool Unsupported;
            internal WeekdayInteractionRuleCache.Requirement Price;
            internal WeekdayInteractionRuleCache.Requirement Lock;
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

        private sealed class Menu
        {
            internal string NodeId;
            internal readonly List<string> Answers = new List<string>();
            internal readonly List<List<string>> NextMenus = new List<List<string>>();
        }

        private readonly WeekdayInteractionRuleCache _rules;
        private readonly MethodInfo _createSmartRes;
        private readonly MethodInfo _isEnough;
        private readonly Dictionary<string, TargetNavigation> _targets =
            new Dictionary<string, TargetNavigation>(StringComparer.Ordinal);
        private readonly object[] _isEnoughArgs = new object[1];
        private string _currentSerialized;

        internal int AnswerCount { get; private set; }
        internal int PathCount { get; private set; }
        internal int PredicateCount { get; private set; }
        internal int UnsupportedPathCount { get; private set; }

        internal NavigationReachabilityCache(WeekdayInteractionRuleCache rules)
        {
            if (rules == null) throw new ArgumentNullException("rules");
            _rules = rules;
            const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
            var type = typeof(WeekdayInteractionRuleCache);
            _createSmartRes = type.GetMethod("CreateSmartRes", flags);
            _isEnough = type.GetMethod("IsEnough", flags);
        }

        internal void Clear()
        {
            _targets.Clear();
            AnswerCount = 0;
            PathCount = 0;
            PredicateCount = 0;
            UnsupportedPathCount = 0;
        }

        internal bool IsReadyForTargets(IEnumerable<WeekdayInteractionRuleCache.TargetRules> targets)
        {
            if (targets == null || _targets.Count != 6) return false;
            var count = 0;
            foreach (var target in targets)
            {
                if (target == null || string.IsNullOrEmpty(target.NpcId) || !_targets.ContainsKey(target.NpcId)) return false;
                count++;
            }
            return count == 6;
        }

        internal bool IsOwnerTaskActionable(WeekdayInteractionRuleCache.TargetRules target, string taskId,
            object unlockedPhrases, object blacklistedPhrases)
        {
            if (target == null || string.IsNullOrEmpty(taskId)) return false;
            List<WeekdayInteractionRuleCache.RuleVariant> variants;
            if (!target.OwnerTaskRules.TryGetValue(taskId, out variants)) return false;
            return AnyVariantActionable(target.NpcId, variants, unlockedPhrases, blacklistedPhrases);
        }

        internal bool IsCrossTaskActionable(WeekdayInteractionRuleCache.TargetRules target,
            WeekdayInteractionRuleCache.CrossTaskRules task, object unlockedPhrases, object blacklistedPhrases)
        {
            return target != null && task != null &&
                   AnyVariantActionable(target.NpcId, task.Rules, unlockedPhrases, blacklistedPhrases);
        }

        internal bool IsTopicActionable(WeekdayInteractionRuleCache.TargetRules target,
            WeekdayInteractionRuleCache.TopicRule topic, object unlockedPhrases, object blacklistedPhrases)
        {
            return target != null && topic != null && !string.IsNullOrEmpty(topic.AnswerId) &&
                   AnyVariantActionable(target.NpcId, topic.Variants, unlockedPhrases, blacklistedPhrases);
        }

        private bool AnyVariantActionable(string npcId, List<WeekdayInteractionRuleCache.RuleVariant> variants,
            object unlockedPhrases, object blacklistedPhrases)
        {
            if (variants == null) return false;
            for (var i = 0; i < variants.Count; i++)
            {
                var variant = variants[i];
                if (variant == null || variant.Unsupported || string.IsNullOrEmpty(variant.AnswerId)) continue;
                if (!PhraseOpen(variant.AnswerId, unlockedPhrases, blacklistedPhrases)) continue;
                if (variant.Price != null && !IsEnough(variant.Price)) continue;
                if (variant.Lock != null && !IsEnough(variant.Lock)) continue;
                if (!IsNavigationReachable(npcId, variant.AnswerId, unlockedPhrases, blacklistedPhrases)) continue;
                return true;
            }
            return false;
        }

        internal bool IsNavigationReachable(string npcId, string answerId, object unlockedPhrases, object blacklistedPhrases)
        {
            TargetNavigation target;
            if (string.IsNullOrEmpty(npcId) || string.IsNullOrEmpty(answerId) ||
                !_targets.TryGetValue(npcId, out target)) return false;
            List<NavigationPath> paths;
            if (!target.PathsByAnswer.TryGetValue(answerId, out paths) || paths == null || paths.Count == 0) return false;

            for (var i = 0; i < paths.Count; i++)
            {
                var path = paths[i];
                if (path == null || path.Unsupported) continue;
                var ok = true;
                for (var p = 0; p < path.Ancestors.Count; p++)
                {
                    if (!PredicateSatisfied(path.Ancestors[p], unlockedPhrases, blacklistedPhrases))
                    {
                        ok = false;
                        break;
                    }
                }
                if (ok) return true;
            }
            return false;
        }

        private bool PredicateSatisfied(EntryPredicate predicate, object unlockedPhrases, object blacklistedPhrases)
        {
            if (predicate == null || predicate.Unsupported) return false;
            if (predicate.RequireNotBlacklisted && ContainsString(blacklistedPhrases, predicate.AnswerId)) return false;
            if (predicate.RequireUnlocked && !ContainsString(unlockedPhrases, predicate.AnswerId)) return false;
            if (predicate.GateVariants.Count == 0) return true;

            for (var i = 0; i < predicate.GateVariants.Count; i++)
            {
                var gate = predicate.GateVariants[i];
                if (gate == null || gate.Unsupported) continue;
                if (gate.Price != null && !IsEnough(gate.Price)) continue;
                if (gate.Lock != null && !IsEnough(gate.Lock)) continue;
                return true;
            }
            return false;
        }

        private bool IsEnough(WeekdayInteractionRuleCache.Requirement requirement)
        {
            if (requirement == null || _isEnough == null) return false;
            try
            {
                _isEnoughArgs[0] = requirement;
                var result = _isEnough.Invoke(_rules, _isEnoughArgs);
                return result is bool && (bool)result;
            }
            catch { return false; }
        }

        internal bool BuildTarget(string npcId, string serialized, object linkedWgo,
            WeekdayInteractionRuleCache.TargetRules productionTarget, out string failure)
        {
            failure = null;
            if (string.IsNullOrEmpty(npcId) || string.IsNullOrEmpty(serialized) || linkedWgo == null || productionTarget == null)
            {
                failure = "navigation target inputs are incomplete";
                return false;
            }

            _currentSerialized = serialized;
            var nodes = BuildNodeIndex(serialized);
            var connections = ParseConnections(serialized);
            if (nodes.Count == 0 || connections.Count == 0)
            {
                failure = "navigation graph index is empty for " + npcId;
                return false;
            }

            var outgoingFlow = BuildConnectionMap(connections, true);
            var incomingValue = BuildConnectionMap(connections, false);
            var functionEvents = BuildFunctionEventMap(nodes, serialized);
            var authoredBlacklist = BuildAuthoredBlacklistSet(nodes, serialized);
            var menus = BuildMenus(nodes, connections, outgoingFlow, functionEvents, serialized);
            if (menus.Count == 0)
            {
                failure = "no dialogue menus found for " + npcId;
                return false;
            }

            var roots = FindInteractionRoots(nodes, outgoingFlow, functionEvents, serialized, menus);
            if (roots.Count == 0)
            {
                failure = "interaction root menu not resolved for " + npcId;
                return false;
            }

            var target = new TargetNavigation { NpcId = npcId };
            for (var r = 0; r < roots.Count; r++)
            {
                BuildPathsDepthFirst(roots[r], menus, nodes, connections, incomingValue, authoredBlacklist,
                    linkedWgo, target, new List<EntryPredicate>(), new HashSet<string>(StringComparer.Ordinal), 0);
            }

            var required = CollectRequiredAnswerIds(productionTarget);
            foreach (var answerId in required)
            {
                List<NavigationPath> paths;
                if (!target.PathsByAnswer.TryGetValue(answerId, out paths) || paths == null || paths.Count == 0)
                {
                    failure = "no interaction-root navigation path for required answer " + npcId + " / " + answerId;
                    return false;
                }
            }

            _targets[npcId] = target;
            Recount();
            return true;
        }

        private void BuildPathsDepthFirst(string menuId, Dictionary<string, Menu> menus,
            Dictionary<string, Node> nodes, List<Connection> connections,
            Dictionary<string, List<Connection>> incomingValue, HashSet<string> authoredBlacklist,
            object linkedWgo, TargetNavigation target, List<EntryPredicate> ancestors,
            HashSet<string> visitedMenus, int depth)
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
                AddPath(target, answerId, ancestors);

                if (i >= menu.NextMenus.Count || menu.NextMenus[i] == null || menu.NextMenus[i].Count == 0) continue;
                var predicate = BuildEntryPredicate(answerId, menu.NodeId, i, nodes, connections,
                    incomingValue, authoredBlacklist, linkedWgo);
                var nextAncestors = ancestors;
                if (predicate != null)
                {
                    nextAncestors = new List<EntryPredicate>(ancestors.Count + 1);
                    nextAncestors.AddRange(ancestors);
                    nextAncestors.Add(predicate);
                }

                for (var n = 0; n < menu.NextMenus[i].Count; n++)
                {
                    var nextMenu = menu.NextMenus[i][n];
                    if (string.IsNullOrEmpty(nextMenu) || string.Equals(nextMenu, menuId, StringComparison.Ordinal)) continue;
                    BuildPathsDepthFirst(nextMenu, menus, nodes, connections, incomingValue, authoredBlacklist,
                        linkedWgo, target, nextAncestors, visitedMenus, depth + 1);
                }
            }

            visitedMenus.Remove(menuId);
        }

        private EntryPredicate BuildEntryPredicate(string answerId, string menuId, int index,
            Dictionary<string, Node> nodes, List<Connection> connections,
            Dictionary<string, List<Connection>> incomingValue, HashSet<string> authoredBlacklist,
            object linkedWgo)
        {
            var requireUnlocked = answerId.StartsWith("@", StringComparison.Ordinal);
            var requireNotBlacklisted = requireUnlocked || authoredBlacklist.Contains(answerId);
            var gates = BuildGateVariants(menuId, index, nodes, connections, incomingValue, linkedWgo);
            if (!requireUnlocked && !requireNotBlacklisted && gates.Count == 0) return null;

            var predicate = new EntryPredicate
            {
                AnswerId = answerId,
                RequireUnlocked = requireUnlocked,
                RequireNotBlacklisted = requireNotBlacklisted
            };
            for (var i = 0; i < gates.Count; i++) predicate.GateVariants.Add(gates[i]);
            if (gates.Count > 0)
            {
                var anySupported = false;
                for (var i = 0; i < gates.Count; i++)
                    if (gates[i] != null && !gates[i].Unsupported) { anySupported = true; break; }
                if (!anySupported) predicate.Unsupported = true;
            }
            return predicate;
        }

        private List<GateVariant> BuildGateVariants(string menuId, int index, Dictionary<string, Node> nodes,
            List<Connection> connections, Dictionary<string, List<Connection>> incomingValue, object linkedWgo)
        {
            var result = new List<GateVariant>();
            var foundSlotConnection = false;
            for (var i = 0; i < connections.Count; i++)
            {
                var connection = connections[i];
                if (!string.Equals(connection.TargetNode, menuId, StringComparison.Ordinal) ||
                    ParseAnswerPortIndex(connection.TargetPort) != index) continue;
                foundSlotConnection = true;
                Node answerNode;
                if (!nodes.TryGetValue(connection.SourceNode, out answerNode) ||
                    answerNode.Type.IndexOf("Flow_Answer", StringComparison.Ordinal) < 0) continue;
                result.Add(BuildGateVariant(answerNode, nodes, incomingValue, linkedWgo));
            }
            if (foundSlotConnection && result.Count == 0)
                result.Add(new GateVariant { Unsupported = true });
            return result;
        }

        private GateVariant BuildGateVariant(Node answerNode, Dictionary<string, Node> nodes,
            Dictionary<string, List<Connection>> incomingValue, object linkedWgo)
        {
            var result = new GateVariant();
            List<Connection> inputs;
            if (!incomingValue.TryGetValue(answerNode.Id, out inputs)) return result;
            for (var i = 0; i < inputs.Count; i++)
            {
                var input = inputs[i];
                var isPrice = string.Equals(input.TargetPort, "price", StringComparison.OrdinalIgnoreCase);
                var isLock = string.Equals(input.TargetPort, "lock", StringComparison.OrdinalIgnoreCase);
                if (!isPrice && !isLock) continue;
                Node source;
                if (!nodes.TryGetValue(input.SourceNode, out source) ||
                    source.Type.IndexOf("Flow_SmartRes", StringComparison.Ordinal) < 0)
                {
                    result.Unsupported = true;
                    continue;
                }
                var requirement = ParseRequirement(source, linkedWgo);
                if (requirement == null)
                {
                    result.Unsupported = true;
                    continue;
                }
                if (isPrice) result.Price = requirement; else result.Lock = requirement;
            }
            return result;
        }

        private WeekdayInteractionRuleCache.Requirement ParseRequirement(Node node, object linkedWgo)
        {
            var type = ReadNodeContentAny(_currentSerialized, node, "res_type", "Res type");
            var id = ReadNodeContentAny(_currentSerialized, node, "id", "Id");
            float value;
            if (string.IsNullOrEmpty(type) || string.IsNullOrEmpty(id) || !TryReadNodeNumber(_currentSerialized, node, out value)) return null;
            var requirement = new WeekdayInteractionRuleCache.Requirement { ResType = type, Id = id, Value = value };
            return BindRequirement(requirement, linkedWgo) ? requirement : null;
        }

        private bool BindRequirement(WeekdayInteractionRuleCache.Requirement requirement, object linkedWgo)
        {
            if (requirement == null || _createSmartRes == null) return false;
            try
            {
                requirement.SmartRes = _createSmartRes.Invoke(_rules, new object[] { requirement, linkedWgo });
                return requirement.SmartRes != null || !string.IsNullOrEmpty(requirement.AuthoritativeZoneId);
            }
            catch
            {
                requirement.SmartRes = null;
                return !string.IsNullOrEmpty(requirement.AuthoritativeZoneId);
            }
        }

        private Dictionary<string, Menu> BuildMenus(Dictionary<string, Node> nodes, List<Connection> connections,
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

        private List<string> FindInteractionRoots(Dictionary<string, Node> nodes,
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

        private List<string> FindFirstMenusFromAnswer(string menuId, int index, Dictionary<string, Node> nodes,
            Dictionary<string, List<Connection>> outgoingFlow, Dictionary<string, string> functionEvents, string serialized)
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

        private List<string> FindFirstMenusFromNode(string nodeId, Dictionary<string, Node> nodes,
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
                var uid = ReadSourceOutputUid(serialized, node);
                string eventNodeId;
                if (!string.IsNullOrEmpty(uid) && functionEvents.TryGetValue(uid, out eventNodeId))
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

        private static HashSet<string> CollectRequiredAnswerIds(WeekdayInteractionRuleCache.TargetRules target)
        {
            var result = new HashSet<string>(StringComparer.Ordinal);
            foreach (var pair in target.OwnerTaskRules) AddSupportedAnswerIds(result, pair.Value);
            for (var i = 0; i < target.CrossTasks.Count; i++) AddSupportedAnswerIds(result, target.CrossTasks[i].Rules);
            for (var i = 0; i < target.Topics.Count; i++) AddSupportedAnswerIds(result, target.Topics[i].Variants);
            return result;
        }

        private static void AddSupportedAnswerIds(HashSet<string> destination,
            List<WeekdayInteractionRuleCache.RuleVariant> variants)
        {
            if (variants == null) return;
            for (var i = 0; i < variants.Count; i++)
            {
                var variant = variants[i];
                if (variant == null || variant.Unsupported || string.IsNullOrEmpty(variant.AnswerId)) continue;
                destination.Add(variant.AnswerId);
            }
        }

        private static void AddPath(TargetNavigation target, string answerId, List<EntryPredicate> ancestors)
        {
            List<NavigationPath> paths;
            if (!target.PathsByAnswer.TryGetValue(answerId, out paths))
                target.PathsByAnswer[answerId] = paths = new List<NavigationPath>();
            var candidate = new NavigationPath();
            for (var i = 0; i < ancestors.Count; i++) candidate.Ancestors.Add(ClonePredicate(ancestors[i]));
            for (var i = 0; i < paths.Count; i++) if (SamePath(paths[i], candidate)) return;
            paths.Add(candidate);
        }

        private static EntryPredicate ClonePredicate(EntryPredicate source)
        {
            var clone = new EntryPredicate
            {
                AnswerId = source.AnswerId,
                RequireUnlocked = source.RequireUnlocked,
                RequireNotBlacklisted = source.RequireNotBlacklisted,
                Unsupported = source.Unsupported
            };
            for (var i = 0; i < source.GateVariants.Count; i++) clone.GateVariants.Add(source.GateVariants[i]);
            return clone;
        }

        private static bool SamePath(NavigationPath a, NavigationPath b)
        {
            if (a == null || b == null || a.Unsupported != b.Unsupported || a.Ancestors.Count != b.Ancestors.Count) return false;
            for (var i = 0; i < a.Ancestors.Count; i++)
            {
                var x = a.Ancestors[i];
                var y = b.Ancestors[i];
                if (!string.Equals(x.AnswerId, y.AnswerId, StringComparison.Ordinal) ||
                    x.RequireUnlocked != y.RequireUnlocked || x.RequireNotBlacklisted != y.RequireNotBlacklisted ||
                    x.Unsupported != y.Unsupported || x.GateVariants.Count != y.GateVariants.Count) return false;
                for (var g = 0; g < x.GateVariants.Count; g++) if (!SameGate(x.GateVariants[g], y.GateVariants[g])) return false;
            }
            return true;
        }

        private static bool SameGate(GateVariant a, GateVariant b)
        {
            if (ReferenceEquals(a, b)) return true;
            if (a == null || b == null || a.Unsupported != b.Unsupported) return false;
            return SameRequirement(a.Price, b.Price) && SameRequirement(a.Lock, b.Lock);
        }

        private static bool SameRequirement(WeekdayInteractionRuleCache.Requirement a,
            WeekdayInteractionRuleCache.Requirement b)
        {
            if (ReferenceEquals(a, b)) return true;
            if (a == null || b == null) return false;
            return string.Equals(a.ResType, b.ResType, StringComparison.Ordinal) &&
                   string.Equals(a.Id, b.Id, StringComparison.Ordinal) && Math.Abs(a.Value - b.Value) < 0.0001f &&
                   string.Equals(a.AuthoritativeZoneId, b.AuthoritativeZoneId, StringComparison.Ordinal);
        }

        internal bool ValidateVerifiedContracts(out string failure)
        {
            failure = null;
            if (!HasAncestor("npc_actress", "@actress_2b_1a", "actress_2b") ||
                !HasAncestor("npc_actress", "@actress_2b_1b", "actress_2b"))
            {
                failure = "verified Charmel nested parent chain was not derived";
                return false;
            }
            if (!AncestorHasRequirement("npc_actress", "@actress_2b_1a", "actress_2b", "GameRes", "_rel", 10f) ||
                !AncestorHasRequirement("npc_actress", "@actress_2b_1b", "actress_2b", "GameRes", "_rel", 10f))
            {
                failure = "verified Charmel parent relation gate was not derived";
                return false;
            }
            if (!HasAncestor("npc_merchant", "@merchant_marketing_done", "@merchant_business") ||
                !HasAncestor("npc_merchant", "@merchant_sales_done", "@merchant_business"))
            {
                failure = "verified Merchant business parent chain was not derived";
                return false;
            }
            if (!HasAncestor("npc_merchant", "@merchant_2e_1f", "@merchant_2e"))
            {
                failure = "verified Merchant debt parent chain was not derived";
                return false;
            }
            if (HasAncestor("npc_bishop", "@bishop_cathidral", "about_cathedral"))
            {
                failure = "unconditional Bishop cathedral parent did not compile away";
                return false;
            }
            if (!HasAncestor("npc_cultist", "@snake_help_done", "@snake_ritual_help"))
            {
                failure = "verified Snake ritual parent chain was not derived";
                return false;
            }
            return true;
        }

        private bool HasAncestor(string npcId, string answerId, string ancestorId)
        {
            TargetNavigation target;
            if (!_targets.TryGetValue(npcId, out target)) return false;
            List<NavigationPath> paths;
            if (!target.PathsByAnswer.TryGetValue(answerId, out paths)) return false;
            for (var p = 0; p < paths.Count; p++)
                for (var i = 0; i < paths[p].Ancestors.Count; i++)
                    if (string.Equals(paths[p].Ancestors[i].AnswerId, ancestorId, StringComparison.Ordinal)) return true;
            return false;
        }

        private bool AncestorHasRequirement(string npcId, string answerId, string ancestorId,
            string resType, string id, float value)
        {
            TargetNavigation target;
            if (!_targets.TryGetValue(npcId, out target)) return false;
            List<NavigationPath> paths;
            if (!target.PathsByAnswer.TryGetValue(answerId, out paths)) return false;
            for (var p = 0; p < paths.Count; p++)
                for (var i = 0; i < paths[p].Ancestors.Count; i++)
                {
                    var predicate = paths[p].Ancestors[i];
                    if (!string.Equals(predicate.AnswerId, ancestorId, StringComparison.Ordinal)) continue;
                    for (var g = 0; g < predicate.GateVariants.Count; g++)
                    {
                        var gate = predicate.GateVariants[g];
                        if (RequirementMatches(gate.Price, resType, id, value) || RequirementMatches(gate.Lock, resType, id, value)) return true;
                    }
                }
            return false;
        }

        private static bool RequirementMatches(WeekdayInteractionRuleCache.Requirement requirement,
            string resType, string id, float value)
        {
            return requirement != null && string.Equals(requirement.ResType, resType, StringComparison.Ordinal) &&
                   string.Equals(requirement.Id, id, StringComparison.Ordinal) && Math.Abs(requirement.Value - value) < 0.0001f;
        }

        internal void Write(BinaryWriter writer)
        {
            var npcIds = new List<string>(_targets.Keys);
            npcIds.Sort(StringComparer.Ordinal);
            writer.Write(npcIds.Count);
            for (var n = 0; n < npcIds.Count; n++)
            {
                var target = _targets[npcIds[n]];
                writer.Write(target.NpcId ?? string.Empty);
                var answerIds = new List<string>(target.PathsByAnswer.Keys);
                answerIds.Sort(StringComparer.Ordinal);
                writer.Write(answerIds.Count);
                for (var a = 0; a < answerIds.Count; a++)
                {
                    var answerId = answerIds[a];
                    writer.Write(answerId);
                    var paths = target.PathsByAnswer[answerId];
                    writer.Write(paths.Count);
                    for (var p = 0; p < paths.Count; p++)
                    {
                        var path = paths[p];
                        writer.Write(path.Unsupported);
                        writer.Write(path.Ancestors.Count);
                        for (var i = 0; i < path.Ancestors.Count; i++)
                        {
                            var predicate = path.Ancestors[i];
                            writer.Write(predicate.AnswerId ?? string.Empty);
                            writer.Write(predicate.RequireUnlocked);
                            writer.Write(predicate.RequireNotBlacklisted);
                            writer.Write(predicate.Unsupported);
                            writer.Write(predicate.GateVariants.Count);
                            for (var g = 0; g < predicate.GateVariants.Count; g++)
                            {
                                var gate = predicate.GateVariants[g];
                                writer.Write(gate.Unsupported);
                                WriteRequirement(writer, gate.Price);
                                WriteRequirement(writer, gate.Lock);
                            }
                        }
                    }
                }
            }
        }

        internal bool Read(BinaryReader reader, Dictionary<string, object> worldObjects, out string failure)
        {
            failure = null;
            Clear();
            try
            {
                var targetCount = reader.ReadInt32();
                if (targetCount != 6) { failure = "navigation target count mismatch"; return false; }
                for (var n = 0; n < targetCount; n++)
                {
                    var npcId = reader.ReadString();
                    object linkedWgo;
                    if (!worldObjects.TryGetValue(npcId, out linkedWgo) || linkedWgo == null)
                    { failure = "navigation WGO unavailable for " + npcId; return false; }
                    var target = new TargetNavigation { NpcId = npcId };
                    var answerCount = reader.ReadInt32();
                    if (answerCount < 0 || answerCount > 512) { failure = "navigation answer count out of range"; return false; }
                    for (var a = 0; a < answerCount; a++)
                    {
                        var answerId = reader.ReadString();
                        var pathCount = reader.ReadInt32();
                        if (pathCount < 0 || pathCount > 64) { failure = "navigation path count out of range"; return false; }
                        var paths = new List<NavigationPath>();
                        for (var p = 0; p < pathCount; p++)
                        {
                            var path = new NavigationPath { Unsupported = reader.ReadBoolean() };
                            var predicateCount = reader.ReadInt32();
                            if (predicateCount < 0 || predicateCount > 32) { failure = "navigation predicate count out of range"; return false; }
                            for (var i = 0; i < predicateCount; i++)
                            {
                                var predicate = new EntryPredicate
                                {
                                    AnswerId = reader.ReadString(),
                                    RequireUnlocked = reader.ReadBoolean(),
                                    RequireNotBlacklisted = reader.ReadBoolean(),
                                    Unsupported = reader.ReadBoolean()
                                };
                                var gateCount = reader.ReadInt32();
                                if (gateCount < 0 || gateCount > 16) { failure = "navigation gate count out of range"; return false; }
                                for (var g = 0; g < gateCount; g++)
                                {
                                    var gate = new GateVariant { Unsupported = reader.ReadBoolean() };
                                    bool valid;
                                    gate.Price = ReadRequirement(reader, linkedWgo, out valid);
                                    if (!valid) { failure = "invalid navigation price requirement"; return false; }
                                    gate.Lock = ReadRequirement(reader, linkedWgo, out valid);
                                    if (!valid) { failure = "invalid navigation lock requirement"; return false; }
                                    predicate.GateVariants.Add(gate);
                                }
                                path.Ancestors.Add(predicate);
                            }
                            paths.Add(path);
                        }
                        target.PathsByAnswer[answerId] = paths;
                    }
                    _targets[npcId] = target;
                }
                Recount();
                return ValidateVerifiedContracts(out failure);
            }
            catch (Exception ex)
            {
                failure = "navigation manifest read failed: " + ex.GetType().Name + ": " + ex.Message;
                Clear();
                return false;
            }
        }

        private WeekdayInteractionRuleCache.Requirement ReadRequirement(BinaryReader reader, object linkedWgo, out bool valid)
        {
            valid = true;
            if (!reader.ReadBoolean()) return null;
            var requirement = new WeekdayInteractionRuleCache.Requirement
            {
                ResType = reader.ReadString(),
                Id = reader.ReadString(),
                Value = reader.ReadSingle(),
                AuthoritativeZoneId = ReadNullableString(reader)
            };
            if (!BindRequirement(requirement, linkedWgo)) valid = false;
            return requirement;
        }

        private static void WriteRequirement(BinaryWriter writer, WeekdayInteractionRuleCache.Requirement requirement)
        {
            writer.Write(requirement != null);
            if (requirement == null) return;
            writer.Write(requirement.ResType ?? string.Empty);
            writer.Write(requirement.Id ?? string.Empty);
            writer.Write(requirement.Value);
            WriteNullableString(writer, requirement.AuthoritativeZoneId);
        }

        private static void WriteNullableString(BinaryWriter writer, string value)
        {
            writer.Write(value != null);
            if (value != null) writer.Write(value);
        }

        private static string ReadNullableString(BinaryReader reader)
        {
            return reader.ReadBoolean() ? reader.ReadString() : null;
        }

        private void Recount()
        {
            AnswerCount = 0;
            PathCount = 0;
            PredicateCount = 0;
            UnsupportedPathCount = 0;
            foreach (var target in _targets.Values)
            {
                AnswerCount += target.PathsByAnswer.Count;
                foreach (var paths in target.PathsByAnswer.Values)
                {
                    PathCount += paths.Count;
                    for (var i = 0; i < paths.Count; i++)
                    {
                        if (paths[i].Unsupported) UnsupportedPathCount++;
                        PredicateCount += paths[i].Ancestors.Count;
                    }
                }
            }
        }

        private static Dictionary<string, List<Connection>> BuildConnectionMap(List<Connection> connections, bool flow)
        {
            var result = new Dictionary<string, List<Connection>>(StringComparer.Ordinal);
            for (var i = 0; i < connections.Count; i++)
            {
                var connection = connections[i];
                if (IsFlowConnection(connection) != flow) continue;
                var key = flow ? connection.SourceNode : connection.TargetNode;
                List<Connection> list;
                if (!result.TryGetValue(key, out list)) result[key] = list = new List<Connection>();
                list.Add(connection);
            }
            return result;
        }

        private static Dictionary<string, string> BuildFunctionEventMap(Dictionary<string, Node> nodes, string serialized)
        {
            var result = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (var node in nodes.Values)
            {
                if (!node.Type.EndsWith("CustomFunctionEvent", StringComparison.Ordinal)) continue;
                var uid = ReadUid(serialized, node);
                if (!string.IsNullOrEmpty(uid)) result[uid] = node.Id;
            }
            return result;
        }

        private static HashSet<string> BuildAuthoredBlacklistSet(Dictionary<string, Node> nodes, string serialized)
        {
            var result = new HashSet<string>(StringComparer.Ordinal);
            foreach (var node in nodes.Values)
            {
                string phrase;
                if (TryReadBlacklistAdd(node, serialized, out phrase) && !string.IsNullOrEmpty(phrase)) result.Add(phrase);
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

        private static bool IsFlowConnection(Connection connection)
        {
            return connection != null &&
                   (string.Equals(connection.TargetPort, "In", StringComparison.OrdinalIgnoreCase) ||
                    string.IsNullOrWhiteSpace(connection.TargetPort));
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
            while (end < window.Length && (char.IsDigit(window[end]) || window[end] == '-' || window[end] == '+' ||
                   window[end] == '.' || window[end] == 'e' || window[end] == 'E')) end++;
            return end > pos && float.TryParse(window.Substring(pos, end - pos), NumberStyles.Float,
                CultureInfo.InvariantCulture, out value);
        }

        private static string ReadCustomEventName(string serialized, Node node)
        {
            return ReadRawStringBefore(serialized, node, "\"eventName\":{\"_value\":\"");
        }

        private static string ReadUid(string serialized, Node node)
        {
            return ReadRawStringBefore(serialized, node, "\"_UID\":\"");
        }

        private static string ReadSourceOutputUid(string serialized, Node node)
        {
            return ReadRawStringBefore(serialized, node, "\"_sourceOutputUID\":\"");
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
    }
}

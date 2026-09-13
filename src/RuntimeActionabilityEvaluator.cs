using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq.Expressions;
using System.Reflection;

namespace CalendarQuestsPins
{
    /// <summary>
    /// Allocation-light steady-state evaluator over the already-persisted 1.0.30 rule/navigation model.
    /// Structural discovery and manifest ownership remain in WeekdayInteractionRuleCache and
    /// NavigationReachabilityCache; this class only evaluates their cached runtime state.
    /// </summary>
    internal sealed class RuntimeActionabilityEvaluator
    {
        private readonly WeekdayInteractionRuleCache _rules;
        private readonly Dictionary<string, NavigationReachabilityCache.TargetNavigation> _navigationTargets;
        private readonly FieldInfo _playerField;
        private readonly FieldInfo _playerIsEnoughField;
        private readonly MethodInfo _ruleIsEnough;
        private readonly object[] _ruleIsEnoughArgs = new object[1];

        private object _boundPlayer;
        private MethodInfo _boundPlayerIsEnough;
        private Func<object, object, bool> _playerIsEnoughInvoker;

        internal RuntimeActionabilityEvaluator(WeekdayInteractionRuleCache rules, NavigationReachabilityCache reachability)
        {
            if (rules == null) throw new ArgumentNullException("rules");
            if (reachability == null) throw new ArgumentNullException("reachability");

            _rules = rules;
            const BindingFlags instance = BindingFlags.Instance | BindingFlags.NonPublic;

            var navigationTargetsField = typeof(NavigationReachabilityCache).GetField("_targets", instance);
            if (navigationTargetsField != null)
            {
                _navigationTargets = navigationTargetsField.GetValue(reachability) as
                    Dictionary<string, NavigationReachabilityCache.TargetNavigation>;
            }

            var rulesType = typeof(WeekdayInteractionRuleCache);
            _playerField = rulesType.GetField("_player", instance);
            _playerIsEnoughField = rulesType.GetField("_isEnough", instance);
            _ruleIsEnough = rulesType.GetMethod("IsEnough", instance);
        }

        internal void RefreshRuntimeBindings()
        {
            if (_playerField == null || _playerIsEnoughField == null) return;

            object player;
            MethodInfo method;
            try
            {
                player = _playerField.GetValue(_rules);
                method = _playerIsEnoughField.GetValue(_rules) as MethodInfo;
            }
            catch
            {
                player = null;
                method = null;
            }

            if (ReferenceEquals(player, _boundPlayer) && ReferenceEquals(method, _boundPlayerIsEnough)) return;

            _boundPlayer = player;
            _boundPlayerIsEnough = method;
            _playerIsEnoughInvoker = BuildPlayerIsEnoughInvoker(method);
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

        private bool IsNavigationReachable(string npcId, string answerId, object unlockedPhrases, object blacklistedPhrases)
        {
            if (_navigationTargets == null || string.IsNullOrEmpty(npcId) || string.IsNullOrEmpty(answerId)) return false;

            NavigationReachabilityCache.TargetNavigation target;
            if (!_navigationTargets.TryGetValue(npcId, out target)) return false;

            List<NavigationReachabilityCache.NavigationPath> paths;
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

        private bool PredicateSatisfied(NavigationReachabilityCache.EntryPredicate predicate,
            object unlockedPhrases, object blacklistedPhrases)
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
            if (requirement == null) return false;

            // Authoritative live-zone mirrors are rare and preserve the accepted 1.0.30 implementation.
            // Ordinary SmartRes gates use the direct compiled player invoker below.
            if (!string.IsNullOrEmpty(requirement.AuthoritativeZoneId))
            {
                if (_ruleIsEnough == null) return false;
                try
                {
                    _ruleIsEnoughArgs[0] = requirement;
                    var result = _ruleIsEnough.Invoke(_rules, _ruleIsEnoughArgs);
                    return result is bool && (bool)result;
                }
                catch { return false; }
            }

            if (requirement.SmartRes == null) return false;
            if (_playerIsEnoughInvoker == null || _boundPlayer == null) RefreshRuntimeBindings();
            if (_playerIsEnoughInvoker == null || _boundPlayer == null) return false;

            try { return _playerIsEnoughInvoker(_boundPlayer, requirement.SmartRes); }
            catch { return false; }
        }

        private static Func<object, object, bool> BuildPlayerIsEnoughInvoker(MethodInfo method)
        {
            if (method == null || method.IsStatic || method.ReturnType != typeof(bool)) return null;
            var parameters = method.GetParameters();
            if (parameters.Length != 1 || method.DeclaringType == null) return null;

            try
            {
                var player = Expression.Parameter(typeof(object), "player");
                var resource = Expression.Parameter(typeof(object), "resource");
                var call = Expression.Call(
                    Expression.Convert(player, method.DeclaringType),
                    method,
                    Expression.Convert(resource, parameters[0].ParameterType));
                return Expression.Lambda<Func<object, object, bool>>(call, player, resource).Compile();
            }
            catch { return null; }
        }

        private static bool PhraseOpen(string answerId, object unlockedObj, object blacklistedObj)
        {
            if (ContainsString(blacklistedObj, answerId)) return false;
            return !answerId.StartsWith("@", StringComparison.Ordinal) || ContainsString(unlockedObj, answerId);
        }

        private static bool ContainsString(object collection, string value)
        {
            var generic = collection as ICollection<string>;
            if (generic != null) return generic.Contains(value);

            var enumerable = collection as IEnumerable;
            if (enumerable == null) return false;
            foreach (var item in enumerable)
                if (string.Equals(item as string, value, StringComparison.Ordinal)) return true;
            return false;
        }
    }
}

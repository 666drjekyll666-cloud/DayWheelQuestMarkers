using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;

namespace CalendarQuestsPins
{
    /// <summary>
    /// Narrow GK 1.407 supplement for verified interaction routes that are not represented
    /// by the accepted generic task/topic classifiers. Most promoted completion answers reuse
    /// existing persisted one-shot/navigation predicates. Exact exceptional routes keep their
    /// authored phrase/task/resource boundaries and delegate resource sufficiency to the game.
    /// </summary>
    internal sealed class VerifiedCompletionReminderRules
    {
        private sealed class PromotedRoute
        {
            internal string NpcId;
            internal string TaskId;
            internal string AnswerId;
        }

        private static readonly PromotedRoute[] PromotedRoutes =
        {
            new PromotedRoute { NpcId = "npc_astrologer", TaskId = "dlc_souls_s29_1", AnswerId = "@souls_s_s30_ask" },
            new PromotedRoute { NpcId = "npc_cultist", TaskId = "snake_key", AnswerId = "@snake_give_key" },
            new PromotedRoute { NpcId = "npc_cultist", TaskId = "dlc_souls_s29_3", AnswerId = "@souls_s_s33_ask" },
            new PromotedRoute { NpcId = "npc_actress", TaskId = "dlc_souls_s29_2", AnswerId = "@souls_s_s31_ask" },
            new PromotedRoute { NpcId = "npc_bishop", TaskId = "bishop_rcitezen", AnswerId = "@bishop_get_citezen" }
        };

        private readonly Type _flowSmartResType = ReflectionUtil.FindType("FlowCanvas.Nodes.Flow_SmartRes");
        private MethodInfo _smartResFactory;
        private object _snakeRelationSmartRes;
        private object _snakeRelationLinkedWgo;
        private object _boundPlayer;
        private MethodInfo _isEnough;
        private readonly object[] _isEnoughArgs = new object[1];

        internal bool IsOwnerTaskActionable(WeekdayInteractionRuleCache.TargetRules target, string taskId,
            object unlockedPhrases, object blacklistedPhrases, NavigationReachabilityCache reachability, object mainGame)
        {
            if (target == null || string.IsNullOrEmpty(target.NpcId) || string.IsNullOrEmpty(taskId) || reachability == null)
                return false;

            // Runtime-proven mandatory interaction stages. The task becoming Visible is itself
            // the verified stage boundary: inquisitor_talk has no intermediate prerequisite;
            // snake_back becomes Visible only after the sword hand-in that installs the later
            // on_back_to_snake_after_ritual interaction event.
            if (string.Equals(target.NpcId, "npc_inquisitor", StringComparison.Ordinal) &&
                string.Equals(taskId, "inquisitor_talk", StringComparison.Ordinal)) return true;
            if (string.Equals(target.NpcId, "npc_cultist", StringComparison.Ordinal) &&
                string.Equals(taskId, "snake_back", StringComparison.Ordinal)) return true;

            // snake_trap is a normal selectable answer hidden behind FireEvent -> CustomEvent.
            // Its authored final-answer gate is GameRes:_rel >= 10. Preserve the exact navigation
            // path and delegate the relation check to Player.IsEnough(SmartRes).
            if (string.Equals(target.NpcId, "npc_cultist", StringComparison.Ordinal) &&
                string.Equals(taskId, "snake_trap", StringComparison.Ordinal))
            {
                const string answerId = "snake_stone_ready";
                if (ContainsString(blacklistedPhrases, answerId)) return false;
                if (!reachability.IsNavigationReachable(target.NpcId, answerId, unlockedPhrases, blacklistedPhrases)) return false;
                return IsSnakeRelationEnough(target.WorldObject, mainGame);
            }

            // The remaining audited routes already exist as persisted one-shot TopicRules in
            // schema 2. Reuse those exact phrase, SmartRes, and navigation predicates instead of
            // re-encoding their item/relation semantics here. This also intentionally keeps
            // @souls_s_s33_ask fail-closed because its AnswerData shape is unsupported.
            for (var i = 0; i < PromotedRoutes.Length; i++)
            {
                var route = PromotedRoutes[i];
                if (!string.Equals(route.NpcId, target.NpcId, StringComparison.Ordinal) ||
                    !string.Equals(route.TaskId, taskId, StringComparison.Ordinal)) continue;
                var topic = FindTopic(target, route.AnswerId);
                return topic != null && reachability.IsTopicActionable(target, topic, unlockedPhrases, blacklistedPhrases);
            }

            return false;
        }

        internal static bool IsPromotedCompletionTopic(string npcId, string answerId)
        {
            if (string.IsNullOrEmpty(npcId) || string.IsNullOrEmpty(answerId)) return false;
            for (var i = 0; i < PromotedRoutes.Length; i++)
            {
                var route = PromotedRoutes[i];
                if (string.Equals(route.NpcId, npcId, StringComparison.Ordinal) &&
                    string.Equals(route.AnswerId, answerId, StringComparison.Ordinal)) return true;
            }
            return false;
        }

        internal void Clear()
        {
            _snakeRelationSmartRes = null;
            _snakeRelationLinkedWgo = null;
            _boundPlayer = null;
            _isEnough = null;
            _isEnoughArgs[0] = null;
        }

        private static WeekdayInteractionRuleCache.TopicRule FindTopic(WeekdayInteractionRuleCache.TargetRules target, string answerId)
        {
            if (target == null || string.IsNullOrEmpty(answerId)) return null;
            for (var i = 0; i < target.Topics.Count; i++)
            {
                var topic = target.Topics[i];
                if (topic != null && string.Equals(topic.AnswerId, answerId, StringComparison.Ordinal)) return topic;
            }
            return null;
        }

        private bool IsSnakeRelationEnough(object linkedWgo, object mainGame)
        {
            if (linkedWgo == null || mainGame == null || !ReflectionUtil.IsUnityAlive(linkedWgo)) return false;
            if (!BindPlayer(mainGame)) return false;
            if (_snakeRelationSmartRes == null || !ReferenceEquals(_snakeRelationLinkedWgo, linkedWgo))
            {
                _snakeRelationSmartRes = CreateSmartRes("GameRes", "_rel", 10f, linkedWgo);
                _snakeRelationLinkedWgo = linkedWgo;
            }
            if (_snakeRelationSmartRes == null || _isEnough == null || _boundPlayer == null) return false;
            try
            {
                _isEnoughArgs[0] = _snakeRelationSmartRes;
                var result = _isEnough.Invoke(_boundPlayer, _isEnoughArgs);
                return result is bool && (bool)result;
            }
            catch { return false; }
        }

        private bool BindPlayer(object mainGame)
        {
            object player;
            if (!ReflectionUtil.TryRead(mainGame, "player", out player) || player == null || !ReflectionUtil.IsUnityAlive(player)) return false;
            if (ReferenceEquals(player, _boundPlayer) && _isEnough != null) return true;

            _boundPlayer = player;
            _isEnough = null;
            var smartResType = ReflectionUtil.FindType("SmartRes");
            if (smartResType == null) return false;
            foreach (var method in player.GetType().GetMethods(ReflectionUtil.AnyInstance))
            {
                var p = method.GetParameters();
                if (method.Name == "IsEnough" && p.Length == 1 && p[0].ParameterType.IsAssignableFrom(smartResType))
                {
                    _isEnough = method;
                    break;
                }
            }
            return _isEnough != null;
        }

        private object CreateSmartRes(string resType, string id, float value, object linkedWgo)
        {
            try
            {
                if (_flowSmartResType == null) return null;
                if (_smartResFactory == null)
                {
                    foreach (var method in _flowSmartResType.GetMethods(ReflectionUtil.AnyInstance | ReflectionUtil.AnyStatic | BindingFlags.DeclaredOnly))
                    {
                        if (method.Name == "Invoke" && method.GetParameters().Length == 3)
                        {
                            _smartResFactory = method;
                            break;
                        }
                    }
                }
                if (_smartResFactory == null) return null;

                var target = _smartResFactory.IsStatic ? null : Activator.CreateInstance(_smartResFactory.DeclaringType);
                var parameters = _smartResFactory.GetParameters();
                var args = new object[parameters.Length];
                for (var i = 0; i < parameters.Length; i++)
                {
                    var t = parameters[i].ParameterType;
                    if (t.IsEnum) args[i] = Enum.Parse(t, resType, false);
                    else if (t == typeof(string)) args[i] = id;
                    else if (t == typeof(float)) args[i] = value;
                    else if (t == typeof(double)) args[i] = (double)value;
                    else if (t == typeof(int)) args[i] = (int)Math.Round(value);
                    else return null;
                }

                var smartRes = _smartResFactory.Invoke(target, args);
                if (smartRes == null) return null;
                var field = smartRes.GetType().GetField("_linked_wgo", ReflectionUtil.AnyInstance);
                if (field != null && field.FieldType.IsInstanceOfType(linkedWgo)) field.SetValue(smartRes, linkedWgo);
                return smartRes;
            }
            catch { return null; }
        }

        private static bool ContainsString(object collection, string value)
        {
            var enumerable = collection as IEnumerable;
            if (enumerable == null) return false;
            foreach (var item in enumerable)
                if (string.Equals(item as string, value, StringComparison.Ordinal)) return true;
            return false;
        }
    }
}

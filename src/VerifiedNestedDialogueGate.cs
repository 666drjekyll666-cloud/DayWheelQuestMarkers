using System;
using System.Collections;
using System.Reflection;

namespace CalendarQuestsPins
{
    /// <summary>
    /// Narrow verified reachability guard for persisted one-shot topics that live behind another
    /// dialogue answer. The generic one-shot cache deliberately evaluates each topic's own gate;
    /// this guard covers the currently verified nested Charmel submenu without reintroducing broad
    /// runtime graph traversal.
    /// </summary>
    internal sealed class VerifiedNestedDialogueGate
    {
        private const string ActressNpcId = "npc_actress";
        private const string ParentAnswerId = "actress_2b";
        private const string ChildQuestionA = "@actress_2b_1a";
        private const string ChildQuestionB = "@actress_2b_1b";

        private readonly Type _flowSmartResType = ReflectionUtil.FindType("FlowCanvas.Nodes.Flow_SmartRes");
        private MethodInfo _smartResFactory;
        private MethodInfo _isEnough;
        private object _player;
        private object _linkedActress;
        private object _relationTen;
        private readonly object[] _isEnoughArgs = new object[1];

        internal bool IsSatisfied(WeekdayInteractionRuleCache.TargetRules target,
            WeekdayInteractionRuleCache.TopicRule topic, object mainGame, object blacklistedPhrases)
        {
            if (!IsVerifiedNestedCharmelTopic(target, topic)) return true;

            // Both child topics are entered through actress_2b. Either child persistently blacklists
            // that parent answer, so an already-consumed parent makes the remaining child unreachable.
            if (blacklistedPhrases == null || ContainsString(blacklistedPhrases, ParentAnswerId)) return false;

            if (mainGame == null || target == null || !ReflectionUtil.IsUnityAlive(target.WorldObject)) return false;
            if (!TryBindPlayer(mainGame)) return false;

            if (!ReferenceEquals(_linkedActress, target.WorldObject) || _relationTen == null)
            {
                _relationTen = CreateRelationRequirement(target.WorldObject);
                _linkedActress = target.WorldObject;
            }
            if (_relationTen == null || _isEnough == null || _player == null) return false;

            try
            {
                _isEnoughArgs[0] = _relationTen;
                var result = _isEnough.Invoke(_player, _isEnoughArgs);
                return result is bool && (bool)result;
            }
            catch
            {
                return false;
            }
        }

        private static bool IsVerifiedNestedCharmelTopic(WeekdayInteractionRuleCache.TargetRules target,
            WeekdayInteractionRuleCache.TopicRule topic)
        {
            if (target == null || topic == null ||
                !string.Equals(target.NpcId, ActressNpcId, StringComparison.Ordinal)) return false;

            return string.Equals(topic.AnswerId, ChildQuestionA, StringComparison.Ordinal) ||
                   string.Equals(topic.AnswerId, ChildQuestionB, StringComparison.Ordinal);
        }

        private bool TryBindPlayer(object mainGame)
        {
            object player;
            if (!ReflectionUtil.TryRead(mainGame, "player", out player) ||
                player == null || !ReflectionUtil.IsUnityAlive(player)) return false;

            if (ReferenceEquals(player, _player) && _isEnough != null) return true;

            _player = player;
            _isEnough = null;
            var smartResType = ReflectionUtil.FindType("SmartRes");
            if (smartResType == null) return false;

            foreach (var method in player.GetType().GetMethods(ReflectionUtil.AnyInstance))
            {
                var parameters = method.GetParameters();
                if (method.Name != "IsEnough" || parameters.Length != 1) continue;
                if (!parameters[0].ParameterType.IsAssignableFrom(smartResType)) continue;
                _isEnough = method;
                break;
            }

            return _isEnough != null;
        }

        private object CreateRelationRequirement(object linkedWgo)
        {
            try
            {
                if (_flowSmartResType == null) return null;
                if (_smartResFactory == null)
                {
                    foreach (var method in _flowSmartResType.GetMethods(
                                 ReflectionUtil.AnyInstance | ReflectionUtil.AnyStatic | BindingFlags.DeclaredOnly))
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
                    var type = parameters[i].ParameterType;
                    if (type.IsEnum) args[i] = Enum.Parse(type, "GameRes", false);
                    else if (type == typeof(string)) args[i] = "_rel";
                    else if (type == typeof(float)) args[i] = 10f;
                    else if (type == typeof(double)) args[i] = 10d;
                    else if (type == typeof(int)) args[i] = 10;
                    else return null;
                }

                var smartRes = _smartResFactory.Invoke(target, args);
                if (smartRes == null) return null;

                var linkedField = smartRes.GetType().GetField("_linked_wgo", ReflectionUtil.AnyInstance);
                if (linkedField == null || !linkedField.FieldType.IsInstanceOfType(linkedWgo)) return null;
                linkedField.SetValue(smartRes, linkedWgo);
                return smartRes;
            }
            catch
            {
                return null;
            }
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

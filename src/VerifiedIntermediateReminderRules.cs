using System;
using System.Collections;
using System.Reflection;

namespace CalendarQuestsPins
{
    /// <summary>
    /// Small verified GK 1.407 manifest for required weekday-NPC interactions that are intermediate
    /// progression stages rather than direct Flow_SetTaskState(... Complete) anchors.
    ///
    /// Inclusion requires both a visible source objective and an exact currently-open authored topic.
    /// Authored SmartRes gates are evaluated through the game's own Player.IsEnough path. Families are
    /// deliberately grouped so a multi-step interaction chain contributes at most one marker.
    /// </summary>
    internal sealed class VerifiedIntermediateReminderRules
    {
        internal sealed class Family
        {
            internal string OwnerNpcId;
            internal string TaskId;
            internal string TargetNpcId;
            internal Stage[] Stages;
        }

        internal sealed class Stage
        {
            internal string AnswerId;
            internal string ResType;
            internal string ResId;
            internal float ResValue;
            internal object CachedWgo;
            internal object CachedSmartRes;

            internal Stage(string answerId)
            {
                AnswerId = answerId;
            }

            internal Stage(string answerId, string resType, string resId, float resValue)
            {
                AnswerId = answerId;
                ResType = resType;
                ResId = resId;
                ResValue = resValue;
            }

            internal bool HasGate { get { return !string.IsNullOrEmpty(ResType) && !string.IsNullOrEmpty(ResId); } }
        }

        private readonly Family[] _families =
        {
            new Family
            {
                OwnerNpcId = "npc_astrologer",
                TaskId = "astrologer_daghter",
                TargetNpcId = "npc_actress",
                Stages = new[]
                {
                    new Stage("@actress_father", "GameRes", "_rel", 50f)
                }
            },
            new Family
            {
                OwnerNpcId = "npc_bishop",
                TaskId = "bishop_invitation_2",
                TargetNpcId = "npc_merchant",
                Stages = new[]
                {
                    new Stage("@merchant_ceremony", "GameRes", "_rel", 90f)
                }
            },
            new Family
            {
                OwnerNpcId = "npc_inquisitor",
                TaskId = "inquisitor_guards",
                TargetNpcId = "npc_inquisitor",
                Stages = new[]
                {
                    new Stage("@inquisitor_portal_guard")
                }
            },
            new Family
            {
                OwnerNpcId = "npc_merchant",
                TaskId = "merchant_support",
                TargetNpcId = "npc_actress",
                Stages = new[]
                {
                    new Stage("@actress_marketing", "GameRes", "_rel", 40f),
                    new Stage("@actress_ jewelry", "Item", "bijouterie_gold", 1f)
                }
            },
            new Family
            {
                OwnerNpcId = "npc_cultist",
                TaskId = "snake_help",
                TargetNpcId = "npc_cultist",
                Stages = new[]
                {
                    new Stage("@snake_help")
                }
            },
            new Family
            {
                OwnerNpcId = "npc_actress",
                TaskId = "actress_necklace",
                TargetNpcId = "npc_cultist",
                Stages = new[]
                {
                    new Stage("@snake_nacklase_0"),
                    new Stage("@snake_nacklase_again", "GameRes", "_rel", 30f),
                    new Stage("@snake_nacklase_again_10a"),
                    new Stage("@snake_nacklase_again_10b"),
                    new Stage("@snake_nacklase_again_10c")
                }
            }
        };

        private readonly Type _worldMapType = ReflectionUtil.FindType("WorldMap");
        private readonly Type _flowSmartResType = ReflectionUtil.FindType("FlowCanvas.Nodes.Flow_SmartRes");
        private MethodInfo _worldObjectGetter;
        private MethodInfo _smartResFactory;
        private MethodInfo _isEnough;
        private object _player;

        internal Family[] Families { get { return _families; } }

        internal bool IsActionable(
            Family family,
            QuestRuleCache rules,
            object mainGame,
            object unlockedPhrases,
            object blacklistedPhrases)
        {
            if (family == null || rules == null) return false;
            if (!IsTaskVisible(rules, family.OwnerNpcId, family.TaskId)) return false;

            for (var i = 0; i < family.Stages.Length; i++)
            {
                var stage = family.Stages[i];
                if (!PhraseOpen(stage.AnswerId, unlockedPhrases, blacklistedPhrases)) continue;
                if (!stage.HasGate) return true;
                if (IsEnough(mainGame, family.TargetNpcId, stage)) return true;
            }
            return false;
        }

        internal void Clear()
        {
            _player = null;
            _isEnough = null;
            ResetStageCaches();
        }

        private static bool IsTaskVisible(QuestRuleCache rules, string ownerNpcId, string taskId)
        {
            foreach (var npc in rules.AllNpcRules)
            {
                if (npc == null || !string.Equals(npc.NpcId, ownerNpcId, StringComparison.Ordinal)) continue;
                var tasks = ReflectionUtil.EnumerateMember(npc.KnownNpc, "tasks");
                if (tasks == null) return false;
                foreach (var task in tasks)
                {
                    string candidate;
                    if (QuestRuleCache.IsVisibleTask(task, out candidate) && string.Equals(candidate, taskId, StringComparison.Ordinal))
                        return true;
                }
                return false;
            }
            return false;
        }

        private bool IsEnough(object mainGame, string targetNpcId, Stage stage)
        {
            if (!EnsurePlayer(mainGame)) return false;

            if (!ReflectionUtil.IsUnityAlive(stage.CachedWgo) || stage.CachedSmartRes == null)
            {
                stage.CachedWgo = null;
                stage.CachedSmartRes = null;

                if (_worldObjectGetter == null) _worldObjectGetter = FindWorldObjectGetter();
                if (_smartResFactory == null) _smartResFactory = FindSmartResFactory();
                if (_worldObjectGetter == null || _smartResFactory == null) return false;

                try { stage.CachedWgo = _worldObjectGetter.Invoke(null, new object[] { targetNpcId, true }); }
                catch { stage.CachedWgo = null; }
                if (!ReflectionUtil.IsUnityAlive(stage.CachedWgo)) return false;

                stage.CachedSmartRes = CreateSmartRes(stage, stage.CachedWgo);
                if (stage.CachedSmartRes == null) return false;
            }

            try
            {
                var result = _isEnough.Invoke(_player, new[] { stage.CachedSmartRes });
                return result is bool && (bool)result;
            }
            catch
            {
                return false;
            }
        }

        private bool EnsurePlayer(object mainGame)
        {
            if (mainGame == null || _worldMapType == null || _flowSmartResType == null) return false;

            object currentPlayer;
            if (!ReflectionUtil.TryRead(mainGame, "player", out currentPlayer) ||
                currentPlayer == null || !ReflectionUtil.IsUnityAlive(currentPlayer)) return false;

            if (ReferenceEquals(currentPlayer, _player) && _isEnough != null) return true;

            _player = currentPlayer;
            _isEnough = FindIsEnough(currentPlayer);
            ResetStageCaches();
            return _isEnough != null;
        }

        private void ResetStageCaches()
        {
            for (var i = 0; i < _families.Length; i++)
            {
                var stages = _families[i].Stages;
                for (var j = 0; j < stages.Length; j++)
                {
                    stages[j].CachedWgo = null;
                    stages[j].CachedSmartRes = null;
                }
            }
        }

        private MethodInfo FindWorldObjectGetter()
        {
            foreach (var method in _worldMapType.GetMethods(ReflectionUtil.AnyStatic))
            {
                if (method.Name != "GetWorldGameObjectByObjId") continue;
                var p = method.GetParameters();
                if (p.Length == 2 && p[0].ParameterType == typeof(string) && p[1].ParameterType == typeof(bool))
                    return method;
            }
            return null;
        }

        private MethodInfo FindSmartResFactory()
        {
            foreach (var method in _flowSmartResType.GetMethods(
                         ReflectionUtil.AnyInstance | ReflectionUtil.AnyStatic | BindingFlags.DeclaredOnly))
            {
                if (method.Name == "Invoke" && method.GetParameters().Length == 3) return method;
            }
            return null;
        }

        private static MethodInfo FindIsEnough(object player)
        {
            if (player == null) return null;
            var smartResType = ReflectionUtil.FindType("SmartRes");
            if (smartResType == null) return null;

            foreach (var method in player.GetType().GetMethods(ReflectionUtil.AnyInstance))
            {
                var p = method.GetParameters();
                if (method.Name == "IsEnough" && p.Length == 1 && p[0].ParameterType.IsAssignableFrom(smartResType))
                    return method;
            }
            return null;
        }

        private object CreateSmartRes(Stage stage, object linkedWgo)
        {
            try
            {
                var target = _smartResFactory.IsStatic ? null : Activator.CreateInstance(_smartResFactory.DeclaringType);
                var p = _smartResFactory.GetParameters();
                var args = new object[p.Length];

                for (var i = 0; i < p.Length; i++)
                {
                    var t = p[i].ParameterType;
                    if (t.IsEnum) args[i] = Enum.Parse(t, stage.ResType, false);
                    else if (t == typeof(string)) args[i] = stage.ResId;
                    else if (t == typeof(float)) args[i] = stage.ResValue;
                    else if (t == typeof(double)) args[i] = (double)stage.ResValue;
                    else if (t == typeof(int)) args[i] = (int)Math.Round(stage.ResValue);
                    else return null;
                }

                var smartRes = _smartResFactory.Invoke(target, args);
                if (smartRes == null) return null;

                var field = smartRes.GetType().GetField("_linked_wgo", ReflectionUtil.AnyInstance);
                if (field == null || linkedWgo == null || !field.FieldType.IsInstanceOfType(linkedWgo)) return null;
                field.SetValue(smartRes, linkedWgo);
                return smartRes;
            }
            catch
            {
                return null;
            }
        }

        private static bool PhraseOpen(string answerId, object unlockedObj, object blacklistedObj)
        {
            if (ContainsString(blacklistedObj, answerId)) return false;
            return ContainsString(unlockedObj, answerId);
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

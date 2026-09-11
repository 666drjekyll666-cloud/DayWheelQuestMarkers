using System;
using System.Collections;
using System.Reflection;

namespace CalendarQuestsPins
{
    /// <summary>
    /// Two narrow, verified GK 1.407 objective bridges that are not represented by the normal
    /// Flow_SetTaskState(... Complete) task-anchor model. These mappings are intentionally explicit:
    /// the full 0.1.26-0.1.29 authored-universe audit found only these two real bridge families.
    ///
    /// Each family contributes at most one reminder while either its entry topic or its verified
    /// continuation is actionable. Arbitrary relationship-unlocked dialogue is intentionally ignored.
    /// </summary>
    internal sealed class VerifiedBridgeReminderRules
    {
        private const string AstrologerNpcId = "npc_astrologer";
        private const string CultistNpcId = "npc_cultist";

        private const string AstrologerMillEntry = "@astrologer_fix_mill";
        private const string AstrologerMillContinuation = "@astrologer_fix";
        private const float AstrologerMillContinuationRelation = 60f;

        private const string SnakeInstrumentEntry = "@snake_instrument";
        private const string SnakeInstrumentContinuation = "@snake_instrument_ready";
        private const float SnakeInstrumentContinuationRelation = 40f;

        private const string RelationResourceId = "_rel";

        private readonly Type _worldMapType = ReflectionUtil.FindType("WorldMap");
        private readonly Type _flowSmartResType = ReflectionUtil.FindType("FlowCanvas.Nodes.Flow_SmartRes");
        private MethodInfo _worldObjectGetter;
        private MethodInfo _smartResFactory;
        private MethodInfo _isEnough;
        private object _player;

        private object _astrologerWgo;
        private object _astrologerRelation60;
        private object _cultistWgo;
        private object _cultistRelation40;

        internal bool IsAstrologerMillActionable(object mainGame, object unlockedPhrases, object blacklistedPhrases)
        {
            if (PhraseOpen(AstrologerMillEntry, unlockedPhrases, blacklistedPhrases)) return true;
            if (!PhraseOpen(AstrologerMillContinuation, unlockedPhrases, blacklistedPhrases)) return false;

            return IsRelationEnough(
                mainGame,
                AstrologerNpcId,
                AstrologerMillContinuationRelation,
                ref _astrologerWgo,
                ref _astrologerRelation60);
        }

        internal bool IsSnakeInstrumentActionable(object mainGame, object unlockedPhrases, object blacklistedPhrases)
        {
            if (PhraseOpen(SnakeInstrumentEntry, unlockedPhrases, blacklistedPhrases)) return true;
            if (!PhraseOpen(SnakeInstrumentContinuation, unlockedPhrases, blacklistedPhrases)) return false;

            return IsRelationEnough(
                mainGame,
                CultistNpcId,
                SnakeInstrumentContinuationRelation,
                ref _cultistWgo,
                ref _cultistRelation40);
        }

        internal void Clear()
        {
            _player = null;
            _isEnough = null;
            _astrologerWgo = null;
            _astrologerRelation60 = null;
            _cultistWgo = null;
            _cultistRelation40 = null;
        }

        private bool IsRelationEnough(
            object mainGame,
            string npcId,
            float requiredRelation,
            ref object cachedWgo,
            ref object cachedRelationLock)
        {
            if (!EnsurePlayer(mainGame)) return false;

            if (!ReflectionUtil.IsUnityAlive(cachedWgo) || cachedRelationLock == null)
            {
                cachedWgo = null;
                cachedRelationLock = null;

                if (_worldObjectGetter == null) _worldObjectGetter = FindWorldObjectGetter();
                if (_smartResFactory == null) _smartResFactory = FindSmartResFactory();
                if (_worldObjectGetter == null || _smartResFactory == null) return false;

                try { cachedWgo = _worldObjectGetter.Invoke(null, new object[] { npcId, true }); }
                catch { cachedWgo = null; }
                if (!ReflectionUtil.IsUnityAlive(cachedWgo)) return false;

                cachedRelationLock = CreateGameResSmartRes(RelationResourceId, requiredRelation, cachedWgo);
                if (cachedRelationLock == null) return false;
            }

            try
            {
                var result = _isEnough.Invoke(_player, new[] { cachedRelationLock });
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

            // SmartRes relation checks are linked to the NPC world objects of the current runtime session.
            // Recreate them whenever the gameplay player/session binding changes.
            _astrologerWgo = null;
            _astrologerRelation60 = null;
            _cultistWgo = null;
            _cultistRelation40 = null;

            return _isEnough != null;
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

        private object CreateGameResSmartRes(string id, float value, object linkedWgo)
        {
            try
            {
                var target = _smartResFactory.IsStatic ? null : Activator.CreateInstance(_smartResFactory.DeclaringType);
                var p = _smartResFactory.GetParameters();
                var args = new object[p.Length];

                for (var i = 0; i < p.Length; i++)
                {
                    var t = p[i].ParameterType;
                    if (t.IsEnum) args[i] = Enum.Parse(t, "GameRes", false);
                    else if (t == typeof(string)) args[i] = id;
                    else if (t == typeof(float)) args[i] = value;
                    else if (t == typeof(double)) args[i] = (double)value;
                    else if (t == typeof(int)) args[i] = (int)Math.Round(value);
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

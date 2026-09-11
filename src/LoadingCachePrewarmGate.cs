using System;
using System.Reflection;
using UnityEngine;

namespace CalendarQuestsPins
{
    // Read-only readiness gate for the pre-game loading window. It does not build or mutate caches;
    // it only verifies that every runtime object the existing cache builders already depend on is present.
    internal sealed class LoadingCachePrewarmGate
    {
        private static readonly string[] PeriodicNpcIds =
        {
            "npc_astrologer", "npc_inquisitor", "npc_cultist", "npc_merchant", "npc_actress", "npc_bishop"
        };

        private readonly Type _worldMapType = ReflectionUtil.FindType("WorldMap");
        private readonly Type _controllerType = ReflectionUtil.FindType("FlowCanvas.FlowScriptController");
        private readonly MethodInfo _worldObjectGetter;

        internal LoadingCachePrewarmGate()
        {
            _worldObjectGetter = FindWorldObjectGetter();
        }

        internal bool IsReady(object mainGame)
        {
            if (mainGame == null || _worldObjectGetter == null || _controllerType == null) return false;

            object player;
            if (!ReflectionUtil.TryRead(mainGame, "player", out player) || player == null || !ReflectionUtil.IsUnityAlive(player))
                return false;

            for (var i = 0; i < PeriodicNpcIds.Length; i++)
            {
                var wgo = GetWorldObject(PeriodicNpcIds[i]);
                var component = wgo as Component;
                if (component == null || !component) return false;

                var controller = component.GetComponent(_controllerType);
                object graph;
                if (controller == null || !ReflectionUtil.TryRead(controller, "_graph", out graph) || graph == null) return false;

                object serializedValue;
                if (!ReflectionUtil.TryRead(graph, "_serializedGraph", out serializedValue)) return false;
                if (string.IsNullOrEmpty(serializedValue as string)) return false;
            }

            return true;
        }

        private object GetWorldObject(string npcId)
        {
            try { return _worldObjectGetter.Invoke(null, new object[] { npcId, true }); }
            catch { return null; }
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
    }
}

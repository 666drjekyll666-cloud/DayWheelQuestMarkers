using System;
using System.Collections.Generic;
using System.Reflection;

namespace CalendarQuestsPins
{
    internal static class SessionCacheRebinder
    {
        private static readonly FieldInfo QuestPlayerField = typeof(QuestRuleCache).GetField("_player", ReflectionUtil.AnyInstance);
        private static readonly FieldInfo CrossPlayerField = typeof(CrossOwnerRuleCache).GetField("_player", ReflectionUtil.AnyInstance);
        private static readonly FieldInfo CrossSaveField = typeof(CrossOwnerRuleCache).GetField("_save", ReflectionUtil.AnyInstance);
        private static readonly FieldInfo CrossKnownNpcCountField = typeof(CrossOwnerRuleCache).GetField("_knownNpcCount", ReflectionUtil.AnyInstance);

        internal static string BuildKnownNpcSignature(object save, out bool hasPeriodicNpc)
        {
            hasPeriodicNpc = false;
            var ids = new List<string>();
            object known;
            if (!ReflectionUtil.TryRead(save, "known_npcs", out known) || known == null) return string.Empty;
            var npcs = ReflectionUtil.EnumerateMember(known, "npcs");
            if (npcs == null) return string.Empty;
            foreach (var npc in npcs)
            {
                var id = ReflectionUtil.ReadString(npc, "npc_id");
                if (string.IsNullOrEmpty(id)) continue;
                ids.Add(id);
                if (IsPeriodicNpc(id)) hasPeriodicNpc = true;
            }
            ids.Sort(StringComparer.Ordinal);
            return string.Join("\u001f", ids.ToArray());
        }

        internal static bool TryRebind(QuestRuleCache rules, CrossOwnerRuleCache crossRules, object save, object mainGame)
        {
            if (rules == null || crossRules == null || save == null || mainGame == null) return false;
            if (QuestPlayerField == null || CrossPlayerField == null || CrossSaveField == null || CrossKnownNpcCountField == null) return false;

            object player;
            if (!ReflectionUtil.TryRead(mainGame, "player", out player) || player == null || !ReflectionUtil.IsUnityAlive(player)) return false;

            int knownNpcCount;
            var knownNpcMap = ReadKnownNpcs(save, out knownNpcCount);
            if (knownNpcMap == null) return false;

            foreach (var npc in rules.AllNpcRules)
            {
                if (npc == null || !ReflectionUtil.IsUnityAlive(npc.WorldObject)) return false;
                object knownNpc;
                if (!knownNpcMap.TryGetValue(npc.NpcId, out knownNpc)) return false;
                npc.KnownNpc = knownNpc;
            }

            foreach (var target in crossRules.AllTargets)
            {
                if (target == null || !ReflectionUtil.IsUnityAlive(target.WorldObject)) return false;
                foreach (var task in target.Tasks)
                {
                    if (task == null) return false;
                    object knownNpc;
                    if (!knownNpcMap.TryGetValue(task.OwnerNpcId, out knownNpc)) return false;
                    task.KnownNpc = knownNpc;
                }
            }

            try
            {
                QuestPlayerField.SetValue(rules, player);
                CrossPlayerField.SetValue(crossRules, player);
                CrossSaveField.SetValue(crossRules, save);
                CrossKnownNpcCountField.SetValue(crossRules, knownNpcCount);
            }
            catch
            {
                return false;
            }

            return !rules.NeedsRebuild() && !crossRules.NeedsRebuild();
        }

        private static Dictionary<string, object> ReadKnownNpcs(object save, out int count)
        {
            count = 0;
            var result = new Dictionary<string, object>(StringComparer.Ordinal);
            object known;
            if (!ReflectionUtil.TryRead(save, "known_npcs", out known) || known == null) return result;
            var npcs = ReflectionUtil.EnumerateMember(known, "npcs");
            if (npcs == null) return result;
            foreach (var npc in npcs)
            {
                count++;
                var id = ReflectionUtil.ReadString(npc, "npc_id");
                if (!string.IsNullOrEmpty(id)) result[id] = npc;
            }
            return result;
        }

        private static bool IsPeriodicNpc(string npcId)
        {
            return string.Equals(npcId, "npc_astrologer", StringComparison.Ordinal) ||
                   string.Equals(npcId, "npc_inquisitor", StringComparison.Ordinal) ||
                   string.Equals(npcId, "npc_cultist", StringComparison.Ordinal) ||
                   string.Equals(npcId, "npc_merchant", StringComparison.Ordinal) ||
                   string.Equals(npcId, "npc_actress", StringComparison.Ordinal) ||
                   string.Equals(npcId, "npc_bishop", StringComparison.Ordinal);
        }
    }
}

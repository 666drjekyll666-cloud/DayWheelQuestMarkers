using System;
using System.Collections;
using System.Reflection;
using BepInEx;
using BepInEx.Bootstrap;
using CalendarQuestsPins;
using UnityEngine;

namespace DayWheelAstrologerMarkerContributorsProbe
{
    [BepInPlugin(PluginGuid, PluginName, PluginVersion)]
    public sealed class AstrologerMarkerContributorsProbe : BaseUnityPlugin
    {
        private const string PluginGuid = "nikich.gyk.daywheel.astrologer-marker-contributors";
        private const string PluginName = "Day Wheel Quest Markers - Astrologer marker contributors";
        private const string PluginVersion = "0.1.1";
        private const string TargetPluginGuid = "nikich.gyk.calendarquestspins";
        private const string TargetNpcId = "npc_astrologer";

        private bool _done;
        private float _nextTry;
        private float _gameStartedAt = -1f;

        private void Awake()
        {
            _nextTry = Time.realtimeSinceStartup + 1f;
            Logger.LogInfo("ASTRO_MARKER_PROBE loaded version=" + PluginVersion + " readOnly=True target=DayWheelQuestMarkers-1.1.1");
        }

        private void Update()
        {
            if (_done || Time.realtimeSinceStartup < _nextTry) return;
            _nextTry = Time.realtimeSinceStartup + 0.5f;

            PluginInfo info;
            if (!Chainloader.PluginInfos.TryGetValue(TargetPluginGuid, out info) || info == null || info.Instance == null) return;

            var plugin = info.Instance;
            object ready;
            if (!ReflectionUtil.TryRead(plugin, "_cacheReady", out ready) || !(ready is bool) || !(bool)ready) return;

            object save;
            object mainGame;
            object rules;
            object reachability;
            object verified;
            if (!ReflectionUtil.TryRead(plugin, "_save", out save) || save == null ||
                !ReflectionUtil.TryRead(plugin, "_mainGame", out mainGame) || mainGame == null ||
                !ReflectionUtil.TryRead(plugin, "_rules", out rules) || rules == null ||
                !ReflectionUtil.TryRead(plugin, "_reachability", out reachability) || reachability == null ||
                !ReflectionUtil.TryRead(plugin, "_verifiedCompletionRules", out verified) || verified == null) return;

            object started;
            if (!TryReadStatic(mainGame.GetType(), "game_started", out started) || !(started is bool) || !(bool)started)
            {
                _gameStartedAt = -1f;
                return;
            }
            if (_gameStartedAt < 0f)
            {
                _gameStartedAt = Time.realtimeSinceStartup;
                return;
            }
            if (Time.realtimeSinceStartup - _gameStartedAt < 1.25f) return;

            try
            {
                Dump(plugin, save, mainGame, rules, reachability, verified);
                _done = true;
            }
            catch (Exception ex)
            {
                Logger.LogError("ASTRO_MARKER_FATAL " + ex);
                _done = true;
            }
        }

        private void Dump(object plugin, object save, object mainGame, object rules, object reachability, object verified)
        {
            object unlocked = null;
            object blacklisted = null;
            ReflectionUtil.TryRead(save, "unlocked_phrases", out unlocked);
            ReflectionUtil.TryRead(save, "black_list_of_phrases", out blacklisted);

            var target = FindTarget(rules, TargetNpcId);
            if (target == null)
            {
                Logger.LogWarning("ASTRO_MARKER_END targetFound=False");
                return;
            }

            var rulesType = rules.GetType();
            var reachType = reachability.GetType();
            var verifiedType = verified.GetType();

            var reachOwner = ReflectionUtil.FindMethod(reachType, "IsOwnerTaskActionable", 4, false);
            var reachTopic = ReflectionUtil.FindMethod(reachType, "IsTopicActionable", 4, false);
            var reachCross = ReflectionUtil.FindMethod(reachType, "IsCrossTaskActionable", 4, false);
            var reachAnswer = ReflectionUtil.FindMethod(reachType, "IsNavigationReachable", 4, false);
            var verifiedOwner = ReflectionUtil.FindMethod(verifiedType, "IsOwnerTaskActionable", 6, false);
            var promotedTopic = ReflectionUtil.FindMethod(verifiedType, "IsPromotedCompletionTopic", 2, true);
            var crossVisible = ReflectionUtil.FindMethod(rulesType, "IsCrossTaskVisible", 1, false);

            if (reachOwner == null || reachTopic == null || reachCross == null || reachAnswer == null ||
                verifiedOwner == null || promotedTopic == null || crossVisible == null)
            {
                Logger.LogWarning("ASTRO_MARKER_END bindings=False");
                return;
            }

            var expectedVisual = ReadAstrologerVisualCount(plugin);
            var contributors = 0;
            Logger.LogInfo("ASTRO_MARKER_BEGIN expectedVisualCount=" + expectedVisual + " npc=" + TargetNpcId);

            object knownNpc;
            ReflectionUtil.TryRead(target, "KnownNpc", out knownNpc);
            var tasks = ReflectionUtil.EnumerateMember(knownNpc, "tasks");
            if (tasks != null)
            {
                foreach (var task in tasks)
                {
                    if (task == null) continue;
                    var taskId = ReflectionUtil.ReadString(task, "id");
                    object stateRaw;
                    ReflectionUtil.TryRead(task, "state", out stateRaw);
                    var state = ToInt(stateRaw, -999);
                    if (state != 0) continue;

                    var nav = InvokeBool(reachOwner, reachability, target, taskId, unlocked, blacklisted);
                    var supplement = InvokeBool(verifiedOwner, verified, target, taskId, unlocked, blacklisted, reachability, mainGame);
                    var contributes = nav || supplement;
                    Logger.LogInfo("ASTRO_OWNER task=" + Safe(taskId) +
                                   " visible=True navigation=" + nav +
                                   " verifiedSupplement=" + supplement +
                                   " contributes=" + contributes);
                    if (contributes)
                    {
                        contributors++;
                        Logger.LogInfo("ASTRO_CONTRIB kind=owner id=" + Safe(taskId) +
                                       " navigation=" + nav + " verifiedSupplement=" + supplement);
                    }
                }
            }

            var topics = ReflectionUtil.EnumerateMember(target, "Topics");
            if (topics != null)
            {
                foreach (var topic in topics)
                {
                    if (topic == null) continue;
                    var answerId = ReflectionUtil.ReadString(topic, "AnswerId");
                    var promoted = InvokeBool(promotedTopic, null, TargetNpcId, answerId);
                    var actionable = InvokeBool(reachTopic, reachability, target, topic, unlocked, blacklisted);
                    var nav = InvokeBool(reachAnswer, reachability, TargetNpcId, answerId, unlocked, blacklisted);
                    var isUnlocked = ContainsString(unlocked, answerId);
                    var isBlacklisted = ContainsString(blacklisted, answerId);
                    var variants = DescribeVariants(topic);
                    var contributes = !promoted && actionable;
                    if (actionable || promoted || string.Equals(answerId, "@astrologer_diary", StringComparison.Ordinal) ||
                        string.Equals(answerId, "@refugees_s_ev_7_1_1", StringComparison.Ordinal))
                    {
                        Logger.LogInfo("ASTRO_TOPIC answer=" + Safe(answerId) +
                                       " promoted=" + promoted +
                                       " actionable=" + actionable +
                                       " navigation=" + nav +
                                       " unlocked=" + isUnlocked +
                                       " blacklisted=" + isBlacklisted +
                                       " contributes=" + contributes +
                                       " variants=" + variants);
                    }
                    if (contributes)
                    {
                        contributors++;
                        Logger.LogInfo("ASTRO_CONTRIB kind=topic id=" + Safe(answerId) + " variants=" + variants);
                    }
                }
            }

            var crossTasks = ReflectionUtil.EnumerateMember(target, "CrossTasks");
            if (crossTasks != null)
            {
                foreach (var cross in crossTasks)
                {
                    if (cross == null) continue;
                    var taskId = ReflectionUtil.ReadString(cross, "TaskId");
                    var ownerNpcId = ReflectionUtil.ReadString(cross, "OwnerNpcId");
                    var visible = InvokeBool(crossVisible, rules, cross);
                    var actionable = InvokeBool(reachCross, reachability, target, cross, unlocked, blacklisted);
                    var contributes = visible && actionable;
                    if (visible || actionable)
                    {
                        Logger.LogInfo("ASTRO_CROSS owner=" + Safe(ownerNpcId) +
                                       " task=" + Safe(taskId) +
                                       " visible=" + visible +
                                       " actionable=" + actionable +
                                       " contributes=" + contributes);
                    }
                    if (contributes)
                    {
                        contributors++;
                        Logger.LogInfo("ASTRO_CONTRIB kind=cross owner=" + Safe(ownerNpcId) + " task=" + Safe(taskId));
                    }
                }
            }

            Logger.LogInfo("ASTRO_MARKER_SUMMARY expectedVisualCount=" + expectedVisual + " contributorCount=" + contributors);
            Logger.LogInfo("ASTRO_MARKER_END");
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

        private static object FindTarget(object rules, string npcId)
        {
            object targetsValue;
            if (!ReflectionUtil.TryRead(rules, "AllTargets", out targetsValue)) return null;
            var targets = targetsValue as IEnumerable;
            if (targets == null) return null;
            foreach (var target in targets)
            {
                if (target != null && string.Equals(ReflectionUtil.ReadString(target, "NpcId"), npcId, StringComparison.Ordinal))
                    return target;
            }
            return null;
        }

        private static int ReadAstrologerVisualCount(object plugin)
        {
            object value;
            if (!ReflectionUtil.TryRead(plugin, "_currentSinMarkers", out value)) return -1;
            var array = value as Array;
            if (array == null || array.Length <= 1) return -1;
            var list = array.GetValue(1) as ICollection;
            return list == null ? -1 : list.Count;
        }

        private static string DescribeVariants(object topic)
        {
            var variants = ReflectionUtil.EnumerateMember(topic, "Variants");
            if (variants == null) return "<none>";
            var result = string.Empty;
            var index = 0;
            foreach (var variant in variants)
            {
                if (index > 0) result += "|";
                if (variant == null)
                {
                    result += index + ":null";
                    index++;
                    continue;
                }
                object unsupportedRaw;
                ReflectionUtil.TryRead(variant, "Unsupported", out unsupportedRaw);
                var unsupported = unsupportedRaw is bool && (bool)unsupportedRaw;
                object price;
                object gate;
                ReflectionUtil.TryRead(variant, "Price", out price);
                ReflectionUtil.TryRead(variant, "Lock", out gate);
                result += index + ":unsupported=" + unsupported + ",price=" + DescribeRequirement(price) + ",lock=" + DescribeRequirement(gate);
                index++;
            }
            return result.Length == 0 ? "<empty>" : result;
        }

        private static string DescribeRequirement(object requirement)
        {
            if (requirement == null) return "-";
            object value;
            ReflectionUtil.TryRead(requirement, "Value", out value);
            return Safe(ReflectionUtil.ReadString(requirement, "ResType")) + ":" +
                   Safe(ReflectionUtil.ReadString(requirement, "Id")) + ":" +
                   (value == null ? "?" : Convert.ToString(value, System.Globalization.CultureInfo.InvariantCulture)) +
                   ":zone=" + Safe(ReflectionUtil.ReadString(requirement, "AuthoritativeZoneId"));
        }

        private static bool InvokeBool(MethodInfo method, object instance, params object[] args)
        {
            if (method == null) return false;
            try
            {
                var value = method.Invoke(instance, args);
                return value is bool && (bool)value;
            }
            catch { return false; }
        }

        private static bool ContainsString(object collection, string value)
        {
            if (collection == null || string.IsNullOrEmpty(value)) return false;
            var enumerable = collection as IEnumerable;
            if (enumerable == null) return false;
            foreach (var item in enumerable)
                if (string.Equals(item as string, value, StringComparison.Ordinal)) return true;
            return false;
        }

        private static int ToInt(object value, int fallback)
        {
            if (value == null) return fallback;
            try { return Convert.ToInt32(value); }
            catch { return fallback; }
        }

        private static string Safe(string value)
        {
            return string.IsNullOrEmpty(value) ? "<empty>" : value.Replace("\r", "\\r").Replace("\n", "\\n");
        }
    }
}

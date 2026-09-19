using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using BepInEx;
using UnityEngine;

namespace DayWheelQuestMarkersResearch
{
    [BepInPlugin(PluginGuid, PluginName, PluginVersion)]
    public sealed class RuntimeWatchdogPlugin : BaseUnityPlugin
    {
        public const string PluginGuid = "nikich.gyk.daywheelquestmarkers.runtimewatchdog";
        public const string PluginName = "Day Wheel Quest Markers - Runtime Watchdog";
        public const string PluginVersion = "0.2.0";

        private const string ProductionTypeName = "CalendarQuestsPins.CalendarQuestsPinsPlugin";
        private const string ExpectedProductionVersion = "1.1.6";
        private const float TickSeconds = 5f;
        private const float StartupGraceSeconds = 12f;
        private const int PersistentMismatchTicks = 2;

        private static readonly string[] WeekdayNpcIds =
        {
            "npc_astrologer", "npc_inquisitor", "npc_cultist",
            "npc_merchant", "npc_actress", "npc_bishop"
        };

        private Type _productionType;
        private Component _production;
        private Type _mainGameType;
        private object _mainGame;
        private float _nextTick;
        private float _gameStartedAt = -1f;

        private string _failureCode;
        private string _failureDetail;
        private string _lastLoggedFailure;
        private int _runtimeInvalidTicks;
        private int _bindingMismatchTicks;
        private int _visualMismatchTicks;

        private string _liveFailureCode;
        private string _liveFailureDetail;
        private string _lastLoggedLiveFailure;
        private LiveDialogueCoverageWatchdog _liveDialogue;

        private GUIStyle _bangStyle;
        private GUIStyle _textStyle;

        private void Awake()
        {
            _mainGameType = FindType("MainGame");
            _productionType = FindType(ProductionTypeName);
            _nextTick = Time.realtimeSinceStartup + 0.5f;
            _liveDialogue = new LiveDialogueCoverageWatchdog(this);
            string liveFailure;
            if (!_liveDialogue.Initialize(out liveFailure))
                ReportLiveFailure("LIVE_WATCHDOG_INIT", liveFailure);
            Logger.LogInfo(PluginName + " " + PluginVersion + " loaded. Read-only; no save or production state mutation.");
        }

        private void OnDestroy()
        {
            if (_liveDialogue != null)
            {
                _liveDialogue.Dispose();
                _liveDialogue = null;
            }
        }

        private void Update()
        {
            if (Time.realtimeSinceStartup < _nextTick) return;
            _nextTick = Time.realtimeSinceStartup + TickSeconds;
            Tick();
        }

        private void Tick()
        {
            if (!EnsureProduction())
            {
                SetFailure("PRODUCTION_PLUGIN_MISSING",
                    "Accepted Day Wheel Quest Markers 1.1.6 component is not present.");
                return;
            }

            var version = ReadStaticString(_productionType, "PluginVersion");
            if (!string.Equals(version, ExpectedProductionVersion, StringComparison.Ordinal))
            {
                SetFailure("PRODUCTION_VERSION",
                    "Expected production " + ExpectedProductionVersion + ", observed " + Safe(version) + ".");
                return;
            }

            if (!EnsureMainGame() || !IsGameStarted())
            {
                _gameStartedAt = -1f;
                ClearTransientCounters();
                ClearFailure();
                return;
            }

            if (_gameStartedAt < 0f) _gameStartedAt = Time.realtimeSinceStartup;
            if (Time.realtimeSinceStartup - _gameStartedAt < StartupGraceSeconds)
            {
                ClearFailure();
                return;
            }

            string detail;
            if (!CheckCanonicalStructure(out detail))
            {
                SetFailure("STRUCTURE_CONTRACT", detail);
                return;
            }

            if (!CheckRuntimeValidity(out detail))
            {
                _runtimeInvalidTicks++;
                if (_runtimeInvalidTicks >= PersistentMismatchTicks)
                {
                    SetFailure("RUNTIME_BINDING_INVALID", detail);
                    return;
                }
            }
            else _runtimeInvalidTicks = 0;

            if (!CheckKnownNpcBindings(out detail))
            {
                _bindingMismatchTicks++;
                if (_bindingMismatchTicks >= PersistentMismatchTicks)
                {
                    SetFailure("KNOWN_NPC_BINDING", detail);
                    return;
                }
            }
            else _bindingMismatchTicks = 0;

            if (!CheckVisualParity(out detail))
            {
                _visualMismatchTicks++;
                if (_visualMismatchTicks >= PersistentMismatchTicks)
                {
                    SetFailure("MARKER_VISUAL_PARITY", detail);
                    return;
                }
            }
            else _visualMismatchTicks = 0;

            if (_runtimeInvalidTicks == 0 && _bindingMismatchTicks == 0 && _visualMismatchTicks == 0)
                ClearFailure();
        }

        private bool EnsureProduction()
        {
            if (_production != null && _production) return true;
            if (_productionType == null) _productionType = FindType(ProductionTypeName);
            if (_productionType == null) return false;

            var objects = Resources.FindObjectsOfTypeAll(_productionType);
            for (var i = 0; i < objects.Length; i++)
            {
                var component = objects[i] as Component;
                if (component == null) continue;
                _production = component;
                return true;
            }
            return false;
        }

        private bool EnsureMainGame()
        {
            if (_mainGame != null && IsUnityAlive(_mainGame)) return true;
            if (_mainGameType == null) _mainGameType = FindType("MainGame");
            if (_mainGameType == null) return false;

            var objects = Resources.FindObjectsOfTypeAll(_mainGameType);
            for (var i = 0; i < objects.Length; i++)
            {
                if (objects[i] == null) continue;
                _mainGame = objects[i];
                return true;
            }
            return false;
        }

        private bool IsGameStarted()
        {
            object value;
            return TryReadStatic(_mainGameType, "game_started", out value) && value is bool && (bool)value;
        }

        private bool CheckCanonicalStructure(out string detail)
        {
            detail = null;
            var rules = ReadMember(_production, "_rules");
            var manifest = ReadMember(_production, "_manifest");
            if (rules == null || manifest == null)
            {
                detail = "Production rule cache or manifest is null.";
                return false;
            }

            var failures = new List<string>();
            ExpectInt(rules, "OwnerSupportedRuleCount", 75, failures);
            ExpectInt(rules, "OwnerUnsupportedRuleCount", 6, failures);
            ExpectInt(rules, "CrossTaskCount", 8, failures);
            ExpectInt(rules, "CrossSupportedRuleCount", 6, failures);
            ExpectInt(rules, "CrossUnsupportedRuleCount", 0, failures);
            ExpectInt(rules, "OneShotTopicCount", 65, failures);
            ExpectInt(rules, "OneShotSupportedRuleCount", 64, failures);
            ExpectInt(rules, "OneShotUnsupportedRuleCount", 1, failures);

            ExpectInt(manifest, "NonAtUniqueCount", 77, failures);
            ExpectInt(manifest, "NonAtExactSelfCount", 19, failures);
            ExpectInt(manifest, "NonAtTopicCount", 6, failures);
            ExpectInt(manifest, "NonAtSupportedVariantCount", 6, failures);
            ExpectInt(manifest, "NonAtUnsupportedVariantCount", 0, failures);
            ExpectInt(manifest, "NonAtCompletionExcludedCount", 9, failures);
            ExpectInt(manifest, "AncestorOwnerCandidateCount", 6, failures);
            ExpectInt(manifest, "AncestorTaskExcludedCount", 2, failures);
            ExpectInt(manifest, "AncestorTopicCount", 4, failures);
            ExpectInt(manifest, "AncestorSupportedVariantCount", 4, failures);
            ExpectInt(manifest, "AncestorUnsupportedVariantCount", 0, failures);
            ExpectInt(manifest, "NavigationAnswerCount", 210, failures);
            ExpectInt(manifest, "NavigationPathCount", 270, failures);
            ExpectInt(manifest, "NavigationPredicateCount", 151, failures);
            ExpectInt(manifest, "NavigationUnsupportedPathCount", 0, failures);

            if (failures.Count == 0) return true;
            detail = string.Join("; ", failures.ToArray());
            return false;
        }

        private bool CheckRuntimeValidity(out string detail)
        {
            detail = null;
            var cacheReady = ReadBool(_production, "_cacheReady");
            if (!cacheReady.HasValue || !cacheReady.Value)
            {
                detail = "Production _cacheReady is false after startup grace.";
                return false;
            }

            var manifest = ReadMember(_production, "_manifest");
            if (manifest == null)
            {
                detail = "Production manifest missing.";
                return false;
            }

            MethodInfo method = null;
            foreach (var candidate in manifest.GetType().GetMethods(
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic))
            {
                if (!string.Equals(candidate.Name, "IsRuntimeValid", StringComparison.Ordinal)) continue;
                if (candidate.GetParameters().Length != 1) continue;
                method = candidate;
                break;
            }
            if (method == null)
            {
                detail = "PersistentRuleManifest.IsRuntimeValid could not be resolved.";
                return false;
            }

            try
            {
                var result = method.Invoke(manifest, new[] { _mainGame });
                if (result is bool && (bool)result) return true;
                detail = "PersistentRuleManifest.IsRuntimeValid returned false.";
                return false;
            }
            catch (Exception ex)
            {
                detail = "IsRuntimeValid threw " + ex.GetType().Name + ".";
                return false;
            }
        }

        private bool CheckKnownNpcBindings(out string detail)
        {
            detail = null;
            var save = ReadMember(_production, "_save");
            var rules = ReadMember(_production, "_rules");
            if (save == null || rules == null)
            {
                detail = "Production save/rules unavailable.";
                return false;
            }

            var known = ReadMember(save, "known_npcs");
            var knownNpcs = EnumerateMember(known, "npcs");
            if (knownNpcs == null)
            {
                detail = "save.known_npcs.npcs unavailable.";
                return false;
            }

            var expectedKnown = new HashSet<string>(StringComparer.Ordinal);
            foreach (var npc in knownNpcs)
            {
                var id = ReadString(npc, "npc_id");
                if (IsWeekdayNpc(id)) expectedKnown.Add(id);
            }

            var actualBound = new HashSet<string>(StringComparer.Ordinal);
            var allTargets = ReadMember(rules, "AllTargets") as IEnumerable;
            if (allTargets == null)
            {
                detail = "WeekdayInteractionRuleCache.AllTargets unavailable.";
                return false;
            }

            foreach (var target in allTargets)
            {
                if (target == null) continue;
                var id = ReadString(target, "NpcId");
                var bound = ReadMember(target, "KnownNpc");
                if (IsWeekdayNpc(id) && bound != null) actualBound.Add(id);
            }

            if (expectedKnown.SetEquals(actualBound)) return true;
            detail = "Known weekday NPC set=" + Join(expectedKnown) + ", bound target set=" + Join(actualBound) + ".";
            return false;
        }

        private bool CheckVisualParity(out string detail)
        {
            detail = null;
            var desiredRaw = ReadMember(_production, "_currentSinMarkers") as Array;
            var markers = ReadMember(_production, "_markers");
            if (desiredRaw == null || markers == null)
            {
                detail = "Desired marker array or CalendarMarkers missing.";
                return false;
            }

            var desiredCounts = new int[7];
            var desiredStyles = new List<int>[7];
            for (var i = 0; i < desiredStyles.Length; i++) desiredStyles[i] = new List<int>();

            for (var sin = 1; sin <= 6; sin++)
            {
                var list = desiredRaw.GetValue(sin) as IEnumerable;
                if (list == null) continue;
                foreach (var style in list)
                {
                    desiredCounts[sin]++;
                    desiredStyles[sin].Add(Convert.ToInt32(style));
                }
            }

            var slotMarkers = ReadMember(markers, "_slotMarkers") as Array;
            var icons = ReadMember(markers, "_icons") as Array;
            if (slotMarkers == null || icons == null || slotMarkers.Length != 6 || icons.Length != 6)
            {
                if (Total(desiredCounts) == 0) return true;
                detail = "CalendarMarkers slot/icon arrays unavailable while markers are desired.";
                return false;
            }

            var actualCounts = new int[7];
            var actualStyles = new List<int>[7];
            for (var i = 0; i < actualStyles.Length; i++) actualStyles[i] = new List<int>();

            for (var slot = 0; slot < 6; slot++)
            {
                var icon = icons.GetValue(slot);
                var sin = ReadInt(icon, "_sin_type");
                if (!sin.HasValue || sin.Value < 1 || sin.Value > 6) continue;

                var visuals = slotMarkers.GetValue(slot) as IEnumerable;
                if (visuals == null) continue;
                foreach (var visual in visuals)
                {
                    if (visual == null) continue;
                    var go = ReadMember(visual, "GameObject") as GameObject;
                    if (go == null || !go.activeSelf) continue;
                    var widget = ReadMember(visual, "Widget") as Component;
                    if (widget == null)
                    {
                        detail = "Active marker has no widget at sinType=" + sin.Value + ".";
                        return false;
                    }
                    actualCounts[sin.Value]++;
                    var style = ReadMember(visual, "Style");
                    if (style != null) actualStyles[sin.Value].Add(Convert.ToInt32(style));
                }
            }

            for (var sin = 1; sin <= 6; sin++)
            {
                if (desiredCounts[sin] != actualCounts[sin])
                {
                    detail = "sinType=" + sin + " desired=" + desiredCounts[sin] + " activeVisuals=" + actualCounts[sin] + ".";
                    return false;
                }
                if (!SameSequence(desiredStyles[sin], actualStyles[sin]))
                {
                    detail = "sinType=" + sin + " marker styles differ. desired=" +
                             JoinInts(desiredStyles[sin]) + " actual=" + JoinInts(actualStyles[sin]) + ".";
                    return false;
                }
            }
            return true;
        }

        internal void ReportLiveFailure(string code, string detail)
        {
            _liveFailureCode = code;
            _liveFailureDetail = detail ?? "<no detail>";
            var signature = code + "|" + _liveFailureDetail;
            if (string.Equals(signature, _lastLoggedLiveFailure, StringComparison.Ordinal)) return;
            _lastLoggedLiveFailure = signature;
            Logger.LogError("WATCHDOG_FAIL code=" + code + " detail=" + _liveFailureDetail);
        }

        internal void LogLiveInfo(string message)
        {
            if (!string.IsNullOrEmpty(message)) Logger.LogInfo(message);
        }

        private void SetFailure(string code, string detail)
        {
            _failureCode = code;
            _failureDetail = detail ?? "<no detail>";
            var signature = code + "|" + _failureDetail;
            if (string.Equals(signature, _lastLoggedFailure, StringComparison.Ordinal)) return;
            _lastLoggedFailure = signature;
            Logger.LogError("WATCHDOG_FAIL code=" + code + " detail=" + _failureDetail);
        }

        private void ClearFailure()
        {
            if (_failureCode != null)
                Logger.LogInfo("WATCHDOG_RECOVERED previous=" + _failureCode);
            _failureCode = null;
            _failureDetail = null;
            _lastLoggedFailure = null;
        }

        private void ClearTransientCounters()
        {
            _runtimeInvalidTicks = 0;
            _bindingMismatchTicks = 0;
            _visualMismatchTicks = 0;
        }

        private void OnGUI()
        {
            var displayCode = !string.IsNullOrEmpty(_liveFailureCode) ? _liveFailureCode : _failureCode;
            if (string.IsNullOrEmpty(displayCode)) return;
            if (_bangStyle == null)
            {
                _bangStyle = new GUIStyle(GUI.skin.label)
                {
                    fontSize = 52,
                    fontStyle = FontStyle.Bold,
                    alignment = TextAnchor.MiddleCenter
                };
                _bangStyle.normal.textColor = Color.red;
                _textStyle = new GUIStyle(GUI.skin.label)
                {
                    fontSize = 14,
                    fontStyle = FontStyle.Bold,
                    alignment = TextAnchor.UpperRight,
                    wordWrap = false
                };
                _textStyle.normal.textColor = Color.red;
            }

            var bangRect = new Rect(Screen.width - 82f, 12f, 64f, 64f);
            GUI.Label(bangRect, "!", _bangStyle);
            var textRect = new Rect(Mathf.Max(8f, Screen.width - 620f), 70f, 600f, 44f);
            GUI.Label(textRect, "DAY WHEEL WATCHDOG FAIL: " + displayCode, _textStyle);
        }

        private static void ExpectInt(object target, string name, int expected, List<string> failures)
        {
            var observed = ReadInt(target, name);
            if (!observed.HasValue || observed.Value != expected)
                failures.Add(name + "=" + (observed.HasValue ? observed.Value.ToString() : "<missing>") +
                             " expected=" + expected);
        }

        private static bool SameSequence(List<int> a, List<int> b)
        {
            if (a == null || b == null || a.Count != b.Count) return false;
            for (var i = 0; i < a.Count; i++) if (a[i] != b[i]) return false;
            return true;
        }

        private static int Total(int[] values)
        {
            var total = 0;
            for (var i = 0; i < values.Length; i++) total += values[i];
            return total;
        }

        private static bool IsWeekdayNpc(string id)
        {
            if (string.IsNullOrEmpty(id)) return false;
            for (var i = 0; i < WeekdayNpcIds.Length; i++)
                if (string.Equals(id, WeekdayNpcIds[i], StringComparison.Ordinal)) return true;
            return false;
        }

        private static string Join(IEnumerable<string> values)
        {
            var list = new List<string>();
            foreach (var value in values) list.Add(value);
            list.Sort(StringComparer.Ordinal);
            return "[" + string.Join(",", list.ToArray()) + "]";
        }

        private static string JoinInts(List<int> values)
        {
            if (values == null || values.Count == 0) return "[]";
            var parts = new string[values.Count];
            for (var i = 0; i < values.Count; i++) parts[i] = values[i].ToString();
            return "[" + string.Join(",", parts) + "]";
        }

        private static string Safe(string value)
        {
            return string.IsNullOrEmpty(value) ? "<null>" : value;
        }

        private static string ReadStaticString(Type type, string name)
        {
            if (type == null) return null;
            var field = type.GetField(name, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static);
            if (field == null) return null;
            try { return field.GetValue(null) as string; }
            catch { return null; }
        }

        private static bool? ReadBool(object target, string name)
        {
            var value = ReadMember(target, name);
            return value is bool ? (bool?)value : null;
        }

        private static int? ReadInt(object target, string name)
        {
            var value = ReadMember(target, name);
            if (value == null) return null;
            try { return Convert.ToInt32(value); }
            catch { return null; }
        }

        private static string ReadString(object target, string name)
        {
            var value = ReadMember(target, name);
            return value == null ? null : value.ToString();
        }

        private static object ReadMember(object target, string name)
        {
            if (target == null || string.IsNullOrEmpty(name)) return null;
            var type = target.GetType();
            var field = type.GetField(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (field != null)
            {
                try { return field.GetValue(target); } catch { return null; }
            }
            var property = type.GetProperty(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (property != null && property.GetIndexParameters().Length == 0)
            {
                try { return property.GetValue(target, null); } catch { return null; }
            }
            return null;
        }

        private static IEnumerable EnumerateMember(object target, string name)
        {
            return ReadMember(target, name) as IEnumerable;
        }

        private static bool TryReadStatic(Type type, string name, out object value)
        {
            value = null;
            if (type == null) return false;
            var field = type.GetField(name, BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
            if (field != null)
            {
                try { value = field.GetValue(null); return true; } catch { return false; }
            }
            var property = type.GetProperty(name, BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
            if (property != null && property.GetIndexParameters().Length == 0)
            {
                try { value = property.GetValue(null, null); return true; } catch { return false; }
            }
            return false;
        }

        private static Type FindType(string fullOrShortName)
        {
            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                try
                {
                    var exact = assembly.GetType(fullOrShortName, false);
                    if (exact != null) return exact;
                    foreach (var type in assembly.GetTypes())
                        if (type != null && string.Equals(type.Name, fullOrShortName, StringComparison.Ordinal))
                            return type;
                }
                catch { }
            }
            return null;
        }

        private static bool IsUnityAlive(object value)
        {
            var unity = value as UnityEngine.Object;
            return unity == null ? value != null : unity;
        }
    }
}

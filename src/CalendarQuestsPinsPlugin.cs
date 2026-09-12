using System;
using System.Collections.Generic;
using BepInEx;
using UnityEngine;

namespace CalendarQuestsPins
{
    [BepInPlugin(PluginGuid, PluginName, PluginVersion)]
    public sealed class CalendarQuestsPinsPlugin : BaseUnityPlugin
    {
        // Stable legacy GUID retained across the public product rename so upgrades stay on the same plugin identity.
        public const string PluginGuid = "nikich.gyk.calendarquestspins";
        public const string PluginName = "Day Wheel Quest Markers";
        public const string PluginVersion = "1.0.24";

        private const float TickSeconds = 1f;
        private const float StructureCheckSeconds = 30f;
        private Type _mainGameType;
        private object _mainGame;
        private object _save;
        private object _prewarmAttemptedSave;
        private float _nextTick;
        private float _nextStructureCheck;
        private bool _cacheReady;
        private bool _waitingForPeriodicNpc;
        private bool _loggedReady;
        private bool _prewarmedDuringLoading;
        private string _cachedKnownNpcSignature;
        private string _currentKnownNpcSignature;
        private readonly List<MarkerStyle>[] _currentSinMarkers = new List<MarkerStyle>[7];
        private WeekdayInteractionRuleCache _rules;
        private CalendarMarkers _markers;
        private LoadingCachePrewarmGate _prewarmGate;

        private void Awake()
        {
            _mainGameType = ReflectionUtil.FindType("MainGame");
            _rules = new WeekdayInteractionRuleCache();
            _markers = new CalendarMarkers();
            _prewarmGate = new LoadingCachePrewarmGate();
            for (var i = 0; i < _currentSinMarkers.Length; i++)
                _currentSinMarkers[i] = new List<MarkerStyle>(4);
            _nextTick = Time.realtimeSinceStartup + 0.5f;
            _nextStructureCheck = Time.realtimeSinceStartup + StructureCheckSeconds;
            Logger.LogInfo(PluginName + " " + PluginVersion + " loaded.");
        }

        private void Update()
        {
            if (Time.realtimeSinceStartup < _nextTick) return;
            _nextTick = Time.realtimeSinceStartup + TickSeconds;

            // Accepted runtime probes show a several-second window where the loaded save, player and periodic-NPC
            // graphs already exist while the loading screen is still active. Build the one structural cache there.
            if (TryPrewarmDuringLoading()) return;

            Tick();
        }

        private void OnDestroy()
        {
            if (_markers != null) _markers.Dispose();
            if (_rules != null) _rules.Clear();
        }

        private bool TryPrewarmDuringLoading()
        {
            if (!EnsureMainGameReference()) return false;

            object started;
            if (!TryReadStatic(_mainGameType, "game_started", out started) || !(started is bool) || (bool)started) return false;
            object starting;
            if (!TryReadStatic(_mainGameType, "game_starting", out starting) || !(starting is bool) || (bool)starting) return false;

            object candidateSave;
            if (!ReflectionUtil.TryRead(_mainGame, "save", out candidateSave) || candidateSave == null) return false;
            if (ReferenceEquals(candidateSave, _prewarmAttemptedSave)) return false;
            if (!HasLoadedCollections(candidateSave)) return false;

            bool hasPeriodicNpc;
            var signature = WeekdayInteractionRuleCache.BuildKnownNpcSignature(candidateSave, out hasPeriodicNpc);
            if (!hasPeriodicNpc || string.IsNullOrEmpty(signature)) return false;
            if (_prewarmGate == null || !_prewarmGate.IsReady(_mainGame)) return false;

            if (_markers != null) _markers.TryPrewarmNativeSprites();
            _prewarmAttemptedSave = candidateSave;

            if (!string.IsNullOrEmpty(_cachedKnownNpcSignature) &&
                string.Equals(_cachedKnownNpcSignature, signature, StringComparison.Ordinal) &&
                _rules.TryRebind(candidateSave, _mainGame))
            {
                _save = candidateSave;
                _cacheReady = true;
                _waitingForPeriodicNpc = false;
                _currentKnownNpcSignature = signature;
                _prewarmedDuringLoading = true;
                _nextStructureCheck = Time.realtimeSinceStartup + StructureCheckSeconds;
                _loggedReady = false;
                Logger.LogInfo("Weekday interaction cache rebound during loading; no graph parse required.");
                return true;
            }

            _rules.Clear();
            var sw = System.Diagnostics.Stopwatch.StartNew();
            var ready = _rules.Build(candidateSave, _mainGame);
            sw.Stop();

            if (!ready)
            {
                _rules.Clear();
                _cacheReady = false;
                _cachedKnownNpcSignature = null;
                _currentKnownNpcSignature = null;
                _prewarmedDuringLoading = false;
                Logger.LogWarning("Loading-screen weekday-interaction prewarm could not complete; gameplay-safe fallback will be used.");
                return true;
            }

            _save = candidateSave;
            _cacheReady = true;
            _waitingForPeriodicNpc = false;
            _currentKnownNpcSignature = signature;
            _cachedKnownNpcSignature = signature;
            _prewarmedDuringLoading = true;
            _nextStructureCheck = Time.realtimeSinceStartup + StructureCheckSeconds;
            _loggedReady = false;

            Logger.LogInfo("Weekday interaction cache prewarmed behind loading screen in " + sw.Elapsed.TotalMilliseconds.ToString("F2") +
                           " ms. owner supported=" + _rules.OwnerSupportedRuleCount +
                           ", cross-owner tasks=" + _rules.CrossTaskCount +
                           ", one-shot topics=" + _rules.OneShotTopicCount + ".");
            return true;
        }

        private void Tick()
        {
            if (!EnsureRuntime())
            {
                HideMarkers();
                return;
            }

            if (_waitingForPeriodicNpc && HasKnownPeriodicNpc(_save))
            {
                _waitingForPeriodicNpc = false;
                _cacheReady = false;
                _currentKnownNpcSignature = null;
            }

            if (string.IsNullOrEmpty(_currentKnownNpcSignature))
            {
                bool hasPeriodicNpc;
                _currentKnownNpcSignature = WeekdayInteractionRuleCache.BuildKnownNpcSignature(_save, out hasPeriodicNpc);
                if (!hasPeriodicNpc)
                {
                    // A fresh save can legitimately know only non-periodic characters. Preserve any existing
                    // parsed cache so returning to a developed save in the same process can use the cheap rebind path.
                    _cacheReady = true;
                    _waitingForPeriodicNpc = true;
                    _nextStructureCheck = Time.realtimeSinceStartup + StructureCheckSeconds;
                    _loggedReady = false;
                }
            }

            if (!_waitingForPeriodicNpc && !_cacheReady &&
                !string.IsNullOrEmpty(_cachedKnownNpcSignature) &&
                string.Equals(_cachedKnownNpcSignature, _currentKnownNpcSignature, StringComparison.Ordinal) &&
                _rules.TryRebind(_save, _mainGame))
            {
                _cacheReady = true;
                _nextStructureCheck = Time.realtimeSinceStartup + StructureCheckSeconds;
                _loggedReady = false;
            }

            if (!_waitingForPeriodicNpc && (!_cacheReady || _rules.NeedsRebuild()))
            {
                if (!RebuildForCurrentSave()) return;
            }
            else if (!_waitingForPeriodicNpc && Time.realtimeSinceStartup >= _nextStructureCheck)
            {
                _nextStructureCheck = Time.realtimeSinceStartup + StructureCheckSeconds;
                bool hasPeriodicNpc;
                var liveSignature = WeekdayInteractionRuleCache.BuildKnownNpcSignature(_save, out hasPeriodicNpc);
                if (!hasPeriodicNpc)
                {
                    _waitingForPeriodicNpc = true;
                    _cacheReady = true;
                    _currentKnownNpcSignature = liveSignature;
                    _cachedKnownNpcSignature = null;
                    _loggedReady = false;
                }
                else if (!string.Equals(liveSignature, _currentKnownNpcSignature, StringComparison.Ordinal) || _rules.NeedsRebuild())
                {
                    _currentKnownNpcSignature = liveSignature;
                    _cachedKnownNpcSignature = null;
                    _cacheReady = false;
                    if (!RebuildForCurrentSave()) return;
                }
            }

            if (_waitingForPeriodicNpc)
            {
                HideMarkers();
                LogReady();
                return;
            }

            object unlocked = null;
            object blacklisted = null;
            ReflectionUtil.TryRead(_save, "unlocked_phrases", out unlocked);
            ReflectionUtil.TryRead(_save, "black_list_of_phrases", out blacklisted);

            ClearMarkerSets();
            foreach (var target in _rules.AllTargets)
            {
                var sinTypeValue = GetSinTypeValue(target.NpcId);
                if (sinTypeValue <= 0 || sinTypeValue >= _currentSinMarkers.Length) continue;

                if (target.KnownNpc != null)
                {
                    var tasks = ReflectionUtil.EnumerateMember(target.KnownNpc, "tasks");
                    if (tasks != null)
                    {
                        foreach (var task in tasks)
                        {
                            string taskId;
                            if (!WeekdayInteractionRuleCache.IsVisibleTask(task, out taskId)) continue;
                            if (!_rules.IsOwnerTaskActionable(target, taskId, unlocked, blacklisted)) continue;
                            AddMarker(sinTypeValue, GetMarkerStyle(taskId));
                        }
                    }
                }

                for (var i = 0; i < target.CrossTasks.Count; i++)
                {
                    var task = target.CrossTasks[i];
                    if (!_rules.IsCrossTaskVisible(task)) continue;
                    if (!_rules.IsCrossTaskActionable(task, unlocked, blacklisted)) continue;
                    AddMarker(sinTypeValue, GetMarkerStyle(task.TaskId));
                }

                // One-shot topics include the bridge/intermediate topics that 1.0.23 still handled through
                // explicit supplemental classes. Exact self-consumption plus native gates is now canonical.
                for (var i = 0; i < target.Topics.Count; i++)
                {
                    var topic = target.Topics[i];
                    if (!_rules.IsTopicActionable(topic, unlocked, blacklisted)) continue;
                    AddMarker(sinTypeValue, MarkerStyle.Base);
                }
            }

            if (!HasAnyMarkers())
            {
                if (_markers != null) _markers.HideAll();
                LogReady();
                return;
            }

            if (!_markers.EnsureAttached()) return;
            _markers.ApplyMarkerSets(_currentSinMarkers);
            LogReady();
        }

        private bool RebuildForCurrentSave()
        {
            if (!HasKnownPeriodicNpc(_save))
            {
                _cacheReady = true;
                _waitingForPeriodicNpc = true;
                _cachedKnownNpcSignature = null;
                _nextStructureCheck = Time.realtimeSinceStartup + StructureCheckSeconds;
                _loggedReady = false;
                HideMarkers();
                return true;
            }

            _cacheReady = _rules.Build(_save, _mainGame);
            _nextStructureCheck = Time.realtimeSinceStartup + StructureCheckSeconds;
            if (!_cacheReady)
            {
                _cachedKnownNpcSignature = null;
                HideMarkers();
                return false;
            }

            bool hasPeriodicNpc;
            _currentKnownNpcSignature = WeekdayInteractionRuleCache.BuildKnownNpcSignature(_save, out hasPeriodicNpc);
            _cachedKnownNpcSignature = hasPeriodicNpc ? _currentKnownNpcSignature : null;
            _waitingForPeriodicNpc = !hasPeriodicNpc;
            _loggedReady = false;
            return true;
        }

        private void AddMarker(int sinTypeValue, MarkerStyle style)
        {
            if (style == MarkerStyle.None || sinTypeValue <= 0 || sinTypeValue >= _currentSinMarkers.Length) return;
            _currentSinMarkers[sinTypeValue].Add(style);
        }

        private void ClearMarkerSets()
        {
            for (var i = 0; i < _currentSinMarkers.Length; i++)
                _currentSinMarkers[i].Clear();
        }

        private bool HasAnyMarkers()
        {
            for (var i = 1; i < _currentSinMarkers.Length; i++)
                if (_currentSinMarkers[i].Count > 0) return true;
            return false;
        }

        private void LogReady()
        {
            if (_loggedReady) return;
            _loggedReady = true;
            if (_waitingForPeriodicNpc)
            {
                Logger.LogInfo("Ready. Unified cache: owner supported=0, owner unsupported=0, cross-owner tasks=0, cross-owner supported=0, cross-owner unsupported=0, one-shot topics=0, one-shot supported=0, one-shot unsupported=0.");
                return;
            }

            Logger.LogInfo("Ready. Unified cache: owner supported=" + _rules.OwnerSupportedRuleCount +
                           ", owner unsupported=" + _rules.OwnerUnsupportedRuleCount +
                           ", cross-owner tasks=" + _rules.CrossTaskCount +
                           ", cross-owner supported=" + _rules.CrossSupportedRuleCount +
                           ", cross-owner unsupported=" + _rules.CrossUnsupportedRuleCount +
                           ", one-shot topics=" + _rules.OneShotTopicCount +
                           ", one-shot supported=" + _rules.OneShotSupportedRuleCount +
                           ", one-shot unsupported=" + _rules.OneShotUnsupportedRuleCount + ".");
        }

        private bool EnsureRuntime()
        {
            if (!EnsureMainGameReference()) return false;

            object newSave;
            if (!ReflectionUtil.TryRead(_mainGame, "save", out newSave) || newSave == null) return false;
            if (!ReferenceEquals(newSave, _save))
            {
                _save = newSave;
                _cacheReady = false;
                _waitingForPeriodicNpc = false;
                _currentKnownNpcSignature = null;
                _prewarmedDuringLoading = false;
                _nextStructureCheck = Time.realtimeSinceStartup + StructureCheckSeconds;
                HideMarkers();
            }

            object started;
            if (!TryReadStatic(_mainGameType, "game_started", out started) || !(started is bool) || !(bool)started) return false;
            object starting;
            if (TryReadStatic(_mainGameType, "game_starting", out starting) && starting is bool && (bool)starting) return false;
            if (!HasLoadedCollections(_save)) return false;

            if (_prewarmedDuringLoading)
            {
                bool hasPeriodicNpc;
                var liveSignature = WeekdayInteractionRuleCache.BuildKnownNpcSignature(_save, out hasPeriodicNpc);
                var valid = hasPeriodicNpc &&
                            !string.IsNullOrEmpty(liveSignature) &&
                            string.Equals(liveSignature, _cachedKnownNpcSignature, StringComparison.Ordinal) &&
                            _rules.TryRebind(_save, _mainGame);

                _prewarmedDuringLoading = false;
                _currentKnownNpcSignature = liveSignature;
                if (valid)
                {
                    _cacheReady = true;
                    _nextStructureCheck = Time.realtimeSinceStartup + StructureCheckSeconds;
                }
                else
                {
                    _cacheReady = false;
                    _cachedKnownNpcSignature = null;
                    Logger.LogWarning("Loading-screen weekday-interaction prewarm did not survive final runtime validation; rebuilding safely after load.");
                }
            }

            return true;
        }

        private bool EnsureMainGameReference()
        {
            if (_mainGame == null || !ReflectionUtil.IsUnityAlive(_mainGame))
                _mainGame = ReflectionUtil.FindActiveUnityInstance(_mainGameType);
            return _mainGame != null;
        }

        private static bool HasLoadedCollections(object save)
        {
            object known;
            if (!ReflectionUtil.TryRead(save, "known_npcs", out known) || known == null) return false;
            var npcs = ReflectionUtil.EnumerateMember(known, "npcs");
            if (npcs == null) return false;
            foreach (var ignored in npcs) return true;
            return false;
        }

        private static bool HasKnownPeriodicNpc(object save)
        {
            object known;
            if (!ReflectionUtil.TryRead(save, "known_npcs", out known) || known == null) return false;
            var npcs = ReflectionUtil.EnumerateMember(known, "npcs");
            if (npcs == null) return false;
            foreach (var npc in npcs)
            {
                var npcId = ReflectionUtil.ReadString(npc, "npc_id");
                if (GetSinTypeValue(npcId) > 0) return true;
            }
            return false;
        }

        private static int GetSinTypeValue(string npcId)
        {
            if (string.Equals(npcId, "npc_astrologer", StringComparison.Ordinal)) return 1; // Sloth
            if (string.Equals(npcId, "npc_inquisitor", StringComparison.Ordinal)) return 2; // Wrath
            if (string.Equals(npcId, "npc_cultist", StringComparison.Ordinal)) return 3; // Envy
            if (string.Equals(npcId, "npc_merchant", StringComparison.Ordinal)) return 4; // Gluttony
            if (string.Equals(npcId, "npc_actress", StringComparison.Ordinal)) return 5; // Lust
            if (string.Equals(npcId, "npc_bishop", StringComparison.Ordinal)) return 6; // Pride
            return -1;
        }

        private static MarkerStyle GetMarkerStyle(string taskId)
        {
            if (string.IsNullOrEmpty(taskId)) return MarkerStyle.Base;
            if (taskId.StartsWith("dlc_stories_", StringComparison.Ordinal)) return MarkerStyle.Stories;
            if (taskId.StartsWith("dlc_refugees", StringComparison.Ordinal) || taskId.StartsWith("s_ev", StringComparison.Ordinal)) return MarkerStyle.Violet;
            if (taskId.StartsWith("dlc_souls", StringComparison.Ordinal)) return MarkerStyle.Souls;
            return MarkerStyle.Base;
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
                if (prop != null && prop.GetIndexParameters().Length == 0) { value = prop.GetValue(null, null); return true; }
            }
            catch { }
            return false;
        }

        private void HideMarkers()
        {
            ClearMarkerSets();
            if (_markers != null) _markers.HideAll();
        }
    }
}

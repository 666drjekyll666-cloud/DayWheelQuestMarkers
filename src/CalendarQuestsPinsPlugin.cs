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
        public const string PluginVersion = "1.0.21";

        private const float TickSeconds = 1f;
        private const float CrossStructureCheckSeconds = 30f;
        private Type _mainGameType;
        private object _mainGame;
        private object _save;
        private object _prewarmAttemptedSave;
        private float _nextTick;
        private float _nextCrossStructureCheck;
        private bool _cacheReady;
        private bool _crossCacheReady;
        private bool _waitingForPeriodicNpc;
        private bool _loggedReady;
        private bool _prewarmedDuringLoading;
        private string _cachedKnownNpcSignature;
        private string _currentKnownNpcSignature;
        private readonly List<MarkerStyle>[] _currentSinMarkers = new List<MarkerStyle>[7];
        private QuestRuleCache _rules;
        private CrossOwnerRuleCache _crossRules;
        private CalendarMarkers _markers;
        private LoadingCachePrewarmGate _prewarmGate;
        private VerifiedBridgeReminderRules _bridgeRules;

        private void Awake()
        {
            _mainGameType = ReflectionUtil.FindType("MainGame");
            _rules = new QuestRuleCache();
            _crossRules = new CrossOwnerRuleCache();
            _markers = new CalendarMarkers();
            _prewarmGate = new LoadingCachePrewarmGate();
            _bridgeRules = new VerifiedBridgeReminderRules();
            for (var i = 0; i < _currentSinMarkers.Length; i++)
                _currentSinMarkers[i] = new List<MarkerStyle>(4);
            _nextTick = Time.realtimeSinceStartup + 0.5f;
            _nextCrossStructureCheck = Time.realtimeSinceStartup + CrossStructureCheckSeconds;
            Logger.LogInfo(PluginName + " " + PluginVersion + " loaded.");
        }

        private void Update()
        {
            if (Time.realtimeSinceStartup < _nextTick) return;
            _nextTick = Time.realtimeSinceStartup + TickSeconds;

            // Accepted runtime probes show a several-second window where the loaded save, player and all six
            // periodic-NPC graphs already exist while the loading screen is still active and game_started is false.
            // Do the expensive one-time graph parse there so the first playable frame does not pay ~500 ms.
            if (TryPrewarmDuringLoading()) return;

            Tick();
        }

        private void OnDestroy()
        {
            if (_markers != null) _markers.Dispose();
            if (_rules != null) _rules.Clear();
            if (_crossRules != null) _crossRules.Clear();
            if (_bridgeRules != null) _bridgeRules.Clear();
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
            var signature = SessionCacheRebinder.BuildKnownNpcSignature(candidateSave, out hasPeriodicNpc);
            if (!hasPeriodicNpc || string.IsNullOrEmpty(signature)) return false;
            if (_prewarmGate == null || !_prewarmGate.IsReady(_mainGame)) return false;

            // Resolve the game's own marker sprites during the already verified loading window. This replaces
            // the old embedded pixel copies without moving the one-time loaded-Sprite lookup into gameplay.
            if (_markers != null) _markers.TryPrewarmNativeSprites();

            _prewarmAttemptedSave = candidateSave;

            // If this exact structural cache is already alive from an earlier load in the same process,
            // reuse it immediately. Otherwise build both existing caches while the loading overlay is still up.
            if (!string.IsNullOrEmpty(_cachedKnownNpcSignature) &&
                string.Equals(_cachedKnownNpcSignature, signature, StringComparison.Ordinal) &&
                SessionCacheRebinder.TryRebind(_rules, _crossRules, candidateSave, _mainGame))
            {
                _save = candidateSave;
                _cacheReady = true;
                _crossCacheReady = true;
                _waitingForPeriodicNpc = false;
                _currentKnownNpcSignature = signature;
                _prewarmedDuringLoading = true;
                _nextCrossStructureCheck = Time.realtimeSinceStartup + CrossStructureCheckSeconds;
                _loggedReady = false;
                Logger.LogInfo("Quest cache rebound during loading; no graph parse required.");
                return true;
            }

            _rules.Clear();
            _crossRules.Clear();
            var sw = System.Diagnostics.Stopwatch.StartNew();
            var ownerReady = _rules.Build(candidateSave, _mainGame);
            var crossReady = ownerReady && _crossRules.Build(candidateSave, _mainGame);
            sw.Stop();

            if (!ownerReady || !crossReady)
            {
                _rules.Clear();
                _crossRules.Clear();
                _cacheReady = false;
                _crossCacheReady = false;
                _cachedKnownNpcSignature = null;
                _currentKnownNpcSignature = null;
                _prewarmedDuringLoading = false;
                Logger.LogWarning("Loading-screen quest-cache prewarm could not complete; gameplay-safe fallback will be used.");
                return true;
            }

            _save = candidateSave;
            _cacheReady = true;
            _crossCacheReady = true;
            _waitingForPeriodicNpc = false;
            _currentKnownNpcSignature = signature;
            _cachedKnownNpcSignature = signature;
            _prewarmedDuringLoading = true;
            _nextCrossStructureCheck = Time.realtimeSinceStartup + CrossStructureCheckSeconds;
            _loggedReady = false;

            Logger.LogInfo("Quest caches prewarmed behind loading screen in " + sw.Elapsed.TotalMilliseconds.ToString("F2") +
                           " ms. supported=" + _rules.SupportedRuleCount +
                           ", cross-owner tasks=" + _crossRules.TrackedTaskCount + ".");
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
                _crossCacheReady = false;
                _currentKnownNpcSignature = null;
            }

            if (string.IsNullOrEmpty(_currentKnownNpcSignature))
            {
                bool hasPeriodicNpc;
                _currentKnownNpcSignature = SessionCacheRebinder.BuildKnownNpcSignature(_save, out hasPeriodicNpc);
                if (!hasPeriodicNpc)
                {
                    // A fresh save can legitimately know only non-periodic characters. There cannot yet be
                    // a weekday reminder. Preserve any already-parsed session cache so returning to a developed
                    // save in the same game process does not pay the graph-parse cost again.
                    _cacheReady = true;
                    _crossCacheReady = true;
                    _waitingForPeriodicNpc = true;
                    _nextCrossStructureCheck = Time.realtimeSinceStartup + CrossStructureCheckSeconds;
                    _loggedReady = false;
                }
            }

            if (!_waitingForPeriodicNpc && !_cacheReady &&
                !string.IsNullOrEmpty(_cachedKnownNpcSignature) &&
                string.Equals(_cachedKnownNpcSignature, _currentKnownNpcSignature, StringComparison.Ordinal) &&
                SessionCacheRebinder.TryRebind(_rules, _crossRules, _save, _mainGame))
            {
                _cacheReady = true;
                _crossCacheReady = true;
                _nextCrossStructureCheck = Time.realtimeSinceStartup + CrossStructureCheckSeconds;
                _loggedReady = false;
            }

            if (!_cacheReady || (!_waitingForPeriodicNpc && _rules.NeedsRebuild()))
            {
                if (!HasKnownPeriodicNpc(_save))
                {
                    _cacheReady = true;
                    _crossCacheReady = true;
                    _waitingForPeriodicNpc = true;
                    _nextCrossStructureCheck = Time.realtimeSinceStartup + CrossStructureCheckSeconds;
                    _loggedReady = false;
                }
                else
                {
                    _cacheReady = _rules.Build(_save, _mainGame);
                    _crossCacheReady = _crossRules.Build(_save, _mainGame);
                    _nextCrossStructureCheck = Time.realtimeSinceStartup + CrossStructureCheckSeconds;
                    if (!_cacheReady)
                    {
                        _cachedKnownNpcSignature = null;
                        HideMarkers();
                        return;
                    }
                    if (_crossCacheReady)
                        _cachedKnownNpcSignature = _currentKnownNpcSignature;
                    else
                        _cachedKnownNpcSignature = null;
                    _loggedReady = false;
                }
            }
            else if (!_waitingForPeriodicNpc && !_crossCacheReady)
            {
                _crossCacheReady = _crossRules.Build(_save, _mainGame);
                _nextCrossStructureCheck = Time.realtimeSinceStartup + CrossStructureCheckSeconds;
                if (_crossCacheReady)
                    _cachedKnownNpcSignature = _currentKnownNpcSignature;
                else
                    _cachedKnownNpcSignature = null;
                _loggedReady = false;
            }
            else if (!_waitingForPeriodicNpc && Time.realtimeSinceStartup >= _nextCrossStructureCheck)
            {
                _nextCrossStructureCheck = Time.realtimeSinceStartup + CrossStructureCheckSeconds;
                if (_crossRules.NeedsRebuild())
                {
                    // A structural/known-NPC change invalidates the current save signature. Rebuild normally;
                    // the resulting structure can become the next reusable session cache after a save reload.
                    _cachedKnownNpcSignature = null;
                    _crossCacheReady = _crossRules.Build(_save, _mainGame);
                    _currentKnownNpcSignature = null;
                    _loggedReady = false;
                }
            }

            // On a fresh save there is nothing the mod can display yet. Do not touch the HUD or marker
            // resources at all; the only recurring work is the cheap periodic-NPC discovery check.
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
            foreach (var npc in _rules.AllNpcRules)
            {
                var sinTypeValue = GetSinTypeValue(npc.NpcId);
                if (sinTypeValue <= 0 || sinTypeValue >= _currentSinMarkers.Length) continue;

                var tasks = ReflectionUtil.EnumerateMember(npc.KnownNpc, "tasks");
                if (tasks == null) continue;
                foreach (var task in tasks)
                {
                    string taskId;
                    if (!QuestRuleCache.IsVisibleTask(task, out taskId)) continue;
                    if (!_rules.IsTaskActionable(npc, taskId, unlocked, blacklisted)) continue;
                    AddMarker(sinTypeValue, GetMarkerStyle(taskId));
                }
            }

            if (_crossCacheReady)
            {
                foreach (var target in _crossRules.AllTargets)
                {
                    var sinTypeValue = GetSinTypeValue(target.NpcId);
                    if (sinTypeValue <= 0 || sinTypeValue >= _currentSinMarkers.Length) continue;
                    foreach (var task in target.Tasks)
                    {
                        if (!_crossRules.IsVisible(task)) continue;
                        if (!_crossRules.IsActionable(task, unlocked, blacklisted)) continue;
                        AddMarker(sinTypeValue, GetMarkerStyle(task.TaskId));
                    }
                }
            }

            // The full 0.1.26-0.1.29 authored-universe audit found only two objective-bridge families
            // that fall outside the normal owner-local/cross-owner completion-anchor models. Keep them as a tiny
            // explicit GK 1.407 mapping rather than retaining the expensive universal provenance parser. Each
            // family contributes at most one base-game marker across its entry and verified continuation stage.
            if (_bridgeRules != null)
            {
                if (_bridgeRules.IsAstrologerMillActionable(_mainGame, unlocked, blacklisted))
                    AddMarker(GetSinTypeValue("npc_astrologer"), MarkerStyle.Base);
                if (_bridgeRules.IsSnakeInstrumentActionable(_mainGame, unlocked, blacklisted))
                    AddMarker(GetSinTypeValue("npc_cultist"), MarkerStyle.Base);
            }

            // HUD discovery stays deferred until there is something to render. The game-owned marker sprites are
            // normally resolved once during loading-screen prewarm; a bounded fallback lookup runs only if needed.
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
                Logger.LogInfo("Ready. Cached supported rules=0, unsupported/fail-closed rules=0, cross-owner tasks=0, cross-owner supported rules=0, cross-owner unsupported rules=0.");
                return;
            }
            Logger.LogInfo("Ready. Cached supported rules=" + _rules.SupportedRuleCount +
                           ", unsupported/fail-closed rules=" + _rules.UnsupportedRuleCount +
                           ", cross-owner tasks=" + (_crossCacheReady ? _crossRules.TrackedTaskCount : 0) +
                           ", cross-owner supported rules=" + (_crossCacheReady ? _crossRules.SupportedRuleCount : 0) +
                           ", cross-owner unsupported rules=" + (_crossCacheReady ? _crossRules.UnsupportedRuleCount : 0) + ".");
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
                _crossCacheReady = false;
                _waitingForPeriodicNpc = false;
                _currentKnownNpcSignature = null;
                _prewarmedDuringLoading = false;
                _nextCrossStructureCheck = Time.realtimeSinceStartup + CrossStructureCheckSeconds;
                HideMarkers();
            }

            object started;
            if (!TryReadStatic(_mainGameType, "game_started", out started) || !(started is bool) || !(bool)started) return false;
            object starting;
            if (TryReadStatic(_mainGameType, "game_starting", out starting) && starting is bool && (bool)starting) return false;
            if (!HasLoadedCollections(_save)) return false;

            // Rebind a loading-screen-built structural cache once gameplay becomes authoritative. This refreshes
            // the final player/save/KnownNpc references and fails closed if the loading phase replaced anything.
            if (_prewarmedDuringLoading)
            {
                bool hasPeriodicNpc;
                var liveSignature = SessionCacheRebinder.BuildKnownNpcSignature(_save, out hasPeriodicNpc);
                var valid = hasPeriodicNpc &&
                            !string.IsNullOrEmpty(liveSignature) &&
                            string.Equals(liveSignature, _cachedKnownNpcSignature, StringComparison.Ordinal) &&
                            SessionCacheRebinder.TryRebind(_rules, _crossRules, _save, _mainGame);

                _prewarmedDuringLoading = false;
                _currentKnownNpcSignature = liveSignature;
                if (valid)
                {
                    _cacheReady = true;
                    _crossCacheReady = true;
                    _nextCrossStructureCheck = Time.realtimeSinceStartup + CrossStructureCheckSeconds;
                }
                else
                {
                    _cacheReady = false;
                    _crossCacheReady = false;
                    _cachedKnownNpcSignature = null;
                    Logger.LogWarning("Loading-screen quest-cache prewarm did not survive final runtime validation; rebuilding safely after load.");
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

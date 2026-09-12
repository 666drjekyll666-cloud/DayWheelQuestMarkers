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
        public const string PluginVersion = "1.0.29";

        private const float TickSeconds = 1f;
        private const float StructureCheckSeconds = 30f;

        private Type _mainGameType;
        private object _mainGame;
        private object _save;
        private object _prewarmAttemptedSave;
        private object _runtimeRestoreAttemptedSave;
        private float _nextTick;
        private float _nextStructureCheck;
        private bool _cacheReady;
        private bool _waitingForPeriodicNpc;
        private bool _loggedReady;
        private bool _prewarmedDuringLoading;
        private ulong _currentKnownNpcFingerprint;

        private readonly List<MarkerStyle>[] _currentSinMarkers = new List<MarkerStyle>[7];
        private WeekdayInteractionRuleCache _rules;
        private PersistentRuleManifest _manifest;
        private VerifiedNestedDialogueGate _nestedDialogueGate;
        private CalendarMarkers _markers;
        private LoadingCachePrewarmGate _prewarmGate;

        private void Awake()
        {
            _mainGameType = ReflectionUtil.FindType("MainGame");
            _rules = new WeekdayInteractionRuleCache();
            _manifest = new PersistentRuleManifest(_rules);
            _nestedDialogueGate = new VerifiedNestedDialogueGate();
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
            if (!TryReadStatic(_mainGameType, "game_started", out started) || !(started is bool) || (bool)started)
                return false;

            object starting;
            if (!TryReadStatic(_mainGameType, "game_starting", out starting) || !(starting is bool) || (bool)starting)
                return false;

            object candidateSave;
            if (!ReflectionUtil.TryRead(_mainGame, "save", out candidateSave) || candidateSave == null) return false;
            if (ReferenceEquals(candidateSave, _prewarmAttemptedSave)) return false;
            if (!HasLoadedCollections(candidateSave)) return false;
            if (_prewarmGate == null || !_prewarmGate.IsReady(_mainGame)) return false;

            if (_markers != null) _markers.TryPrewarmNativeSprites();
            _prewarmAttemptedSave = candidateSave;

            double loadMs;
            string loadFailure;
            var loaded = _manifest.TryLoad(candidateSave, _mainGame, out loadMs, out loadFailure);

            double bootstrapMs = 0;
            string bootstrapNote = null;
            if (!loaded)
            {
                if (!_manifest.TryBootstrapAndPersist(candidateSave, _mainGame, out bootstrapMs, out bootstrapNote))
                {
                    _rules.Clear();
                    _cacheReady = false;
                    _currentKnownNpcFingerprint = 0UL;
                    _prewarmedDuringLoading = false;
                    Logger.LogWarning("Loading-screen rule-manifest initialization failed. Cache load: " +
                                      (loadFailure ?? "<none>") + "; bootstrap: " +
                                      (bootstrapNote ?? "<none>") + ". Gameplay-safe fallback will be used.");
                    return true;
                }
            }

            ulong fingerprint;
            bool hasPeriodicNpc;
            if (!_manifest.Bind(candidateSave, _mainGame, out fingerprint, out hasPeriodicNpc) ||
                !_manifest.IsRuntimeValid(_mainGame))
            {
                _rules.Clear();
                _cacheReady = false;
                _currentKnownNpcFingerprint = 0UL;
                _prewarmedDuringLoading = false;
                Logger.LogWarning("Loading-screen rule manifest could not bind to the loaded runtime.");
                return true;
            }

            _save = candidateSave;
            _runtimeRestoreAttemptedSave = null;
            _cacheReady = true;
            _waitingForPeriodicNpc = !hasPeriodicNpc;
            _currentKnownNpcFingerprint = fingerprint;
            _prewarmedDuringLoading = true;
            _nextStructureCheck = Time.realtimeSinceStartup + StructureCheckSeconds;
            _loggedReady = false;

            if (loaded)
            {
                Logger.LogInfo("Persistent rule manifest loaded behind loading screen in " +
                               loadMs.ToString("F2") + " ms; FlowCanvas graph parse skipped.");
            }
            else
            {
                Logger.LogInfo("Persistent rule manifest bootstrapped behind loading screen in " +
                               bootstrapMs.ToString("F2") +
                               " ms; future loads can skip FlowCanvas graph parsing.");
                if (!string.IsNullOrEmpty(bootstrapNote))
                    Logger.LogWarning(bootstrapNote);
            }

            LogManifestSummary("Loading manifest ready");
            return true;
        }

        private void Tick()
        {
            if (!EnsureRuntime())
            {
                HideMarkers();
                return;
            }

            if (!_cacheReady)
            {
                if (!RestoreForCurrentSave("runtime cache was not prewarmed"))
                {
                    HideMarkers();
                    return;
                }
            }

            if (!_manifest.IsRuntimeValid(_mainGame))
            {
                if (!RestoreForCurrentSave("runtime object bindings were recreated"))
                {
                    HideMarkers();
                    return;
                }
            }
            else if (_manifest.KnownNpcCountChanged(_save))
            {
                ulong fingerprint;
                bool hasPeriodicNpc;
                if (!_manifest.Bind(_save, _mainGame, out fingerprint, out hasPeriodicNpc))
                {
                    if (!RestoreForCurrentSave("known-NPC bind failed"))
                    {
                        HideMarkers();
                        return;
                    }
                }
                else
                {
                    ApplyKnownNpcState(fingerprint, hasPeriodicNpc, true);
                }
            }

            if (Time.realtimeSinceStartup >= _nextStructureCheck)
            {
                _nextStructureCheck = Time.realtimeSinceStartup + StructureCheckSeconds;

                if (!_manifest.IsRuntimeValid(_mainGame))
                {
                    if (!RestoreForCurrentSave("scheduled runtime validation failed"))
                    {
                        HideMarkers();
                        return;
                    }
                }
                else
                {
                    bool hasPeriodicNpc;
                    var liveFingerprint = PersistentRuleManifest.BuildKnownNpcFingerprint(_save, out hasPeriodicNpc);
                    if (liveFingerprint != _currentKnownNpcFingerprint)
                    {
                        ulong reboundFingerprint;
                        bool reboundHasPeriodicNpc;
                        if (!_manifest.Bind(_save, _mainGame, out reboundFingerprint, out reboundHasPeriodicNpc))
                        {
                            if (!RestoreForCurrentSave("scheduled known-NPC bind failed"))
                            {
                                HideMarkers();
                                return;
                            }
                        }
                        else
                        {
                            ApplyKnownNpcState(reboundFingerprint, reboundHasPeriodicNpc, true);
                        }
                    }
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

                    for (var i = 0; i < target.Topics.Count; i++)
                    {
                        var topic = target.Topics[i];
                        if (!_rules.IsTopicActionable(topic, unlocked, blacklisted)) continue;
                        if (_nestedDialogueGate == null ||
                            !_nestedDialogueGate.IsSatisfied(target, topic, _mainGame, blacklisted)) continue;
                        AddMarker(sinTypeValue, MarkerStyle.Base);
                    }
                }

                for (var i = 0; i < target.CrossTasks.Count; i++)
                {
                    var task = target.CrossTasks[i];
                    if (!_rules.IsCrossTaskVisible(task)) continue;
                    if (!_rules.IsCrossTaskActionable(task, unlocked, blacklisted)) continue;
                    AddMarker(sinTypeValue, GetMarkerStyle(task.TaskId));
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

        private bool RestoreForCurrentSave(string reason)
        {
            if (ReferenceEquals(_runtimeRestoreAttemptedSave, _save)) return false;
            _runtimeRestoreAttemptedSave = _save;

            double loadMs;
            string loadFailure;
            if (!_manifest.TryLoad(_save, _mainGame, out loadMs, out loadFailure))
            {
                Logger.LogWarning("Persistent rule manifest unavailable during gameplay (" + reason + "): " +
                                  (loadFailure ?? "<unknown>") + ". No graph parser will run in gameplay.");
                return false;
            }

            ulong fingerprint;
            bool hasPeriodicNpc;
            if (!_manifest.Bind(_save, _mainGame, out fingerprint, out hasPeriodicNpc))
                return false;

            _cacheReady = true;
            _runtimeRestoreAttemptedSave = null;
            ApplyKnownNpcState(fingerprint, hasPeriodicNpc, false);
            Logger.LogInfo("Persistent rule manifest restored in " + loadMs.ToString("F2") +
                           " ms (" + reason + "); graph parse not required.");
            return true;
        }

        private void ApplyKnownNpcState(ulong fingerprint, bool hasPeriodicNpc, bool logRebind)
        {
            var wasWaiting = _waitingForPeriodicNpc;
            _currentKnownNpcFingerprint = fingerprint;
            _waitingForPeriodicNpc = !hasPeriodicNpc;
            _nextStructureCheck = Time.realtimeSinceStartup + StructureCheckSeconds;

            if (wasWaiting != _waitingForPeriodicNpc) _loggedReady = false;
            if (logRebind)
                Logger.LogInfo("Known-NPC bindings refreshed from persistent manifest; graph parse not required.");
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
                Logger.LogInfo("Ready. Persistent rule manifest active; no weekday NPC is known yet.");
                return;
            }

            LogManifestSummary("Ready");
        }

        private void LogManifestSummary(string prefix)
        {
            Logger.LogInfo(prefix + ". Rules: owner supported=" + _rules.OwnerSupportedRuleCount +
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
                _runtimeRestoreAttemptedSave = null;
                _cacheReady = false;
                _waitingForPeriodicNpc = false;
                _currentKnownNpcFingerprint = 0UL;
                _prewarmedDuringLoading = false;
                _nextStructureCheck = Time.realtimeSinceStartup + StructureCheckSeconds;
                HideMarkers();
            }

            object started;
            if (!TryReadStatic(_mainGameType, "game_started", out started) || !(started is bool) || !(bool)started)
                return false;

            object starting;
            if (TryReadStatic(_mainGameType, "game_starting", out starting) && starting is bool && (bool)starting)
                return false;

            if (!HasLoadedCollections(_save)) return false;

            if (_prewarmedDuringLoading)
            {
                _prewarmedDuringLoading = false;

                ulong fingerprint;
                bool hasPeriodicNpc;
                var valid = _manifest.Bind(_save, _mainGame, out fingerprint, out hasPeriodicNpc) &&
                            _manifest.IsRuntimeValid(_mainGame);

                if (valid)
                {
                    _cacheReady = true;
                    _currentKnownNpcFingerprint = fingerprint;
                    _waitingForPeriodicNpc = !hasPeriodicNpc;
                    _nextStructureCheck = Time.realtimeSinceStartup + StructureCheckSeconds;
                }
                else
                {
                    _cacheReady = false;
                    Logger.LogWarning("Loading-screen persistent manifest did not survive final runtime binding; " +
                                      "a cheap manifest reload will be attempted.");
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

        private static int GetSinTypeValue(string npcId)
        {
            if (string.Equals(npcId, "npc_astrologer", StringComparison.Ordinal)) return 1;
            if (string.Equals(npcId, "npc_inquisitor", StringComparison.Ordinal)) return 2;
            if (string.Equals(npcId, "npc_cultist", StringComparison.Ordinal)) return 3;
            if (string.Equals(npcId, "npc_merchant", StringComparison.Ordinal)) return 4;
            if (string.Equals(npcId, "npc_actress", StringComparison.Ordinal)) return 5;
            if (string.Equals(npcId, "npc_bishop", StringComparison.Ordinal)) return 6;
            return -1;
        }

        private static MarkerStyle GetMarkerStyle(string taskId)
        {
            if (string.IsNullOrEmpty(taskId)) return MarkerStyle.Base;
            if (taskId.StartsWith("dlc_stories_", StringComparison.Ordinal)) return MarkerStyle.Stories;
            if (taskId.StartsWith("dlc_refugees", StringComparison.Ordinal) || taskId.StartsWith("s_ev", StringComparison.Ordinal))
                return MarkerStyle.Violet;
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
                if (field != null)
                {
                    value = field.GetValue(null);
                    return true;
                }

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

        private void HideMarkers()
        {
            ClearMarkerSets();
            if (_markers != null) _markers.HideAll();
        }
    }
}

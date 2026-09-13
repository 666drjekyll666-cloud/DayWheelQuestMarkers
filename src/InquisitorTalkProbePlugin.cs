using System;
using System.Collections;
using System.Collections.Generic;
using BepInEx;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace CalendarQuestsPins
{
    [BepInPlugin(PluginGuid, PluginName, PluginVersion)]
    public sealed class InquisitorTalkProbePlugin : BaseUnityPlugin
    {
        public const string PluginGuid = "nikich.gyk.calendarquestspins.inquisitortalkprobe";
        public const string PluginName = "Day Wheel Quest Markers - inquisitor_talk probe";
        public const string PluginVersion = "0.1.0";

        private const string TargetTaskId = "inquisitor_talk";
        private const float TickSeconds = 0.5f;
        private const float HeartbeatSeconds = 30f;

        private Type _mainGameType;
        private Type _playerType;
        private object _mainGame;
        private object _save;
        private object _player;
        private float _nextTick;
        private float _nextHeartbeat;
        private bool _haveSnapshot;

        private readonly Dictionary<string, string> _taskStates =
            new Dictionary<string, string>(StringComparer.Ordinal);
        private readonly HashSet<string> _unlocked =
            new HashSet<string>(StringComparer.Ordinal);
        private readonly HashSet<string> _blacklisted =
            new HashSet<string>(StringComparer.Ordinal);

        private void Awake()
        {
            _mainGameType = ReflectionUtil.FindType("MainGame");
            _playerType = ReflectionUtil.FindType("Player");
            _nextTick = Time.realtimeSinceStartup + 0.5f;
            _nextHeartbeat = Time.realtimeSinceStartup + HeartbeatSeconds;
            Logger.LogInfo(PluginName + " " + PluginVersion + " loaded. Read-only diagnostics for " + TargetTaskId + ".");
        }

        private void Update()
        {
            if (Time.realtimeSinceStartup < _nextTick) return;
            _nextTick = Time.realtimeSinceStartup + TickSeconds;

            if (!EnsureRuntime()) return;
            CaptureAndCompare();

            if (Time.realtimeSinceStartup >= _nextHeartbeat)
            {
                _nextHeartbeat = Time.realtimeSinceStartup + HeartbeatSeconds;
                Logger.LogInfo("PROBE_HEARTBEAT " + BuildContext());
            }
        }

        private bool EnsureRuntime()
        {
            if (_mainGame == null || !ReflectionUtil.IsUnityAlive(_mainGame))
                _mainGame = ReflectionUtil.FindActiveUnityInstance(_mainGameType);
            if (_mainGame == null) return false;

            object started;
            if (!TryReadStatic(_mainGameType, "game_started", out started) || !(started is bool) || !(bool)started)
                return false;

            object candidateSave;
            if (!ReflectionUtil.TryRead(_mainGame, "save", out candidateSave) || candidateSave == null)
                return false;

            if (!ReferenceEquals(candidateSave, _save))
            {
                _save = candidateSave;
                _player = null;
                _taskStates.Clear();
                _unlocked.Clear();
                _blacklisted.Clear();
                _haveSnapshot = false;
                Logger.LogInfo("PROBE_SAVE_BOUND scene=" + SafeSceneName());
            }

            object known;
            if (!ReflectionUtil.TryRead(_save, "known_npcs", out known) || known == null) return false;
            return ReflectionUtil.EnumerateMember(known, "npcs") != null;
        }

        private void CaptureAndCompare()
        {
            var currentTasks = BuildRelevantTaskSnapshot();
            var currentUnlocked = BuildRelevantPhraseSet("unlocked_phrases");
            var currentBlacklisted = BuildRelevantPhraseSet("black_list_of_phrases");

            if (!_haveSnapshot)
            {
                _haveSnapshot = true;
                CopyDictionary(currentTasks, _taskStates);
                CopySet(currentUnlocked, _unlocked);
                CopySet(currentBlacklisted, _blacklisted);
                LogInitialSnapshot(currentTasks, currentUnlocked, currentBlacklisted);
                return;
            }

            var changed = false;
            foreach (var pair in currentTasks)
            {
                string previous;
                if (!_taskStates.TryGetValue(pair.Key, out previous))
                {
                    Logger.LogInfo("TASK_ADDED " + pair.Key + " state=" + pair.Value);
                    changed = true;
                }
                else if (!string.Equals(previous, pair.Value, StringComparison.Ordinal))
                {
                    Logger.LogInfo("TASK_CHANGED " + pair.Key + " " + previous + " -> " + pair.Value);
                    changed = true;
                }
            }

            foreach (var pair in _taskStates)
            {
                if (!currentTasks.ContainsKey(pair.Key))
                {
                    Logger.LogInfo("TASK_REMOVED " + pair.Key + " previous=" + pair.Value);
                    changed = true;
                }
            }

            changed |= LogSetDelta("PHRASE_UNLOCKED", _unlocked, currentUnlocked);
            changed |= LogSetDelta("PHRASE_BLACKLISTED", _blacklisted, currentBlacklisted);

            if (changed)
                Logger.LogInfo("PROBE_CONTEXT " + BuildContextFrom(currentTasks));

            CopyDictionary(currentTasks, _taskStates);
            CopySet(currentUnlocked, _unlocked);
            CopySet(currentBlacklisted, _blacklisted);
        }

        private Dictionary<string, string> BuildRelevantTaskSnapshot()
        {
            var result = new Dictionary<string, string>(StringComparer.Ordinal);
            object known;
            if (!ReflectionUtil.TryRead(_save, "known_npcs", out known) || known == null) return result;
            var npcs = ReflectionUtil.EnumerateMember(known, "npcs");
            if (npcs == null) return result;

            foreach (var npc in npcs)
            {
                var owner = ReflectionUtil.ReadString(npc, "npc_id") ?? "<unknown-owner>";
                var tasks = ReflectionUtil.EnumerateMember(npc, "tasks");
                if (tasks == null) continue;
                foreach (var task in tasks)
                {
                    var id = ReflectionUtil.ReadString(task, "id");
                    if (!IsRelevantTaskId(id)) continue;
                    object state;
                    ReflectionUtil.TryRead(task, "state", out state);
                    result[owner + "/" + id] = DescribeState(state);
                }
            }
            return result;
        }

        private HashSet<string> BuildRelevantPhraseSet(string member)
        {
            var result = new HashSet<string>(StringComparer.Ordinal);
            object value;
            if (!ReflectionUtil.TryRead(_save, member, out value) || value == null) return result;
            var enumerable = value as IEnumerable;
            if (enumerable == null) return result;
            foreach (var item in enumerable)
            {
                var text = item as string;
                if (string.IsNullOrEmpty(text)) continue;
                if (text.IndexOf("inquisitor", StringComparison.OrdinalIgnoreCase) < 0) continue;
                result.Add(text);
            }
            return result;
        }

        private void LogInitialSnapshot(Dictionary<string, string> tasks, HashSet<string> unlocked, HashSet<string> blacklisted)
        {
            Logger.LogInfo("PROBE_INITIAL " + BuildContextFrom(tasks));
            if (tasks.Count == 0)
                Logger.LogInfo("PROBE_INITIAL_TASKS <none>");
            else
                foreach (var pair in tasks) Logger.LogInfo("PROBE_INITIAL_TASK " + pair.Key + " state=" + pair.Value);

            foreach (var phrase in unlocked) Logger.LogInfo("PROBE_INITIAL_UNLOCKED " + phrase);
            foreach (var phrase in blacklisted) Logger.LogInfo("PROBE_INITIAL_BLACKLISTED " + phrase);
        }

        private string BuildContext()
        {
            return BuildContextFrom(BuildRelevantTaskSnapshot());
        }

        private string BuildContextFrom(Dictionary<string, string> tasks)
        {
            var target = "<absent>";
            foreach (var pair in tasks)
            {
                if (pair.Key.EndsWith("/" + TargetTaskId, StringComparison.Ordinal))
                {
                    target = pair.Key + "=" + pair.Value;
                    break;
                }
            }

            return "target=" + target +
                   " scene=" + SafeSceneName() +
                   " player=" + PlayerPosition() +
                   " realtime=" + Time.realtimeSinceStartup.ToString("F2");
        }

        private string PlayerPosition()
        {
            if (_player == null || !ReflectionUtil.IsUnityAlive(_player))
                _player = ReflectionUtil.FindActiveUnityInstance(_playerType);
            var component = _player as Component;
            if (component == null) return "<unresolved>";
            var p = component.transform.position;
            return "(" + p.x.ToString("F1") + "," + p.y.ToString("F1") + "," + p.z.ToString("F1") + ")";
        }

        private static string SafeSceneName()
        {
            try { return SceneManager.GetActiveScene().name ?? "<unnamed>"; }
            catch { return "<unresolved>"; }
        }

        private static bool IsRelevantTaskId(string id)
        {
            if (string.IsNullOrEmpty(id)) return false;
            return string.Equals(id, TargetTaskId, StringComparison.Ordinal) ||
                   id.StartsWith("inquisitor_", StringComparison.Ordinal);
        }

        private static string DescribeState(object state)
        {
            if (state == null) return "<null>";
            var text = state.ToString();
            try { return Convert.ToInt32(state) + ":" + text; }
            catch { return text ?? "<unknown>"; }
        }

        private bool LogSetDelta(string label, HashSet<string> previous, HashSet<string> current)
        {
            var changed = false;
            foreach (var item in current)
            {
                if (previous.Contains(item)) continue;
                Logger.LogInfo(label + "_ADDED " + item);
                changed = true;
            }
            foreach (var item in previous)
            {
                if (current.Contains(item)) continue;
                Logger.LogInfo(label + "_REMOVED " + item);
                changed = true;
            }
            return changed;
        }

        private static void CopyDictionary(Dictionary<string, string> source, Dictionary<string, string> target)
        {
            target.Clear();
            foreach (var pair in source) target[pair.Key] = pair.Value;
        }

        private static void CopySet(HashSet<string> source, HashSet<string> target)
        {
            target.Clear();
            foreach (var item in source) target.Add(item);
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
    }
}

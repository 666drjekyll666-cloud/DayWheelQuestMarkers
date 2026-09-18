using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Text.RegularExpressions;
using BepInEx;
using UnityEngine;

namespace CalendarQuestsPins
{
    /// <summary>
    /// Schema-4 persistent manifest. It stores the accepted structural reminder rules, the unified
    /// exact-self-consuming dialogue rules, and compact root-to-answer navigation predicates derived
    /// from the same six Graveyard Keeper 1.407 graphs. Graph parsing remains loading-screen-only.
    /// </summary>
    internal sealed class PersistentRuleManifest
    {
        internal const string VerifiedGameVersion = "1.407";
        private const string Magic = "DWQM_RULE_MANIFEST";
        private const int SchemaVersion = 4;

        private const int ExpectedOwnerSupported = 75;
        private const int ExpectedOwnerUnsupported = 6;
        private const int ExpectedCrossTasks = 8;
        private const int ExpectedCrossSupported = 6;
        private const int ExpectedCrossUnsupported = 0;
        private const int ExpectedAtTopics = 55;
        private const int ExpectedAtTopicSupported = 54;
        private const int ExpectedAtTopicUnsupported = 1;

        private static readonly string[] NpcIds =
        {
            "npc_astrologer", "npc_inquisitor", "npc_cultist", "npc_merchant", "npc_actress", "npc_bishop"
        };

        private static readonly Regex OwnerNpcRegex = new Regex(
            "\"NPC id\":\\{\\\"\\$content\\\":\\\"([^\\\"]+)\\\"",
            RegexOptions.CultureInvariant | RegexOptions.Compiled);

        private readonly WeekdayInteractionRuleCache _cache;
        private readonly NavigationReachabilityCache _navigation;
        private readonly UnifiedSelfConsumingCompiler _selfConsumingCompiler = new UnifiedSelfConsumingCompiler();
        private readonly Type _cacheType = typeof(WeekdayInteractionRuleCache);
        private readonly Type _controllerType = ReflectionUtil.FindType("FlowCanvas.FlowScriptController");
        private readonly Type _worldMapType = ReflectionUtil.FindType("WorldMap");

        private readonly FieldInfo _targetsField;
        private readonly FieldInfo _saveField;
        private readonly FieldInfo _playerField;
        private readonly FieldInfo _knownNpcCountField;
        private readonly FieldInfo _worldObjectGetterField;
        private readonly FieldInfo _smartResFactoryField;

        private readonly MethodInfo _findWorldObjectGetter;
        private readonly MethodInfo _findSmartResFactory;
        private readonly MethodInfo _tryBindPlayer;
        private readonly MethodInfo _parseGraph;
        private readonly MethodInfo _createSmartRes;

        private readonly MethodInfo _worldObjectGetter;
        private readonly string _path;
        private int _boundKnownNpcCount;
        private UnifiedSelfConsumingCompiler.Stats _nonAtStats = new UnifiedSelfConsumingCompiler.Stats();

        internal string ManifestPath { get { return _path; } }
        internal int NavigationAnswerCount { get { return _navigation.AnswerCount; } }
        internal int NavigationPathCount { get { return _navigation.PathCount; } }
        internal int NavigationPredicateCount { get { return _navigation.PredicateCount; } }
        internal int NavigationUnsupportedPathCount { get { return _navigation.UnsupportedPathCount; } }
        internal int NonAtUniqueCount { get { return _nonAtStats == null ? 0 : _nonAtStats.NonAtUnique; } }
        internal int NonAtExactSelfCount { get { return _nonAtStats == null ? 0 : _nonAtStats.ExactSelf; } }
        internal int NonAtTopicCount { get { return _nonAtStats == null ? 0 : _nonAtStats.AdmittedTopics; } }
        internal int NonAtSupportedVariantCount { get { return _nonAtStats == null ? 0 : _nonAtStats.SupportedVariants; } }
        internal int NonAtUnsupportedVariantCount { get { return _nonAtStats == null ? 0 : _nonAtStats.UnsupportedVariants; } }
        internal int NonAtCompletionExcludedCount { get { return _nonAtStats == null ? 0 : _nonAtStats.CompletionExcluded; } }
        internal int NonAtNavigationExcludedCount { get { return _nonAtStats == null ? 0 : _nonAtStats.NavigationExcluded; } }

        internal PersistentRuleManifest(WeekdayInteractionRuleCache cache, NavigationReachabilityCache navigation)
        {
            if (cache == null) throw new ArgumentNullException("cache");
            if (navigation == null) throw new ArgumentNullException("navigation");
            _cache = cache;
            _navigation = navigation;

            const BindingFlags instance = BindingFlags.Instance | BindingFlags.NonPublic;
            _targetsField = _cacheType.GetField("_targets", instance);
            _saveField = _cacheType.GetField("_save", instance);
            _playerField = _cacheType.GetField("_player", instance);
            _knownNpcCountField = _cacheType.GetField("_knownNpcCount", instance);
            _worldObjectGetterField = _cacheType.GetField("_worldObjectGetter", instance);
            _smartResFactoryField = _cacheType.GetField("_smartResFactory", instance);

            _findWorldObjectGetter = _cacheType.GetMethod("FindWorldObjectGetter", instance);
            _findSmartResFactory = _cacheType.GetMethod("FindSmartResFactory", instance);
            _tryBindPlayer = _cacheType.GetMethod("TryBindPlayer", instance);
            _parseGraph = _cacheType.GetMethod("ParseGraph", instance);
            _createSmartRes = _cacheType.GetMethod("CreateSmartRes", instance);

            _worldObjectGetter = FindWorldObjectGetter();
            _path = Path.Combine(Paths.CachePath, "DayWheelQuestMarkers", "rules-1.407.bin");
        }

        internal bool KnownNpcCountChanged(object save)
        {
            return CountKnownNpcsNoAlloc(save) != _boundKnownNpcCount;
        }

        internal bool IsRuntimeValid(object mainGame)
        {
            if (mainGame == null || _playerField == null) return false;
            object livePlayer;
            if (!ReflectionUtil.TryRead(mainGame, "player", out livePlayer) || !ReflectionUtil.IsUnityAlive(livePlayer)) return false;
            var cachedPlayer = _playerField.GetValue(_cache);
            if (!ReferenceEquals(livePlayer, cachedPlayer)) return false;

            var targets = GetTargets();
            if (targets == null || targets.Count != NpcIds.Length || !_navigation.IsReadyForTargets(targets)) return false;
            for (var i = 0; i < targets.Count; i++)
                if (targets[i] == null || !ReflectionUtil.IsUnityAlive(targets[i].WorldObject)) return false;
            return true;
        }

        internal bool TryLoad(object save, object mainGame, out double elapsedMs, out string failure)
        {
            var sw = Stopwatch.StartNew();
            failure = null;
            try
            {
                if (!File.Exists(_path)) { failure = "manifest file is missing"; return false; }
                if (!PrepareRuntime(save, mainGame)) { failure = "runtime bindings are not ready"; return false; }

                using (var stream = new FileStream(_path, FileMode.Open, FileAccess.Read, FileShare.Read))
                using (var reader = new BinaryReader(stream))
                {
                    if (!string.Equals(reader.ReadString(), Magic, StringComparison.Ordinal))
                    { failure = "manifest magic mismatch"; return false; }
                    if (reader.ReadInt32() != SchemaVersion)
                    { failure = "manifest schema mismatch; schema 4 rebuild required"; return false; }
                    if (!string.Equals(reader.ReadString(), VerifiedGameVersion, StringComparison.Ordinal))
                    { failure = "manifest game version mismatch"; return false; }
                    var gameVersion = ReadGameVersion(save);
                    if (!string.Equals(gameVersion, VerifiedGameVersion, StringComparison.Ordinal))
                    { failure = "loaded save game version is " + (gameVersion ?? "<unknown>"); return false; }

                    ClearCaches();
                    if (!PrepareRuntime(save, mainGame)) { failure = "runtime bindings were lost during manifest load"; return false; }

                    var targets = GetTargets();
                    var worldObjects = new Dictionary<string, object>(StringComparer.Ordinal);
                    if (targets == null) { failure = "cache target storage unavailable"; return false; }

                    var targetCount = reader.ReadInt32();
                    if (targetCount != NpcIds.Length) { failure = "manifest target count mismatch"; return false; }
                    for (var i = 0; i < targetCount; i++)
                    {
                        var npcId = reader.ReadString();
                        if (!string.Equals(npcId, NpcIds[i], StringComparison.Ordinal))
                        { failure = "manifest target order/id mismatch at index " + i; return false; }
                        var wgo = GetWorldObject(npcId);
                        if (!ReflectionUtil.IsUnityAlive(wgo))
                        { failure = "weekday NPC world object not ready: " + npcId; return false; }
                        worldObjects[npcId] = wgo;
                        var target = new WeekdayInteractionRuleCache.TargetRules { NpcId = npcId, WorldObject = wgo };

                        var ownerTaskCount = reader.ReadInt32();
                        if (ownerTaskCount < 0 || ownerTaskCount > 256) { failure = "owner task count out of range"; return false; }
                        for (var t = 0; t < ownerTaskCount; t++)
                        {
                            var taskId = reader.ReadString();
                            var rules = new List<WeekdayInteractionRuleCache.RuleVariant>();
                            if (!ReadVariants(reader, wgo, rules, out failure)) return false;
                            target.OwnerTaskRules[taskId] = rules;
                        }

                        var crossTaskCount = reader.ReadInt32();
                        if (crossTaskCount < 0 || crossTaskCount > 128) { failure = "cross-task count out of range"; return false; }
                        for (var c = 0; c < crossTaskCount; c++)
                        {
                            var cross = new WeekdayInteractionRuleCache.CrossTaskRules
                            {
                                OwnerNpcId = reader.ReadString(),
                                TaskId = reader.ReadString()
                            };
                            if (!ReadVariants(reader, wgo, cross.Rules, out failure)) return false;
                            target.CrossTasks.Add(cross);
                        }

                        var topicCount = reader.ReadInt32();
                        if (topicCount < 0 || topicCount > 256) { failure = "topic count out of range"; return false; }
                        for (var p = 0; p < topicCount; p++)
                        {
                            var topic = new WeekdayInteractionRuleCache.TopicRule { AnswerId = reader.ReadString() };
                            if (!ReadVariants(reader, wgo, topic.Variants, out failure)) return false;
                            target.Topics.Add(topic);
                        }
                        targets.Add(target);
                    }

                    if (!_navigation.Read(reader, worldObjects, out failure)) return false;
                    var counts = ReadCounts(reader);
                    _nonAtStats = ReadNonAtStats(reader);
                    if (!CountsAreCanonical(counts, _nonAtStats))
                    {
                        failure = "manifest canonical-count check failed: " + CountsToString(counts, _nonAtStats);
                        return false;
                    }
                    ApplyCounts(counts);
                    if (stream.Position != stream.Length) { failure = "manifest has trailing data"; return false; }
                }

                ulong ignoredFingerprint;
                bool ignoredPeriodic;
                if (!Bind(save, mainGame, out ignoredFingerprint, out ignoredPeriodic))
                { failure = "manifest loaded but live binding failed"; return false; }
                return IsRuntimeValid(mainGame);
            }
            catch (Exception ex)
            {
                failure = ex.GetType().Name + ": " + ex.Message;
                ClearCaches();
                return false;
            }
            finally
            {
                sw.Stop();
                elapsedMs = sw.Elapsed.TotalMilliseconds;
            }
        }

        internal bool TryBootstrapAndPersist(object save, object mainGame, out double elapsedMs, out string failure)
        {
            var sw = Stopwatch.StartNew();
            failure = null;
            try
            {
                if (!string.Equals(ReadGameVersion(save), VerifiedGameVersion, StringComparison.Ordinal))
                { failure = "bootstrap is supported only for Graveyard Keeper " + VerifiedGameVersion; return false; }
                ClearCaches();
                if (!PrepareRuntime(save, mainGame)) { failure = "runtime bindings are not ready for bootstrap"; return false; }

                int ignoredKnownCount;
                var knownNpcMap = ReadKnownNpcs(save, out ignoredKnownCount);
                var graphs = new Dictionary<string, string>(StringComparer.Ordinal);
                for (var i = 0; i < NpcIds.Length; i++)
                {
                    var npcId = NpcIds[i];
                    var serialized = ReadSerializedGraph(npcId);
                    if (string.IsNullOrEmpty(serialized)) { failure = "serialized graph not ready: " + npcId; return false; }
                    graphs[npcId] = serialized;
                    if (!knownNpcMap.ContainsKey(npcId)) knownNpcMap[npcId] = new object();
                    foreach (Match match in OwnerNpcRegex.Matches(serialized))
                    {
                        if (!match.Success || match.Groups.Count < 2) continue;
                        var ownerNpcId = match.Groups[1].Value;
                        if (string.IsNullOrEmpty(ownerNpcId) || knownNpcMap.ContainsKey(ownerNpcId)) continue;
                        knownNpcMap[ownerNpcId] = new object();
                    }
                }

                var targets = GetTargets();
                if (targets == null) { failure = "cache target storage unavailable"; return false; }
                for (var i = 0; i < NpcIds.Length; i++)
                {
                    var npcId = NpcIds[i];
                    var wgo = GetWorldObject(npcId);
                    if (!ReflectionUtil.IsUnityAlive(wgo)) { failure = "weekday NPC world object not ready: " + npcId; return false; }
                    object knownNpc;
                    knownNpcMap.TryGetValue(npcId, out knownNpc);
                    var target = new WeekdayInteractionRuleCache.TargetRules { NpcId = npcId, KnownNpc = knownNpc, WorldObject = wgo };
                    _parseGraph.Invoke(_cache, new object[] { target, graphs[npcId], knownNpcMap });
                    targets.Add(target);
                }

                // Build the interaction-root navigation index against the already accepted owner/cross/@ rule set first.
                // Non-@ exact-self candidates are admitted only when this index proves a real root-to-answer path.
                for (var i = 0; i < NpcIds.Length; i++)
                {
                    var target = targets[i];
                    string navigationFailure;
                    if (!_navigation.BuildTarget(target.NpcId, graphs[target.NpcId], target.WorldObject, target, out navigationFailure))
                    {
                        failure = "navigation bootstrap failed: " + navigationFailure;
                        ClearCaches();
                        return false;
                    }
                }

                string verifiedFailure;
                if (!_navigation.ValidateVerifiedContracts(out verifiedFailure))
                {
                    failure = "navigation verification failed: " + verifiedFailure;
                    ClearCaches();
                    return false;
                }

                string selfFailure;
                UnifiedSelfConsumingCompiler.Stats selfStats;
                if (!_selfConsumingCompiler.Compile(targets, graphs, _cache, _navigation, out selfStats, out selfFailure))
                {
                    failure = "unified self-consuming bootstrap failed: " + (selfFailure ?? "<unknown>");
                    ClearCaches();
                    return false;
                }
                _nonAtStats = selfStats;

                var counts = CaptureCounts();
                if (!CountsAreCanonical(counts, _nonAtStats))
                {
                    failure = "bootstrap produced non-canonical rule counts: " + CountsToString(counts, _nonAtStats);
                    ClearCaches();
                    return false;
                }
                ApplyCounts(counts);

                ulong ignoredFingerprint;
                bool ignoredPeriodic;
                if (!Bind(save, mainGame, out ignoredFingerprint, out ignoredPeriodic))
                {
                    failure = "bootstrap completed but live binding failed";
                    ClearCaches();
                    return false;
                }

                string persistFailure;
                if (!TryPersist(out persistFailure)) failure = "bootstrap succeeded in memory, but " + persistFailure;
                return true;
            }
            catch (TargetInvocationException ex)
            {
                var inner = ex.InnerException ?? ex;
                failure = inner.GetType().Name + ": " + inner.Message;
                ClearCaches();
                return false;
            }
            catch (Exception ex)
            {
                failure = ex.GetType().Name + ": " + ex.Message;
                ClearCaches();
                return false;
            }
            finally
            {
                sw.Stop();
                elapsedMs = sw.Elapsed.TotalMilliseconds;
            }
        }

        internal bool Bind(object save, object mainGame, out ulong fingerprint, out bool hasPeriodicNpc)
        {
            fingerprint = 0UL;
            hasPeriodicNpc = false;
            if (save == null || mainGame == null || !InvokeTryBindPlayer(mainGame)) return false;
            int knownCount;
            var knownNpcMap = ReadKnownNpcs(save, out knownCount);
            var targets = GetTargets();
            if (targets == null || targets.Count != NpcIds.Length || !_navigation.IsReadyForTargets(targets)) return false;

            for (var i = 0; i < targets.Count; i++)
            {
                var target = targets[i];
                if (target == null || !ReflectionUtil.IsUnityAlive(target.WorldObject)) return false;
                object knownNpc;
                knownNpcMap.TryGetValue(target.NpcId, out knownNpc);
                target.KnownNpc = knownNpc;
                if (knownNpc != null) hasPeriodicNpc = true;
                for (var c = 0; c < target.CrossTasks.Count; c++)
                {
                    var cross = target.CrossTasks[c];
                    if (cross == null) return false;
                    object owner;
                    knownNpcMap.TryGetValue(cross.OwnerNpcId, out owner);
                    cross.KnownNpc = owner;
                }
            }

            _saveField.SetValue(_cache, save);
            _knownNpcCountField.SetValue(_cache, knownCount);
            _boundKnownNpcCount = knownCount;
            fingerprint = BuildKnownNpcFingerprint(save, out hasPeriodicNpc);
            return true;
        }

        internal static ulong BuildKnownNpcFingerprint(object save, out bool hasPeriodicNpc)
        {
            hasPeriodicNpc = false;
            const ulong offset = 14695981039346656037UL;
            const ulong prime = 1099511628211UL;
            var hash = offset;
            object known;
            if (save == null || !ReflectionUtil.TryRead(save, "known_npcs", out known) || known == null) return hash;
            var npcs = ReflectionUtil.EnumerateMember(known, "npcs");
            if (npcs == null) return hash;
            foreach (var npc in npcs)
            {
                if (npc == null) continue;
                var id = ReflectionUtil.ReadString(npc, "npc_id");
                if (string.IsNullOrEmpty(id)) continue;
                for (var i = 0; i < id.Length; i++) { hash ^= id[i]; hash *= prime; }
                hash ^= 0x1FUL;
                hash *= prime;
                if (IsPeriodicNpc(id)) hasPeriodicNpc = true;
            }
            return hash;
        }

        private bool TryPersist(out string failure)
        {
            failure = null;
            try
            {
                var directory = Path.GetDirectoryName(_path);
                if (!string.IsNullOrEmpty(directory)) Directory.CreateDirectory(directory);
                var temp = _path + ".tmp";
                using (var stream = new FileStream(temp, FileMode.Create, FileAccess.Write, FileShare.None))
                using (var writer = new BinaryWriter(stream))
                {
                    writer.Write(Magic);
                    writer.Write(SchemaVersion);
                    writer.Write(VerifiedGameVersion);
                    var targets = GetTargets();
                    writer.Write(targets.Count);
                    for (var i = 0; i < targets.Count; i++)
                    {
                        var target = targets[i];
                        writer.Write(target.NpcId ?? string.Empty);
                        var taskIds = new List<string>(target.OwnerTaskRules.Keys);
                        taskIds.Sort(StringComparer.Ordinal);
                        writer.Write(taskIds.Count);
                        for (var t = 0; t < taskIds.Count; t++)
                        {
                            var taskId = taskIds[t];
                            writer.Write(taskId);
                            WriteVariants(writer, target.OwnerTaskRules[taskId]);
                        }
                        writer.Write(target.CrossTasks.Count);
                        for (var c = 0; c < target.CrossTasks.Count; c++)
                        {
                            var cross = target.CrossTasks[c];
                            writer.Write(cross.OwnerNpcId ?? string.Empty);
                            writer.Write(cross.TaskId ?? string.Empty);
                            WriteVariants(writer, cross.Rules);
                        }
                        writer.Write(target.Topics.Count);
                        for (var p = 0; p < target.Topics.Count; p++)
                        {
                            var topic = target.Topics[p];
                            writer.Write(topic.AnswerId ?? string.Empty);
                            WriteVariants(writer, topic.Variants);
                        }
                    }
                    _navigation.Write(writer);
                    WriteCounts(writer, CaptureCounts());
                    WriteNonAtStats(writer, _nonAtStats);
                    writer.Flush();
                    stream.Flush(true);
                }
                if (File.Exists(_path)) File.Delete(_path);
                File.Move(temp, _path);
                return true;
            }
            catch (Exception ex)
            {
                failure = "could not persist schema-4 manifest: " + ex.GetType().Name + ": " + ex.Message;
                try { if (File.Exists(_path + ".tmp")) File.Delete(_path + ".tmp"); } catch { }
                return false;
            }
        }

        private bool PrepareRuntime(object save, object mainGame)
        {
            if (save == null || mainGame == null || _targetsField == null || _saveField == null ||
                _playerField == null || _knownNpcCountField == null || _worldObjectGetterField == null ||
                _smartResFactoryField == null || _findWorldObjectGetter == null || _findSmartResFactory == null ||
                _tryBindPlayer == null || _parseGraph == null || _createSmartRes == null ||
                _worldObjectGetter == null || _controllerType == null) return false;
            var gameGetter = _findWorldObjectGetter.Invoke(_cache, null) as MethodInfo;
            var smartResFactory = _findSmartResFactory.Invoke(_cache, null) as MethodInfo;
            if (gameGetter == null || smartResFactory == null) return false;
            _worldObjectGetterField.SetValue(_cache, gameGetter);
            _smartResFactoryField.SetValue(_cache, smartResFactory);
            return InvokeTryBindPlayer(mainGame);
        }

        private bool InvokeTryBindPlayer(object mainGame)
        {
            try
            {
                var result = _tryBindPlayer.Invoke(_cache, new[] { mainGame });
                return result is bool && (bool)result;
            }
            catch { return false; }
        }

        private string ReadSerializedGraph(string npcId)
        {
            var wgo = GetWorldObject(npcId) as Component;
            if (wgo == null) return null;
            var controller = wgo.GetComponent(_controllerType);
            object graph;
            if (controller == null || !ReflectionUtil.TryRead(controller, "_graph", out graph) || graph == null) return null;
            object serialized;
            return ReflectionUtil.TryRead(graph, "_serializedGraph", out serialized) ? serialized as string : null;
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
                var parameters = method.GetParameters();
                if (parameters.Length == 2 && parameters[0].ParameterType == typeof(string) && parameters[1].ParameterType == typeof(bool)) return method;
            }
            return null;
        }

        private List<WeekdayInteractionRuleCache.TargetRules> GetTargets()
        {
            return _targetsField == null ? null : _targetsField.GetValue(_cache) as List<WeekdayInteractionRuleCache.TargetRules>;
        }

        private bool ReadVariants(BinaryReader reader, object linkedWgo,
            List<WeekdayInteractionRuleCache.RuleVariant> destination, out string failure)
        {
            failure = null;
            var count = reader.ReadInt32();
            if (count < 0 || count > 256) { failure = "variant count out of range"; return false; }
            for (var i = 0; i < count; i++)
            {
                var variant = new WeekdayInteractionRuleCache.RuleVariant
                {
                    AnswerId = reader.ReadString(),
                    Unsupported = reader.ReadBoolean()
                };
                bool valid;
                variant.Price = ReadRequirement(reader, linkedWgo, out valid);
                if (!valid) { failure = "invalid price requirement for " + variant.AnswerId; return false; }
                variant.Lock = ReadRequirement(reader, linkedWgo, out valid);
                if (!valid) { failure = "invalid lock requirement for " + variant.AnswerId; return false; }
                destination.Add(variant);
            }
            return true;
        }

        private WeekdayInteractionRuleCache.Requirement ReadRequirement(BinaryReader reader, object linkedWgo, out bool valid)
        {
            valid = true;
            if (!reader.ReadBoolean()) return null;
            var requirement = new WeekdayInteractionRuleCache.Requirement
            {
                ResType = reader.ReadString(),
                Id = reader.ReadString(),
                Value = reader.ReadSingle(),
                AuthoritativeZoneId = ReadNullableString(reader)
            };
            try { requirement.SmartRes = _createSmartRes.Invoke(_cache, new object[] { requirement, linkedWgo }); }
            catch { requirement.SmartRes = null; }
            if (requirement.SmartRes == null && string.IsNullOrEmpty(requirement.AuthoritativeZoneId)) valid = false;
            return requirement;
        }

        private static void WriteVariants(BinaryWriter writer, List<WeekdayInteractionRuleCache.RuleVariant> variants)
        {
            writer.Write(variants.Count);
            for (var i = 0; i < variants.Count; i++)
            {
                var variant = variants[i];
                writer.Write(variant.AnswerId ?? string.Empty);
                writer.Write(variant.Unsupported);
                WriteRequirement(writer, variant.Price);
                WriteRequirement(writer, variant.Lock);
            }
        }

        private static void WriteRequirement(BinaryWriter writer, WeekdayInteractionRuleCache.Requirement requirement)
        {
            writer.Write(requirement != null);
            if (requirement == null) return;
            writer.Write(requirement.ResType ?? string.Empty);
            writer.Write(requirement.Id ?? string.Empty);
            writer.Write(requirement.Value);
            WriteNullableString(writer, requirement.AuthoritativeZoneId);
        }

        private static void WriteNullableString(BinaryWriter writer, string value)
        {
            writer.Write(value != null);
            if (value != null) writer.Write(value);
        }

        private static string ReadNullableString(BinaryReader reader)
        {
            return reader.ReadBoolean() ? reader.ReadString() : null;
        }

        private sealed class Counts
        {
            internal int OwnerSupported;
            internal int OwnerUnsupported;
            internal int CrossTasks;
            internal int CrossSupported;
            internal int CrossUnsupported;
            internal int Topics;
            internal int TopicSupported;
            internal int TopicUnsupported;
        }

        private Counts CaptureCounts()
        {
            var topics = 0;
            var supported = 0;
            var unsupported = 0;
            var targets = GetTargets();
            if (targets != null)
            {
                for (var t = 0; t < targets.Count; t++)
                {
                    var target = targets[t];
                    if (target == null) continue;
                    topics += target.Topics.Count;
                    for (var p = 0; p < target.Topics.Count; p++)
                    {
                        var topic = target.Topics[p];
                        if (topic == null) continue;
                        for (var v = 0; v < topic.Variants.Count; v++)
                        {
                            var variant = topic.Variants[v];
                            if (variant != null && variant.Unsupported) unsupported++; else supported++;
                        }
                    }
                }
            }

            return new Counts
            {
                OwnerSupported = _cache.OwnerSupportedRuleCount,
                OwnerUnsupported = _cache.OwnerUnsupportedRuleCount,
                CrossTasks = _cache.CrossTaskCount,
                CrossSupported = _cache.CrossSupportedRuleCount,
                CrossUnsupported = _cache.CrossUnsupportedRuleCount,
                Topics = topics,
                TopicSupported = supported,
                TopicUnsupported = unsupported
            };
        }

        private static void WriteCounts(BinaryWriter writer, Counts counts)
        {
            writer.Write(counts.OwnerSupported);
            writer.Write(counts.OwnerUnsupported);
            writer.Write(counts.CrossTasks);
            writer.Write(counts.CrossSupported);
            writer.Write(counts.CrossUnsupported);
            writer.Write(counts.Topics);
            writer.Write(counts.TopicSupported);
            writer.Write(counts.TopicUnsupported);
        }

        private static Counts ReadCounts(BinaryReader reader)
        {
            return new Counts
            {
                OwnerSupported = reader.ReadInt32(),
                OwnerUnsupported = reader.ReadInt32(),
                CrossTasks = reader.ReadInt32(),
                CrossSupported = reader.ReadInt32(),
                CrossUnsupported = reader.ReadInt32(),
                Topics = reader.ReadInt32(),
                TopicSupported = reader.ReadInt32(),
                TopicUnsupported = reader.ReadInt32()
            };
        }

        private static void WriteNonAtStats(BinaryWriter writer, UnifiedSelfConsumingCompiler.Stats stats)
        {
            if (stats == null) stats = new UnifiedSelfConsumingCompiler.Stats();
            writer.Write(stats.GraphCount);
            writer.Write(stats.NonAtUnique);
            writer.Write(stats.ExactSelf);
            writer.Write(stats.Reversible);
            writer.Write(stats.Utility);
            writer.Write(stats.CompletionExcluded);
            writer.Write(stats.AdmittedTopics);
            writer.Write(stats.SupportedVariants);
            writer.Write(stats.UnsupportedVariants);
        }

        private static UnifiedSelfConsumingCompiler.Stats ReadNonAtStats(BinaryReader reader)
        {
            var stats = new UnifiedSelfConsumingCompiler.Stats
            {
                GraphCount = reader.ReadInt32(),
                NonAtUnique = reader.ReadInt32(),
                ExactSelf = reader.ReadInt32(),
                Reversible = reader.ReadInt32(),
                Utility = reader.ReadInt32(),
                CompletionExcluded = reader.ReadInt32(),
                AdmittedTopics = reader.ReadInt32(),
                SupportedVariants = reader.ReadInt32(),
                UnsupportedVariants = reader.ReadInt32()
            };
            // Schema 3 already stores enough information to reconstruct the fail-closed
            // root-unreachable subset without changing the binary layout.
            stats.NavigationExcluded = Math.Max(0, stats.ExactSelf - stats.CompletionExcluded - stats.AdmittedTopics);
            return stats;
        }

        private void ApplyCounts(Counts counts)
        {
            SetAutoPropertyBackingField("OwnerSupportedRuleCount", counts.OwnerSupported);
            SetAutoPropertyBackingField("OwnerUnsupportedRuleCount", counts.OwnerUnsupported);
            SetAutoPropertyBackingField("CrossTaskCount", counts.CrossTasks);
            SetAutoPropertyBackingField("CrossSupportedRuleCount", counts.CrossSupported);
            SetAutoPropertyBackingField("CrossUnsupportedRuleCount", counts.CrossUnsupported);
            SetAutoPropertyBackingField("OneShotTopicCount", counts.Topics);
            SetAutoPropertyBackingField("OneShotSupportedRuleCount", counts.TopicSupported);
            SetAutoPropertyBackingField("OneShotUnsupportedRuleCount", counts.TopicUnsupported);
        }

        private void SetAutoPropertyBackingField(string propertyName, int value)
        {
            var field = _cacheType.GetField("<" + propertyName + ">k__BackingField", BindingFlags.Instance | BindingFlags.NonPublic);
            if (field != null) field.SetValue(_cache, value);
        }

        private static bool CountsAreCanonical(Counts counts, UnifiedSelfConsumingCompiler.Stats stats)
        {
            string ignored;
            if (counts == null || !UnifiedSelfConsumingCompiler.Validate(stats, out ignored)) return false;
            return counts.OwnerSupported == ExpectedOwnerSupported && counts.OwnerUnsupported == ExpectedOwnerUnsupported &&
                   counts.CrossTasks == ExpectedCrossTasks && counts.CrossSupported == ExpectedCrossSupported &&
                   counts.CrossUnsupported == ExpectedCrossUnsupported &&
                   counts.Topics - stats.AdmittedTopics == ExpectedAtTopics &&
                   counts.TopicSupported - stats.SupportedVariants == ExpectedAtTopicSupported &&
                   counts.TopicUnsupported - stats.UnsupportedVariants == ExpectedAtTopicUnsupported;
        }

        private static string CountsToString(Counts counts, UnifiedSelfConsumingCompiler.Stats stats)
        {
            if (counts == null) return "<null>";
            return "owner=" + counts.OwnerSupported + "/" + counts.OwnerUnsupported +
                   ", cross=" + counts.CrossTasks + "/" + counts.CrossSupported + "/" + counts.CrossUnsupported +
                   ", self-consuming=" + counts.Topics + "/" + counts.TopicSupported + "/" + counts.TopicUnsupported +
                   ", nonAt=" + (stats == null ? "<null>" :
                       stats.AdmittedTopics + "/" + stats.SupportedVariants + "/" + stats.UnsupportedVariants +
                       ", raw=" + stats.ExactSelf + ", excluded=" + stats.CompletionExcluded);
        }

        private void ClearCaches()
        {
            _cache.Clear();
            _navigation.Clear();
            _nonAtStats = new UnifiedSelfConsumingCompiler.Stats();
        }

        private static Dictionary<string, object> ReadKnownNpcs(object save, out int count)
        {
            count = 0;
            var result = new Dictionary<string, object>(StringComparer.Ordinal);
            if (save == null) return result;
            object known;
            if (!ReflectionUtil.TryRead(save, "known_npcs", out known) || known == null) return result;
            var npcs = ReflectionUtil.EnumerateMember(known, "npcs");
            if (npcs == null) return result;
            foreach (var npc in npcs)
            {
                if (npc == null) continue;
                count++;
                var id = ReflectionUtil.ReadString(npc, "npc_id");
                if (!string.IsNullOrEmpty(id)) result[id] = npc;
            }
            return result;
        }

        private static int CountKnownNpcsNoAlloc(object save)
        {
            if (save == null) return 0;
            object known;
            if (!ReflectionUtil.TryRead(save, "known_npcs", out known) || known == null) return 0;
            var npcs = ReflectionUtil.EnumerateMember(known, "npcs");
            if (npcs == null) return 0;
            var collection = npcs as ICollection;
            if (collection != null) return collection.Count;
            var count = 0;
            foreach (var npc in npcs) if (npc != null) count++;
            return count;
        }

        private static bool IsPeriodicNpc(string npcId)
        {
            for (var i = 0; i < NpcIds.Length; i++)
                if (string.Equals(npcId, NpcIds[i], StringComparison.Ordinal)) return true;
            return false;
        }

        private static string ReadGameVersion(object save)
        {
            object raw;
            if (save == null || !ReflectionUtil.TryRead(save, "game_version", out raw) || raw == null) return null;
            try
            {
                var formattable = raw as IFormattable;
                return formattable != null ? formattable.ToString(null, CultureInfo.InvariantCulture) : raw.ToString();
            }
            catch { return null; }
        }
    }
}

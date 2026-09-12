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
    /// Persistent, pure-data structural manifest for the verified Graveyard Keeper 1.407 weekday graphs.
    ///
    /// The expensive graph parser remains only as a bootstrap generator on a cache miss. Once generated,
    /// subsequent saves/process launches deserialize compact rule data and only bind live Player/KnownNpc/WGO
    /// references. No FlowCanvas graph parsing is required on the normal runtime path.
    /// </summary>
    internal sealed class PersistentRuleManifest
    {
        internal const string VerifiedGameVersion = "1.407";
        private const string Magic = "DWQM_RULE_MANIFEST";
        private const int SchemaVersion = 1;

        // Canonical counts from accepted 1.0.24 runtime evidence. A bootstrap result that differs is not persisted.
        private const int ExpectedOwnerSupported = 75;
        private const int ExpectedOwnerUnsupported = 6;
        private const int ExpectedCrossTasks = 8;
        private const int ExpectedCrossSupported = 6;
        private const int ExpectedCrossUnsupported = 0;
        private const int ExpectedTopics = 55;
        private const int ExpectedTopicSupported = 54;
        private const int ExpectedTopicUnsupported = 1;

        private static readonly string[] NpcIds =
        {
            "npc_astrologer", "npc_inquisitor", "npc_cultist", "npc_merchant", "npc_actress", "npc_bishop"
        };

        private static readonly Regex OwnerNpcRegex = new Regex(
            "\"NPC id\":\\{\\\"\\$content\\\":\\\"([^\\\"]+)\\\"",
            RegexOptions.CultureInvariant | RegexOptions.Compiled);

        private readonly WeekdayInteractionRuleCache _cache;
        private readonly Type _cacheType = typeof(WeekdayInteractionRuleCache);
        private readonly Type _controllerType = ReflectionUtil.FindType("FlowCanvas.FlowScriptController");
        private readonly Type _worldMapType = ReflectionUtil.FindType("WorldMap");

        private readonly FieldInfo _targetsField;
        private readonly FieldInfo _saveField;
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

        internal string ManifestPath { get { return _path; } }

        internal PersistentRuleManifest(WeekdayInteractionRuleCache cache)
        {
            if (cache == null) throw new ArgumentNullException("cache");
            _cache = cache;

            const BindingFlags instance = BindingFlags.Instance | BindingFlags.NonPublic;
            _targetsField = _cacheType.GetField("_targets", instance);
            _saveField = _cacheType.GetField("_save", instance);
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

        internal bool HasPersistedManifest()
        {
            try { return File.Exists(_path); }
            catch { return false; }
        }

        internal bool KnownNpcCountChanged(object save)
        {
            return CountKnownNpcs(save) != _boundKnownNpcCount;
        }

        internal bool IsRuntimeValid(object mainGame)
        {
            if (mainGame == null || !InvokeTryBindPlayer(mainGame)) return false;
            var targets = GetTargets();
            if (targets == null || targets.Count != NpcIds.Length) return false;
            for (var i = 0; i < targets.Count; i++)
            {
                var target = targets[i];
                if (target == null || !ReflectionUtil.IsUnityAlive(target.WorldObject)) return false;
            }
            return true;
        }

        internal bool TryLoad(object save, object mainGame, out double elapsedMs, out string failure)
        {
            var sw = Stopwatch.StartNew();
            failure = null;
            try
            {
                if (!File.Exists(_path))
                {
                    failure = "manifest file is missing";
                    return false;
                }
                if (!PrepareRuntime(save, mainGame))
                {
                    failure = "runtime bindings are not ready";
                    return false;
                }

                using (var stream = new FileStream(_path, FileMode.Open, FileAccess.Read, FileShare.Read))
                using (var reader = new BinaryReader(stream))
                {
                    if (!string.Equals(reader.ReadString(), Magic, StringComparison.Ordinal))
                    {
                        failure = "manifest magic mismatch";
                        return false;
                    }
                    if (reader.ReadInt32() != SchemaVersion)
                    {
                        failure = "manifest schema mismatch";
                        return false;
                    }
                    if (!string.Equals(reader.ReadString(), VerifiedGameVersion, StringComparison.Ordinal))
                    {
                        failure = "manifest game version mismatch";
                        return false;
                    }

                    var gameVersion = ReadGameVersion(save);
                    if (!string.Equals(gameVersion, VerifiedGameVersion, StringComparison.Ordinal))
                    {
                        failure = "loaded save game version is " + (gameVersion ?? "<unknown>");
                        return false;
                    }

                    _cache.Clear();
                    if (!PrepareRuntime(save, mainGame))
                    {
                        failure = "runtime bindings were lost during manifest load";
                        return false;
                    }

                    var targets = GetTargets();
                    if (targets == null)
                    {
                        failure = "cache target storage unavailable";
                        return false;
                    }

                    var targetCount = reader.ReadInt32();
                    if (targetCount != NpcIds.Length)
                    {
                        failure = "manifest target count mismatch";
                        return false;
                    }

                    for (var i = 0; i < targetCount; i++)
                    {
                        var npcId = reader.ReadString();
                        if (!string.Equals(npcId, NpcIds[i], StringComparison.Ordinal))
                        {
                            failure = "manifest target order/id mismatch at index " + i;
                            return false;
                        }

                        var wgo = GetWorldObject(npcId);
                        if (!ReflectionUtil.IsUnityAlive(wgo))
                        {
                            failure = "weekday NPC world object not ready: " + npcId;
                            return false;
                        }

                        var target = new WeekdayInteractionRuleCache.TargetRules
                        {
                            NpcId = npcId,
                            WorldObject = wgo
                        };

                        var ownerTaskCount = reader.ReadInt32();
                        if (ownerTaskCount < 0 || ownerTaskCount > 256)
                        {
                            failure = "owner task count out of range";
                            return false;
                        }
                        for (var t = 0; t < ownerTaskCount; t++)
                        {
                            var taskId = reader.ReadString();
                            var rules = new List<WeekdayInteractionRuleCache.RuleVariant>();
                            if (!ReadVariants(reader, wgo, rules, out failure)) return false;
                            target.OwnerTaskRules[taskId] = rules;
                        }

                        var crossTaskCount = reader.ReadInt32();
                        if (crossTaskCount < 0 || crossTaskCount > 128)
                        {
                            failure = "cross-task count out of range";
                            return false;
                        }
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
                        if (topicCount < 0 || topicCount > 256)
                        {
                            failure = "topic count out of range";
                            return false;
                        }
                        for (var p = 0; p < topicCount; p++)
                        {
                            var topic = new WeekdayInteractionRuleCache.TopicRule { AnswerId = reader.ReadString() };
                            if (!ReadVariants(reader, wgo, topic.Variants, out failure)) return false;
                            target.Topics.Add(topic);
                        }

                        targets.Add(target);
                    }

                    var counts = ReadCounts(reader);
                    if (!CountsAreCanonical(counts))
                    {
                        failure = "manifest canonical-count check failed";
                        return false;
                    }
                    ApplyCounts(counts);

                    if (stream.Position != stream.Length)
                    {
                        failure = "manifest has trailing data";
                        return false;
                    }
                }

                string ignoredSignature;
                bool ignoredPeriodic;
                if (!Bind(save, mainGame, out ignoredSignature, out ignoredPeriodic))
                {
                    failure = "manifest loaded but live binding failed";
                    return false;
                }

                return IsRuntimeValid(mainGame);
            }
            catch (Exception ex)
            {
                failure = ex.GetType().Name + ": " + ex.Message;
                _cache.Clear();
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
                {
                    failure = "bootstrap is supported only for Graveyard Keeper " + VerifiedGameVersion;
                    return false;
                }

                _cache.Clear();
                if (!PrepareRuntime(save, mainGame))
                {
                    failure = "runtime bindings are not ready for bootstrap";
                    return false;
                }

                var knownNpcMap = ReadKnownNpcs(save, out _boundKnownNpcCount);
                var graphs = new Dictionary<string, string>(StringComparer.Ordinal);

                for (var i = 0; i < NpcIds.Length; i++)
                {
                    var npcId = NpcIds[i];
                    var serialized = ReadSerializedGraph(npcId);
                    if (string.IsNullOrEmpty(serialized))
                    {
                        failure = "serialized graph not ready: " + npcId;
                        return false;
                    }
                    graphs[npcId] = serialized;

                    if (!knownNpcMap.ContainsKey(npcId))
                        knownNpcMap[npcId] = new object();

                    foreach (Match match in OwnerNpcRegex.Matches(serialized))
                    {
                        if (!match.Success || match.Groups.Count < 2) continue;
                        var ownerNpcId = match.Groups[1].Value;
                        if (string.IsNullOrEmpty(ownerNpcId) || knownNpcMap.ContainsKey(ownerNpcId)) continue;
                        knownNpcMap[ownerNpcId] = new object();
                    }
                }

                var targets = GetTargets();
                if (targets == null)
                {
                    failure = "cache target storage unavailable";
                    return false;
                }

                for (var i = 0; i < NpcIds.Length; i++)
                {
                    var npcId = NpcIds[i];
                    var wgo = GetWorldObject(npcId);
                    if (!ReflectionUtil.IsUnityAlive(wgo))
                    {
                        failure = "weekday NPC world object not ready: " + npcId;
                        return false;
                    }

                    object knownNpc;
                    knownNpcMap.TryGetValue(npcId, out knownNpc);
                    var target = new WeekdayInteractionRuleCache.TargetRules
                    {
                        NpcId = npcId,
                        KnownNpc = knownNpc,
                        WorldObject = wgo
                    };

                    _parseGraph.Invoke(_cache, new object[] { target, graphs[npcId], knownNpcMap });
                    targets.Add(target);
                }

                var counts = CaptureCounts();
                if (!CountsAreCanonical(counts))
                {
                    failure = "bootstrap produced non-canonical counts: " + CountsToString(counts);
                    _cache.Clear();
                    return false;
                }

                string ignoredSignature;
                bool ignoredPeriodic;
                if (!Bind(save, mainGame, out ignoredSignature, out ignoredPeriodic))
                {
                    failure = "bootstrap completed but live binding failed";
                    _cache.Clear();
                    return false;
                }

                string persistFailure;
                if (!TryPersist(out persistFailure))
                    failure = "bootstrap succeeded in memory, but " + persistFailure;

                return true;
            }
            catch (TargetInvocationException ex)
            {
                var inner = ex.InnerException ?? ex;
                failure = inner.GetType().Name + ": " + inner.Message;
                _cache.Clear();
                return false;
            }
            catch (Exception ex)
            {
                failure = ex.GetType().Name + ": " + ex.Message;
                _cache.Clear();
                return false;
            }
            finally
            {
                sw.Stop();
                elapsedMs = sw.Elapsed.TotalMilliseconds;
            }
        }

        internal bool Bind(object save, object mainGame, out string signature, out bool hasPeriodicNpc)
        {
            signature = null;
            hasPeriodicNpc = false;
            if (save == null || mainGame == null || !InvokeTryBindPlayer(mainGame)) return false;

            int knownCount;
            var knownNpcMap = ReadKnownNpcs(save, out knownCount);
            var targets = GetTargets();
            if (targets == null || targets.Count != NpcIds.Length) return false;

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
            signature = WeekdayInteractionRuleCache.BuildKnownNpcSignature(save, out hasPeriodicNpc);
            return true;
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

                    WriteCounts(writer, CaptureCounts());
                    writer.Flush();
                    stream.Flush(true);
                }

                if (File.Exists(_path)) File.Delete(_path);
                File.Move(temp, _path);
                return true;
            }
            catch (Exception ex)
            {
                failure = "could not persist manifest: " + ex.GetType().Name + ": " + ex.Message;
                try { if (File.Exists(_path + ".tmp")) File.Delete(_path + ".tmp"); } catch { }
                return false;
            }
        }

        private bool PrepareRuntime(object save, object mainGame)
        {
            if (save == null || mainGame == null ||
                _targetsField == null || _saveField == null || _knownNpcCountField == null ||
                _worldObjectGetterField == null || _smartResFactoryField == null ||
                _findWorldObjectGetter == null || _findSmartResFactory == null ||
                _tryBindPlayer == null || _parseGraph == null || _createSmartRes == null ||
                _worldObjectGetter == null || _controllerType == null)
                return false;

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
                var p = method.GetParameters();
                if (p.Length == 2 && p[0].ParameterType == typeof(string) && p[1].ParameterType == typeof(bool))
                    return method;
            }
            return null;
        }

        private List<WeekdayInteractionRuleCache.TargetRules> GetTargets()
        {
            return _targetsField == null
                ? null
                : _targetsField.GetValue(_cache) as List<WeekdayInteractionRuleCache.TargetRules>;
        }

        private bool ReadVariants(BinaryReader reader, object linkedWgo,
            List<WeekdayInteractionRuleCache.RuleVariant> destination, out string failure)
        {
            failure = null;
            var count = reader.ReadInt32();
            if (count < 0 || count > 256)
            {
                failure = "variant count out of range";
                return false;
            }

            for (var i = 0; i < count; i++)
            {
                var variant = new WeekdayInteractionRuleCache.RuleVariant
                {
                    AnswerId = reader.ReadString(),
                    Unsupported = reader.ReadBoolean()
                };

                bool valid;
                variant.Price = ReadRequirement(reader, linkedWgo, out valid);
                if (!valid)
                {
                    failure = "invalid price requirement for " + variant.AnswerId;
                    return false;
                }

                variant.Lock = ReadRequirement(reader, linkedWgo, out valid);
                if (!valid)
                {
                    failure = "invalid lock requirement for " + variant.AnswerId;
                    return false;
                }

                destination.Add(variant);
            }
            return true;
        }

        private WeekdayInteractionRuleCache.Requirement ReadRequirement(BinaryReader reader, object linkedWgo, out bool valid)
        {
            valid = true;
            if (!reader.ReadBoolean()) return null;

            var req = new WeekdayInteractionRuleCache.Requirement
            {
                ResType = reader.ReadString(),
                Id = reader.ReadString(),
                Value = reader.ReadSingle(),
                AuthoritativeZoneId = ReadNullableString(reader)
            };

            try
            {
                req.SmartRes = _createSmartRes.Invoke(_cache, new object[] { req, linkedWgo });
            }
            catch
            {
                req.SmartRes = null;
            }

            if (req.SmartRes == null && string.IsNullOrEmpty(req.AuthoritativeZoneId))
                valid = false;
            return req;
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

        private static void WriteRequirement(BinaryWriter writer, WeekdayInteractionRuleCache.Requirement req)
        {
            writer.Write(req != null);
            if (req == null) return;
            writer.Write(req.ResType ?? string.Empty);
            writer.Write(req.Id ?? string.Empty);
            writer.Write(req.Value);
            WriteNullableString(writer, req.AuthoritativeZoneId);
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
            return new Counts
            {
                OwnerSupported = _cache.OwnerSupportedRuleCount,
                OwnerUnsupported = _cache.OwnerUnsupportedRuleCount,
                CrossTasks = _cache.CrossTaskCount,
                CrossSupported = _cache.CrossSupportedRuleCount,
                CrossUnsupported = _cache.CrossUnsupportedRuleCount,
                Topics = _cache.OneShotTopicCount,
                TopicSupported = _cache.OneShotSupportedRuleCount,
                TopicUnsupported = _cache.OneShotUnsupportedRuleCount
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
            var field = _cacheType.GetField("<" + propertyName + ">k__BackingField",
                BindingFlags.Instance | BindingFlags.NonPublic);
            if (field != null) field.SetValue(_cache, value);
        }

        private static bool CountsAreCanonical(Counts counts)
        {
            return counts != null &&
                   counts.OwnerSupported == ExpectedOwnerSupported &&
                   counts.OwnerUnsupported == ExpectedOwnerUnsupported &&
                   counts.CrossTasks == ExpectedCrossTasks &&
                   counts.CrossSupported == ExpectedCrossSupported &&
                   counts.CrossUnsupported == ExpectedCrossUnsupported &&
                   counts.Topics == ExpectedTopics &&
                   counts.TopicSupported == ExpectedTopicSupported &&
                   counts.TopicUnsupported == ExpectedTopicUnsupported;
        }

        private static string CountsToString(Counts c)
        {
            if (c == null) return "<null>";
            return "owner=" + c.OwnerSupported + "/" + c.OwnerUnsupported +
                   ", cross=" + c.CrossTasks + "/" + c.CrossSupported + "/" + c.CrossUnsupported +
                   ", topics=" + c.Topics + "/" + c.TopicSupported + "/" + c.TopicUnsupported;
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

        private static int CountKnownNpcs(object save)
        {
            int count;
            ReadKnownNpcs(save, out count);
            return count;
        }

        private static string ReadGameVersion(object save)
        {
            object raw;
            if (save == null || !ReflectionUtil.TryRead(save, "game_version", out raw) || raw == null) return null;
            try
            {
                var formattable = raw as IFormattable;
                return formattable != null
                    ? formattable.ToString(null, CultureInfo.InvariantCulture)
                    : raw.ToString();
            }
            catch { return null; }
        }
    }
}

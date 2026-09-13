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
    /// Loading-derived/persisted supplement for authored exact-self-consuming answers whose IDs do
    /// not use the persisted-topic '@' prefix. Graveyard Keeper 1.407 uses the same phrase blacklist
    /// for these answers, so the prefix is not a semantic one-shot boundary.
    ///
    /// Bootstrap is deliberately narrow: only the six weekday NPC graphs are scanned; a candidate
    /// must be a real MultiAnswer entry that reaches an add-to-blacklist operation for its exact own
    /// answer ID. Utility/reversible candidates invalidate the bootstrap. Gameplay performs no graph
    /// traversal: it evaluates the persisted answer, authored SmartRes gates, blacklist state, and the
    /// already-persisted NavigationReachabilityCache.
    /// </summary>
    internal sealed class NonAtSelfConsumingRuleCache
    {
        private const string Magic = "DWQM_NONAT_SELF";
        private const int SchemaVersion = 1;
        private const string VerifiedGameVersion = "1.407";
        private const int ExpectedGraphCount = 6;
        private const int ExpectedNonAtUnique = 77;
        private const int ExpectedExactSelfNonAt = 19;

        private static readonly string[] NpcIds =
        {
            "npc_astrologer", "npc_inquisitor", "npc_cultist", "npc_merchant", "npc_actress", "npc_bishop"
        };

        private sealed class Rule
        {
            internal string NpcId;
            internal string AnswerId;
            internal readonly List<Variant> Variants = new List<Variant>();
        }

        private sealed class Variant
        {
            internal bool Unsupported;
            internal Requirement Price;
            internal Requirement Lock;
        }

        private sealed class Requirement
        {
            internal string ResType;
            internal string Id;
            internal float Value;
            internal object SmartRes;
        }

        private sealed class Node
        {
            internal string Id;
            internal string Type;
            internal int TypePosition;
            internal string Uid;
            internal string SourceOutputUid;
        }

        private sealed class Connection
        {
            internal string SourcePort;
            internal string TargetPort;
            internal string SourceNode;
            internal string TargetNode;
        }

        private sealed class ExactAnchor
        {
            internal string MultiNodeId;
            internal int AnswerIndex = -1;
        }

        private sealed class CompletionAnchor
        {
            internal string EventIdentifier;
            internal string MultiNodeId;
            internal int AnswerIndex = -1;
        }

        private readonly Dictionary<string, List<Rule>> _rulesByNpc =
            new Dictionary<string, List<Rule>>(StringComparer.Ordinal);
        private readonly Type _worldMapType = ReflectionUtil.FindType("WorldMap");
        private readonly Type _controllerType = ReflectionUtil.FindType("FlowCanvas.FlowScriptController");
        private readonly Type _flowSmartResType = ReflectionUtil.FindType("FlowCanvas.Nodes.Flow_SmartRes");
        private readonly object[] _isEnoughArgs = new object[1];
        private readonly string _path;

        private MethodInfo _worldObjectGetter;
        private MethodInfo _smartResFactory;
        private MethodInfo _isEnough;
        private object _player;
        private bool _ready;

        internal int RuleCount { get; private set; }
        internal int SupportedVariantCount { get; private set; }
        internal int UnsupportedVariantCount { get; private set; }
        internal int CompletionExcludedCount { get; private set; }
        internal string CachePath { get { return _path; } }
        internal bool IsReady { get { return _ready; } }

        internal NonAtSelfConsumingRuleCache()
        {
            _path = Path.Combine(Paths.CachePath, "DayWheelQuestMarkers", "non-at-self-consuming-1.407.bin");
        }

        internal void Clear()
        {
            _rulesByNpc.Clear();
            _player = null;
            _isEnough = null;
            _worldObjectGetter = null;
            _smartResFactory = null;
            _ready = false;
            RuleCount = 0;
            SupportedVariantCount = 0;
            UnsupportedVariantCount = 0;
            CompletionExcludedCount = 0;
        }

        internal bool TryLoad(object mainGame, out double elapsedMs, out string failure)
        {
            var sw = Stopwatch.StartNew();
            failure = null;
            try
            {
                Clear();
                if (!File.Exists(_path)) { failure = "supplemental cache file is missing"; return false; }
                if (!PrepareRuntime(mainGame)) { failure = "runtime bindings are not ready"; return false; }

                using (var stream = new FileStream(_path, FileMode.Open, FileAccess.Read, FileShare.Read))
                using (var reader = new BinaryReader(stream))
                {
                    if (!string.Equals(reader.ReadString(), Magic, StringComparison.Ordinal))
                    { failure = "supplemental cache magic mismatch"; return false; }
                    if (reader.ReadInt32() != SchemaVersion)
                    { failure = "supplemental cache schema mismatch"; return false; }
                    if (!string.Equals(reader.ReadString(), VerifiedGameVersion, StringComparison.Ordinal))
                    { failure = "supplemental cache game version mismatch"; return false; }

                    CompletionExcludedCount = reader.ReadInt32();
                    var npcCount = reader.ReadInt32();
                    if (npcCount != NpcIds.Length) { failure = "supplemental NPC count mismatch"; return false; }

                    for (var n = 0; n < npcCount; n++)
                    {
                        var npcId = reader.ReadString();
                        if (!string.Equals(npcId, NpcIds[n], StringComparison.Ordinal))
                        { failure = "supplemental NPC order/id mismatch"; return false; }
                        var count = reader.ReadInt32();
                        if (count < 0 || count > 64) { failure = "supplemental rule count out of range"; return false; }
                        var list = new List<Rule>(count);
                        _rulesByNpc[npcId] = list;
                        for (var i = 0; i < count; i++)
                        {
                            var rule = new Rule { NpcId = npcId, AnswerId = reader.ReadString() };
                            var variants = reader.ReadInt32();
                            if (variants < 1 || variants > 16) { failure = "supplemental variant count out of range"; return false; }
                            for (var v = 0; v < variants; v++)
                            {
                                var variant = new Variant { Unsupported = reader.ReadBoolean() };
                                variant.Price = ReadRequirement(reader);
                                variant.Lock = ReadRequirement(reader);
                                rule.Variants.Add(variant);
                                if (variant.Unsupported) UnsupportedVariantCount++; else SupportedVariantCount++;
                            }
                            list.Add(rule);
                            RuleCount++;
                        }
                    }
                    if (stream.Position != stream.Length) { failure = "supplemental cache has trailing data"; return false; }
                }

                if (!BindRequirements()) { failure = "supplemental SmartRes binding failed"; return false; }
                _ready = true;
                return true;
            }
            catch (Exception ex)
            {
                failure = ex.GetType().Name + ": " + ex.Message;
                Clear();
                return false;
            }
            finally
            {
                sw.Stop();
                elapsedMs = sw.Elapsed.TotalMilliseconds;
            }
        }

        internal bool TryBootstrapAndPersist(object mainGame, out double elapsedMs, out string note)
        {
            var sw = Stopwatch.StartNew();
            note = null;
            try
            {
                Clear();
                if (!PrepareRuntime(mainGame)) { note = "supplemental runtime bindings are not ready"; return false; }

                var allNonAt = new HashSet<string>(StringComparer.Ordinal);
                var rawExactSelf = new HashSet<string>(StringComparer.Ordinal);
                var graphCount = 0;
                var reversibleCount = 0;
                var utilityCount = 0;

                for (var n = 0; n < NpcIds.Length; n++)
                {
                    var npcId = NpcIds[n];
                    string serialized;
                    object worldObject;
                    if (!TryGetSerializedGraph(npcId, out serialized, out worldObject))
                    { note = "weekday NPC graph unavailable: " + npcId; return false; }
                    graphCount++;

                    var nodes = BuildNodeIndex(serialized);
                    var connections = ParseConnections(serialized);
                    var productionIncoming = BuildIncomingFlow(nodes, connections, false);
                    var exactIncoming = BuildIncomingFlow(nodes, connections, true);
                    var callsByUid = BuildCallsByUid(nodes);
                    var removals = FindBlacklistRemovals(nodes, serialized);
                    var completionAnswerIds = FindProductionCompletionAnswerIds(nodes, productionIncoming, serialized);
                    var admitted = new Dictionary<string, Rule>(StringComparer.Ordinal);

                    foreach (var node in nodes.Values)
                    {
                        if (!node.Type.EndsWith("Flow_MultiAnswer", StringComparison.Ordinal)) continue;
                        var answers = ReadMultiAnswers(serialized, node);
                        for (var i = 0; i < answers.Count; i++)
                        {
                            var id = answers[i];
                            if (!string.IsNullOrEmpty(id) && !id.StartsWith("@", StringComparison.Ordinal)) allNonAt.Add(id);
                        }
                    }

                    foreach (var node in nodes.Values)
                    {
                        string blacklistedId;
                        if (!TryReadBlacklistAdd(node, serialized, out blacklistedId) ||
                            string.IsNullOrEmpty(blacklistedId) || blacklistedId.StartsWith("@", StringComparison.Ordinal)) continue;

                        var anchors = FindExactAnswerAnchors(nodes, exactIncoming, callsByUid, node.Id, 96);
                        for (var a = 0; a < anchors.Count; a++)
                        {
                            var anchor = anchors[a];
                            Node multi;
                            if (!nodes.TryGetValue(anchor.MultiNodeId, out multi)) continue;
                            var answers = ReadMultiAnswers(serialized, multi);
                            if (anchor.AnswerIndex < 0 || anchor.AnswerIndex >= answers.Count) continue;
                            var answerId = answers[anchor.AnswerIndex];
                            if (!string.Equals(answerId, blacklistedId, StringComparison.Ordinal)) continue;

                            var universeKey = npcId + "\n" + answerId;
                            if (!rawExactSelf.Add(universeKey)) continue;
                            if (removals.Contains(answerId)) reversibleCount++;
                            if (IsUtilityLike(answerId)) utilityCount++;
                            if (completionAnswerIds.Contains(answerId))
                            {
                                CompletionExcludedCount++;
                                continue;
                            }

                            var rule = BuildRule(npcId, answerId, anchor.MultiNodeId, anchor.AnswerIndex,
                                serialized, nodes, connections, worldObject);
                            admitted[answerId] = rule;
                        }
                    }

                    var list = new List<Rule>(admitted.Values);
                    list.Sort((x, y) => string.CompareOrdinal(x.AnswerId, y.AnswerId));
                    _rulesByNpc[npcId] = list;
                    RuleCount += list.Count;
                    for (var i = 0; i < list.Count; i++)
                    {
                        for (var v = 0; v < list[i].Variants.Count; v++)
                        {
                            if (list[i].Variants[v].Unsupported) UnsupportedVariantCount++;
                            else SupportedVariantCount++;
                        }
                    }
                }

                if (graphCount != ExpectedGraphCount || allNonAt.Count != ExpectedNonAtUnique ||
                    rawExactSelf.Count != ExpectedExactSelfNonAt || reversibleCount != 0 || utilityCount != 0)
                {
                    note = "supplemental universe integrity mismatch: graphs=" + graphCount + "/" + ExpectedGraphCount +
                           ", nonAtUnique=" + allNonAt.Count + "/" + ExpectedNonAtUnique +
                           ", exactSelf=" + rawExactSelf.Count + "/" + ExpectedExactSelfNonAt +
                           ", reversible=" + reversibleCount + ", utility=" + utilityCount;
                    Clear();
                    return false;
                }

                if (!BindRequirements()) { note = "supplemental SmartRes binding failed after bootstrap"; return false; }
                if (!Persist(out note)) { Clear(); return false; }
                _ready = true;
                note = "generalized non-@ exact-self cache: raw=" + rawExactSelf.Count +
                       ", completion-excluded=" + CompletionExcludedCount +
                       ", admitted=" + RuleCount +
                       ", supported variants=" + SupportedVariantCount +
                       ", unsupported variants=" + UnsupportedVariantCount;
                return true;
            }
            catch (Exception ex)
            {
                note = ex.GetType().Name + ": " + ex.Message;
                Clear();
                return false;
            }
            finally
            {
                sw.Stop();
                elapsedMs = sw.Elapsed.TotalMilliseconds;
            }
        }

        internal int CountActionable(string npcId, object unlockedPhrases, object blacklistedPhrases,
            NavigationReachabilityCache reachability, object mainGame)
        {
            if (!_ready || reachability == null || string.IsNullOrEmpty(npcId)) return 0;
            if (!EnsurePlayer(mainGame)) return 0;
            List<Rule> rules;
            if (!_rulesByNpc.TryGetValue(npcId, out rules) || rules == null) return 0;

            var count = 0;
            for (var i = 0; i < rules.Count; i++)
            {
                var rule = rules[i];
                if (rule == null || string.IsNullOrEmpty(rule.AnswerId) || ContainsString(blacklistedPhrases, rule.AnswerId)) continue;
                if (!reachability.IsNavigationReachable(npcId, rule.AnswerId, unlockedPhrases, blacklistedPhrases)) continue;
                if (!AnyVariantSatisfied(rule.Variants)) continue;
                count++;
            }
            return count;
        }

        private bool AnyVariantSatisfied(List<Variant> variants)
        {
            if (variants == null) return false;
            for (var i = 0; i < variants.Count; i++)
            {
                var variant = variants[i];
                if (variant == null || variant.Unsupported) continue;
                if (variant.Price != null && !IsEnough(variant.Price)) continue;
                if (variant.Lock != null && !IsEnough(variant.Lock)) continue;
                return true;
            }
            return false;
        }

        private bool PrepareRuntime(object mainGame)
        {
            if (_worldMapType == null || _controllerType == null || _flowSmartResType == null || mainGame == null) return false;
            _worldObjectGetter = FindWorldObjectGetter();
            _smartResFactory = FindSmartResFactory();
            return _worldObjectGetter != null && _smartResFactory != null && EnsurePlayer(mainGame);
        }

        private bool EnsurePlayer(object mainGame)
        {
            object player;
            if (mainGame == null || !ReflectionUtil.TryRead(mainGame, "player", out player) ||
                player == null || !ReflectionUtil.IsUnityAlive(player)) return false;
            if (ReferenceEquals(player, _player) && _isEnough != null) return true;

            _player = player;
            _isEnough = null;
            var smartResType = ReflectionUtil.FindType("SmartRes");
            if (smartResType == null) return false;
            foreach (var method in player.GetType().GetMethods(ReflectionUtil.AnyInstance))
            {
                var p = method.GetParameters();
                if (method.Name == "IsEnough" && p.Length == 1 && p[0].ParameterType.IsAssignableFrom(smartResType))
                {
                    _isEnough = method;
                    break;
                }
            }
            if (_isEnough == null) return false;
            return BindRequirements();
        }

        private bool BindRequirements()
        {
            if (_smartResFactory == null || _worldObjectGetter == null) return false;
            foreach (var pair in _rulesByNpc)
            {
                object wgo;
                try { wgo = _worldObjectGetter.Invoke(null, new object[] { pair.Key, true }); }
                catch { return false; }
                if (!ReflectionUtil.IsUnityAlive(wgo)) return false;
                var list = pair.Value;
                for (var i = 0; i < list.Count; i++)
                {
                    for (var v = 0; v < list[i].Variants.Count; v++)
                    {
                        var variant = list[i].Variants[v];
                        if (!BindRequirement(variant.Price, wgo) || !BindRequirement(variant.Lock, wgo)) return false;
                    }
                }
            }
            return true;
        }

        private bool BindRequirement(Requirement requirement, object linkedWgo)
        {
            if (requirement == null) return true;
            requirement.SmartRes = CreateSmartRes(requirement, linkedWgo);
            return requirement.SmartRes != null;
        }

        private object CreateSmartRes(Requirement requirement, object linkedWgo)
        {
            try
            {
                var target = _smartResFactory.IsStatic ? null : Activator.CreateInstance(_smartResFactory.DeclaringType);
                var parameters = _smartResFactory.GetParameters();
                var args = new object[parameters.Length];
                for (var i = 0; i < parameters.Length; i++)
                {
                    var t = parameters[i].ParameterType;
                    if (t.IsEnum) args[i] = Enum.Parse(t, requirement.ResType, false);
                    else if (t == typeof(string)) args[i] = requirement.Id;
                    else if (t == typeof(float)) args[i] = requirement.Value;
                    else if (t == typeof(double)) args[i] = (double)requirement.Value;
                    else if (t == typeof(int)) args[i] = (int)Math.Round(requirement.Value);
                    else return null;
                }
                var smartRes = _smartResFactory.Invoke(target, args);
                if (smartRes == null) return null;
                if (linkedWgo != null)
                {
                    var field = smartRes.GetType().GetField("_linked_wgo", ReflectionUtil.AnyInstance);
                    if (field != null && field.FieldType.IsInstanceOfType(linkedWgo)) field.SetValue(smartRes, linkedWgo);
                }
                return smartRes;
            }
            catch { return null; }
        }

        private bool IsEnough(Requirement requirement)
        {
            if (requirement == null || requirement.SmartRes == null || _player == null || _isEnough == null) return false;
            try
            {
                _isEnoughArgs[0] = requirement.SmartRes;
                var result = _isEnough.Invoke(_player, _isEnoughArgs);
                return result is bool && (bool)result;
            }
            catch { return false; }
        }

        private bool Persist(out string failure)
        {
            failure = null;
            var directory = Path.GetDirectoryName(_path);
            if (string.IsNullOrEmpty(directory)) { failure = "supplemental cache directory unavailable"; return false; }
            Directory.CreateDirectory(directory);
            var temp = _path + ".tmp";
            try
            {
                using (var stream = new FileStream(temp, FileMode.Create, FileAccess.Write, FileShare.None))
                using (var writer = new BinaryWriter(stream))
                {
                    writer.Write(Magic);
                    writer.Write(SchemaVersion);
                    writer.Write(VerifiedGameVersion);
                    writer.Write(CompletionExcludedCount);
                    writer.Write(NpcIds.Length);
                    for (var n = 0; n < NpcIds.Length; n++)
                    {
                        var npcId = NpcIds[n];
                        writer.Write(npcId);
                        List<Rule> list;
                        if (!_rulesByNpc.TryGetValue(npcId, out list)) list = new List<Rule>();
                        writer.Write(list.Count);
                        for (var i = 0; i < list.Count; i++)
                        {
                            writer.Write(list[i].AnswerId ?? string.Empty);
                            writer.Write(list[i].Variants.Count);
                            for (var v = 0; v < list[i].Variants.Count; v++)
                            {
                                var variant = list[i].Variants[v];
                                writer.Write(variant.Unsupported);
                                WriteRequirement(writer, variant.Price);
                                WriteRequirement(writer, variant.Lock);
                            }
                        }
                    }
                }
                if (File.Exists(_path)) File.Delete(_path);
                File.Move(temp, _path);
                return true;
            }
            catch (Exception ex)
            {
                failure = "supplemental cache persist failed: " + ex.GetType().Name + ": " + ex.Message;
                try { if (File.Exists(temp)) File.Delete(temp); } catch { }
                return false;
            }
        }

        private static void WriteRequirement(BinaryWriter writer, Requirement requirement)
        {
            writer.Write(requirement != null);
            if (requirement == null) return;
            writer.Write(requirement.ResType ?? string.Empty);
            writer.Write(requirement.Id ?? string.Empty);
            writer.Write(requirement.Value);
        }

        private static Requirement ReadRequirement(BinaryReader reader)
        {
            if (!reader.ReadBoolean()) return null;
            return new Requirement { ResType = reader.ReadString(), Id = reader.ReadString(), Value = reader.ReadSingle() };
        }

        private Rule BuildRule(string npcId, string answerId, string multiId, int answerIndex, string serialized,
            Dictionary<string, Node> nodes, List<Connection> connections, object worldObject)
        {
            var rule = new Rule { NpcId = npcId, AnswerId = answerId };
            var answerConnections = FindAnswerConnections(connections, multiId, answerIndex);
            if (answerConnections.Count == 0)
            {
                rule.Variants.Add(new Variant());
                return rule;
            }

            var found = false;
            for (var i = 0; i < answerConnections.Count; i++)
            {
                Node answerNode;
                if (!nodes.TryGetValue(answerConnections[i].SourceNode, out answerNode) ||
                    answerNode.Type.IndexOf("Flow_Answer", StringComparison.Ordinal) < 0) continue;
                found = true;
                var variant = BuildVariant(serialized, answerNode, nodes, connections, worldObject);
                AddVariant(rule, variant);
            }
            if (!found) rule.Variants.Add(new Variant { Unsupported = true });
            return rule;
        }

        private Variant BuildVariant(string serialized, Node answerNode, Dictionary<string, Node> nodes,
            List<Connection> connections, object worldObject)
        {
            var variant = new Variant();
            for (var i = 0; i < connections.Count; i++)
            {
                var c = connections[i];
                if (!string.Equals(c.TargetNode, answerNode.Id, StringComparison.Ordinal)) continue;
                if (!string.Equals(c.TargetPort, "price", StringComparison.OrdinalIgnoreCase) &&
                    !string.Equals(c.TargetPort, "lock", StringComparison.OrdinalIgnoreCase)) continue;
                Node smart;
                if (!nodes.TryGetValue(c.SourceNode, out smart) || smart.Type.IndexOf("Flow_SmartRes", StringComparison.Ordinal) < 0)
                { variant.Unsupported = true; break; }
                var req = ParseRequirement(serialized, smart);
                if (req == null) { variant.Unsupported = true; break; }
                if (string.Equals(c.TargetPort, "price", StringComparison.OrdinalIgnoreCase)) variant.Price = req;
                else variant.Lock = req;
            }
            return variant;
        }

        private static void AddVariant(Rule rule, Variant candidate)
        {
            for (var i = 0; i < rule.Variants.Count; i++)
            {
                var existing = rule.Variants[i];
                if (existing.Unsupported != candidate.Unsupported) continue;
                if (SameRequirement(existing.Price, candidate.Price) && SameRequirement(existing.Lock, candidate.Lock)) return;
            }
            rule.Variants.Add(candidate);
        }

        private static bool SameRequirement(Requirement a, Requirement b)
        {
            if (ReferenceEquals(a, b)) return true;
            if (a == null || b == null) return false;
            return string.Equals(a.ResType, b.ResType, StringComparison.Ordinal) &&
                   string.Equals(a.Id, b.Id, StringComparison.Ordinal) && Math.Abs(a.Value - b.Value) < 0.0001f;
        }

        private static Requirement ParseRequirement(string serialized, Node node)
        {
            var type = ReadNodeContentAny(serialized, node, "res_type", "Res type");
            var id = ReadNodeContentAny(serialized, node, "id", "Id");
            float value;
            if (string.IsNullOrEmpty(type) || string.IsNullOrEmpty(id) || !TryReadNodeNumber(serialized, node, out value)) return null;
            return new Requirement { ResType = type, Id = id, Value = value };
        }

        private bool TryGetSerializedGraph(string npcId, out string serialized, out object worldObject)
        {
            serialized = null;
            worldObject = null;
            try { worldObject = _worldObjectGetter.Invoke(null, new object[] { npcId, true }); }
            catch { return false; }
            var component = worldObject as Component;
            if (component == null) return false;
            var controller = component.GetComponent(_controllerType);
            if (controller == null) return false;
            object graph;
            if (!ReflectionUtil.TryRead(controller, "_graph", out graph) || graph == null) return false;
            object value;
            if (!ReflectionUtil.TryRead(graph, "_serializedGraph", out value)) return false;
            serialized = value as string;
            return !string.IsNullOrEmpty(serialized);
        }

        private HashSet<string> FindProductionCompletionAnswerIds(Dictionary<string, Node> nodes,
            Dictionary<string, List<Connection>> incomingFlow, string serialized)
        {
            var ids = new HashSet<string>(StringComparer.Ordinal);
            foreach (var node in nodes.Values)
            {
                if (!node.Type.EndsWith("Flow_SetTaskState", StringComparison.Ordinal)) continue;
                if (!string.Equals(ReadNodeContent(serialized, node, "State"), "Complete", StringComparison.Ordinal)) continue;
                var anchors = FindCompletionAnchors(nodes, incomingFlow, serialized, node.Id, 64);
                for (var i = 0; i < anchors.Count; i++) AddCompletionAnswerIds(ids, anchors[i], serialized, nodes);
            }
            return ids;
        }

        private static List<CompletionAnchor> FindCompletionAnchors(Dictionary<string, Node> nodes,
            Dictionary<string, List<Connection>> incomingFlow, string serialized, string startNodeId, int maxDepth)
        {
            var result = new List<CompletionAnchor>();
            var queue = new Queue<Tuple<string, int>>();
            var seen = new HashSet<string>(StringComparer.Ordinal) { startNodeId };
            queue.Enqueue(Tuple.Create(startNodeId, 0));
            while (queue.Count > 0)
            {
                var current = queue.Dequeue();
                if (current.Item2 >= maxDepth) continue;
                List<Connection> incoming;
                if (!incomingFlow.TryGetValue(current.Item1, out incoming)) continue;
                for (var i = 0; i < incoming.Count; i++)
                {
                    var c = incoming[i];
                    Node source;
                    if (!nodes.TryGetValue(c.SourceNode, out source)) continue;
                    if (source.Type.EndsWith("CustomFunctionEvent", StringComparison.Ordinal))
                    {
                        AddCompletionAnchor(result, new CompletionAnchor { EventIdentifier = ReadNodeIdentifier(serialized, source) });
                        continue;
                    }
                    if (source.Type.EndsWith("Flow_MultiAnswer", StringComparison.Ordinal))
                    {
                        AddCompletionAnchor(result, new CompletionAnchor { MultiNodeId = source.Id, AnswerIndex = ParseOutPortIndex(c.SourcePort) });
                        continue;
                    }
                    if (seen.Add(source.Id)) queue.Enqueue(Tuple.Create(source.Id, current.Item2 + 1));
                }
            }
            return result;
        }

        private static void AddCompletionAnchor(List<CompletionAnchor> list, CompletionAnchor candidate)
        {
            for (var i = 0; i < list.Count; i++)
            {
                var a = list[i];
                if (a.EventIdentifier == candidate.EventIdentifier && a.MultiNodeId == candidate.MultiNodeId && a.AnswerIndex == candidate.AnswerIndex) return;
            }
            list.Add(candidate);
        }

        private static void AddCompletionAnswerIds(HashSet<string> ids, CompletionAnchor anchor, string serialized,
            Dictionary<string, Node> nodes)
        {
            if (anchor == null) return;
            if (!string.IsNullOrEmpty(anchor.MultiNodeId) && anchor.AnswerIndex >= 0)
            {
                Node multi;
                if (!nodes.TryGetValue(anchor.MultiNodeId, out multi)) return;
                var answers = ReadMultiAnswers(serialized, multi);
                if (anchor.AnswerIndex < answers.Count && !string.IsNullOrEmpty(answers[anchor.AnswerIndex])) ids.Add(answers[anchor.AnswerIndex]);
                return;
            }
            if (string.IsNullOrEmpty(anchor.EventIdentifier)) return;
            var names = CandidateAnswerNames(anchor.EventIdentifier);
            foreach (var multi in nodes.Values)
            {
                if (!multi.Type.EndsWith("Flow_MultiAnswer", StringComparison.Ordinal)) continue;
                var answers = ReadMultiAnswers(serialized, multi);
                for (var i = 0; i < answers.Count; i++) if (names.Contains(answers[i])) ids.Add(answers[i]);
            }
        }

        private static List<ExactAnchor> FindExactAnswerAnchors(Dictionary<string, Node> nodes,
            Dictionary<string, List<Connection>> incomingFlow, Dictionary<string, List<string>> callsByUid,
            string startNodeId, int maxDepth)
        {
            var result = new List<ExactAnchor>();
            var queue = new Queue<Tuple<string, int>>();
            var seen = new HashSet<string>(StringComparer.Ordinal) { startNodeId };
            queue.Enqueue(Tuple.Create(startNodeId, 0));
            while (queue.Count > 0)
            {
                var current = queue.Dequeue();
                if (current.Item2 >= maxDepth) continue;
                List<Connection> incoming;
                if (!incomingFlow.TryGetValue(current.Item1, out incoming)) continue;
                for (var i = 0; i < incoming.Count; i++)
                {
                    var c = incoming[i];
                    Node source;
                    if (!nodes.TryGetValue(c.SourceNode, out source)) continue;
                    if (source.Type.EndsWith("Flow_MultiAnswer", StringComparison.Ordinal))
                    {
                        AddExactAnchor(result, new ExactAnchor { MultiNodeId = source.Id, AnswerIndex = ParseOutPortIndex(c.SourcePort) });
                        continue;
                    }
                    if (source.Type.EndsWith("CustomFunctionEvent", StringComparison.Ordinal) && !string.IsNullOrEmpty(source.Uid))
                    {
                        List<string> callers;
                        if (callsByUid.TryGetValue(source.Uid, out callers))
                        {
                            for (var j = 0; j < callers.Count; j++)
                                if (seen.Add(callers[j])) queue.Enqueue(Tuple.Create(callers[j], current.Item2 + 1));
                        }
                        continue;
                    }
                    if (seen.Add(source.Id)) queue.Enqueue(Tuple.Create(source.Id, current.Item2 + 1));
                }
            }
            return result;
        }

        private static void AddExactAnchor(List<ExactAnchor> list, ExactAnchor candidate)
        {
            if (candidate == null || string.IsNullOrEmpty(candidate.MultiNodeId) || candidate.AnswerIndex < 0) return;
            for (var i = 0; i < list.Count; i++)
                if (list[i].MultiNodeId == candidate.MultiNodeId && list[i].AnswerIndex == candidate.AnswerIndex) return;
            list.Add(candidate);
        }

        private static Dictionary<string, List<Connection>> BuildIncomingFlow(Dictionary<string, Node> nodes,
            List<Connection> connections, bool includeWaitForFlowNumberedInputs)
        {
            var result = new Dictionary<string, List<Connection>>(StringComparer.Ordinal);
            for (var i = 0; i < connections.Count; i++)
            {
                var c = connections[i];
                var isFlow = string.IsNullOrWhiteSpace(c.TargetPort) || string.Equals(c.TargetPort, "In", StringComparison.OrdinalIgnoreCase);
                if (!isFlow && includeWaitForFlowNumberedInputs)
                {
                    Node target;
                    int ignored;
                    isFlow = nodes.TryGetValue(c.TargetNode, out target) && target.Type.EndsWith("Flow_WaitForFlow", StringComparison.Ordinal) &&
                             int.TryParse(c.TargetPort, NumberStyles.Integer, CultureInfo.InvariantCulture, out ignored);
                }
                if (!isFlow) continue;
                List<Connection> list;
                if (!result.TryGetValue(c.TargetNode, out list)) result[c.TargetNode] = list = new List<Connection>();
                list.Add(c);
            }
            return result;
        }

        private static Dictionary<string, List<string>> BuildCallsByUid(Dictionary<string, Node> nodes)
        {
            var result = new Dictionary<string, List<string>>(StringComparer.Ordinal);
            foreach (var node in nodes.Values)
            {
                if (!node.Type.EndsWith("CustomFunctionCall", StringComparison.Ordinal) || string.IsNullOrEmpty(node.SourceOutputUid)) continue;
                List<string> list;
                if (!result.TryGetValue(node.SourceOutputUid, out list)) result[node.SourceOutputUid] = list = new List<string>();
                list.Add(node.Id);
            }
            return result;
        }

        private static Dictionary<string, Node> BuildNodeIndex(string serialized)
        {
            var result = new Dictionary<string, Node>(StringComparer.Ordinal);
            const string typeMarker = "\"$type\":\"";
            const string idMarker = "\"$id\":\"";
            var start = 0;
            while (start < serialized.Length)
            {
                var typePos = serialized.IndexOf(typeMarker, start, StringComparison.Ordinal);
                if (typePos < 0) break;
                int typeEnd;
                var type = ReadJsonString(serialized, typePos + typeMarker.Length, out typeEnd);
                if (type == null) break;
                var nextType = serialized.IndexOf(typeMarker, typeEnd, StringComparison.Ordinal);
                var idPos = serialized.IndexOf(idMarker, typeEnd, StringComparison.Ordinal);
                if (idPos >= 0 && (nextType < 0 || idPos < nextType) && idPos - typeEnd < 240)
                {
                    int idEnd;
                    var id = ReadJsonString(serialized, idPos + idMarker.Length, out idEnd);
                    if (!string.IsNullOrEmpty(id))
                    {
                        var node = new Node { Id = id, Type = type, TypePosition = typePos };
                        node.Uid = ReadRawStringProperty(serialized, node, "_UID");
                        node.SourceOutputUid = ReadRawStringProperty(serialized, node, "_sourceOutputUID");
                        result[id] = node;
                    }
                }
                start = typeEnd + 1;
            }
            return result;
        }

        private static List<Connection> ParseConnections(string serialized)
        {
            var result = new List<Connection>();
            const string spMarker = "\"_sourcePortName\":\"";
            const string tpMarker = "\"_targetPortName\":\"";
            const string srcMarker = "\"_sourceNode\":{\"$ref\":\"";
            const string dstMarker = "\"_targetNode\":{\"$ref\":\"";
            var start = 0;
            while (start < serialized.Length)
            {
                var spPos = serialized.IndexOf(spMarker, start, StringComparison.Ordinal);
                if (spPos < 0) break;
                int spEnd;
                var sp = ReadJsonString(serialized, spPos + spMarker.Length, out spEnd);
                var tpPos = serialized.IndexOf(tpMarker, spEnd, StringComparison.Ordinal);
                if (tpPos < 0 || tpPos - spEnd > 300) { start = spEnd + 1; continue; }
                int tpEnd;
                var tp = ReadJsonString(serialized, tpPos + tpMarker.Length, out tpEnd);
                var srcPos = serialized.IndexOf(srcMarker, tpEnd, StringComparison.Ordinal);
                if (srcPos < 0 || srcPos - tpEnd > 300) { start = tpEnd + 1; continue; }
                int srcEnd;
                var src = ReadJsonString(serialized, srcPos + srcMarker.Length, out srcEnd);
                var dstPos = serialized.IndexOf(dstMarker, srcEnd, StringComparison.Ordinal);
                if (dstPos < 0 || dstPos - srcEnd > 300) { start = srcEnd + 1; continue; }
                int dstEnd;
                var dst = ReadJsonString(serialized, dstPos + dstMarker.Length, out dstEnd);
                if (!string.IsNullOrEmpty(src) && !string.IsNullOrEmpty(dst))
                    result.Add(new Connection { SourcePort = sp, TargetPort = tp, SourceNode = src, TargetNode = dst });
                start = dstEnd + 1;
            }
            return result;
        }

        private static List<Connection> FindAnswerConnections(List<Connection> connections, string multiId, int index)
        {
            var result = new List<Connection>();
            for (var i = 0; i < connections.Count; i++)
                if (connections[i].TargetNode == multiId && ParseAnswerPortIndex(connections[i].TargetPort) == index) result.Add(connections[i]);
            return result;
        }

        private static HashSet<string> FindBlacklistRemovals(Dictionary<string, Node> nodes, string serialized)
        {
            var result = new HashSet<string>(StringComparer.Ordinal);
            foreach (var node in nodes.Values)
            {
                if (!node.Type.EndsWith("Flow_AddPhraseToBlacklist", StringComparison.Ordinal)) continue;
                bool remove;
                if (!TryReadNodeBool(serialized, node, "remove", out remove) || !remove) continue;
                var id = ReadNodeContent(serialized, node, "Phrase ID") ?? ReadNodeContent(serialized, node, "in_phrase");
                if (!string.IsNullOrEmpty(id)) result.Add(id);
            }
            return result;
        }

        private static bool TryReadBlacklistAdd(Node node, string serialized, out string phraseId)
        {
            phraseId = null;
            if (node == null) return false;
            if (node.Type.EndsWith("Flow_BlackListPhrase", StringComparison.Ordinal))
            {
                phraseId = ReadNodeContent(serialized, node, "phrase") ?? ReadNodeContent(serialized, node, "Phrase");
                return !string.IsNullOrEmpty(phraseId);
            }
            if (!node.Type.EndsWith("Flow_AddPhraseToBlacklist", StringComparison.Ordinal)) return false;
            bool remove;
            if (TryReadNodeBool(serialized, node, "remove", out remove) && remove) return false;
            phraseId = ReadNodeContent(serialized, node, "Phrase ID") ?? ReadNodeContent(serialized, node, "in_phrase");
            return !string.IsNullOrEmpty(phraseId);
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

        private MethodInfo FindSmartResFactory()
        {
            if (_flowSmartResType == null) return null;
            foreach (var method in _flowSmartResType.GetMethods(ReflectionUtil.AnyInstance | ReflectionUtil.AnyStatic | BindingFlags.DeclaredOnly))
                if (method.Name == "Invoke" && method.GetParameters().Length == 3) return method;
            return null;
        }

        private static string ReadNodeIdentifier(string serialized, Node node)
        {
            var begin = Math.Max(0, node.TypePosition - 2600);
            var window = serialized.Substring(begin, node.TypePosition - begin);
            const string marker = "\"identifier\":\"";
            var pos = window.LastIndexOf(marker, StringComparison.Ordinal);
            if (pos < 0) return null;
            int end;
            return ReadJsonString(window, pos + marker.Length, out end);
        }

        private static string ReadNodeContent(string serialized, Node node, string key)
        {
            if (node == null) return null;
            var begin = Math.Max(0, node.TypePosition - 2600);
            var window = serialized.Substring(begin, node.TypePosition - begin);
            var marker = "\"" + key + "\":{\"$content\":\"";
            var pos = window.LastIndexOf(marker, StringComparison.Ordinal);
            if (pos < 0) return null;
            int end;
            return ReadJsonString(window, pos + marker.Length, out end);
        }

        private static string ReadNodeContentAny(string serialized, Node node, string first, string second)
        {
            return ReadNodeContent(serialized, node, first) ?? ReadNodeContent(serialized, node, second);
        }

        private static string ReadRawStringProperty(string serialized, Node node, string key)
        {
            if (node == null) return null;
            var begin = Math.Max(0, node.TypePosition - 2600);
            var window = serialized.Substring(begin, node.TypePosition - begin);
            var marker = "\"" + key + "\":\"";
            var pos = window.LastIndexOf(marker, StringComparison.Ordinal);
            if (pos < 0) return null;
            int end;
            return ReadJsonString(window, pos + marker.Length, out end);
        }

        private static bool TryReadNodeBool(string serialized, Node node, string key, out bool value)
        {
            value = false;
            if (node == null) return false;
            var begin = Math.Max(0, node.TypePosition - 2600);
            var window = serialized.Substring(begin, node.TypePosition - begin);
            var marker = "\"" + key + "\":{\"$content\":";
            var pos = window.LastIndexOf(marker, StringComparison.Ordinal);
            if (pos < 0) return false;
            pos += marker.Length;
            while (pos < window.Length && char.IsWhiteSpace(window[pos])) pos++;
            if (window.IndexOf("true", pos, StringComparison.Ordinal) == pos) { value = true; return true; }
            if (window.IndexOf("false", pos, StringComparison.Ordinal) == pos) { value = false; return true; }
            return false;
        }

        private static bool TryReadNodeNumber(string serialized, Node node, out float value)
        {
            return TryReadNodeNumber(serialized, node, "v", out value) || TryReadNodeNumber(serialized, node, "V", out value);
        }

        private static bool TryReadNodeNumber(string serialized, Node node, string key, out float value)
        {
            value = 0f;
            var begin = Math.Max(0, node.TypePosition - 2600);
            var window = serialized.Substring(begin, node.TypePosition - begin);
            var marker = "\"" + key + "\":{\"$content\":";
            var pos = window.LastIndexOf(marker, StringComparison.Ordinal);
            if (pos < 0) return false;
            pos += marker.Length;
            while (pos < window.Length && char.IsWhiteSpace(window[pos])) pos++;
            var end = pos;
            while (end < window.Length && (char.IsDigit(window[end]) || window[end] == '-' || window[end] == '+' || window[end] == '.' || window[end] == 'e' || window[end] == 'E')) end++;
            return end > pos && float.TryParse(window.Substring(pos, end - pos), NumberStyles.Float, CultureInfo.InvariantCulture, out value);
        }

        private static List<string> ReadMultiAnswers(string serialized, Node node)
        {
            var result = new List<string>();
            var begin = Math.Max(0, node.TypePosition - 18000);
            var window = serialized.Substring(begin, node.TypePosition - begin);
            const string marker = "\"answers\":[";
            var pos = window.LastIndexOf(marker, StringComparison.Ordinal);
            if (pos < 0) return result;
            var i = pos + marker.Length;
            while (i < window.Length)
            {
                while (i < window.Length && (char.IsWhiteSpace(window[i]) || window[i] == ',')) i++;
                if (i >= window.Length || window[i] == ']') break;
                if (window[i] != '"') break;
                int end;
                var value = ReadJsonString(window, i + 1, out end);
                if (value == null) break;
                result.Add(value);
                i = end + 1;
            }
            return result;
        }

        private static int ParseAnswerPortIndex(string port)
        {
            if (string.IsNullOrEmpty(port)) return -1;
            var hash = port.LastIndexOf('#');
            if (hash < 0 || hash + 1 >= port.Length) return -1;
            var i = hash + 1;
            var value = 0;
            var digits = 0;
            while (i < port.Length && char.IsDigit(port[i]))
            {
                value = value * 10 + (port[i] - '0');
                digits++;
                i++;
            }
            return digits == 0 ? -1 : value;
        }

        private static int ParseOutPortIndex(string port)
        {
            const string prefix = "out_";
            int value;
            return port != null && port.StartsWith(prefix, StringComparison.Ordinal) && int.TryParse(port.Substring(prefix.Length), out value) ? value : -1;
        }

        private static HashSet<string> CandidateAnswerNames(string identifier)
        {
            var result = new HashSet<string>(StringComparer.Ordinal);
            if (string.IsNullOrEmpty(identifier)) return result;
            result.Add(identifier);
            if (identifier.StartsWith("@", StringComparison.Ordinal)) result.Add(identifier.Substring(1));
            else result.Add("@" + identifier);
            return result;
        }

        private static bool IsUtilityLike(string id)
        {
            return string.Equals(id, "Leave", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(id, "Back", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(id, "Trade", StringComparison.OrdinalIgnoreCase);
        }

        private static bool ContainsString(object collection, string value)
        {
            var enumerable = collection as IEnumerable;
            if (enumerable == null) return false;
            foreach (var item in enumerable)
                if (string.Equals(item as string, value, StringComparison.Ordinal)) return true;
            return false;
        }

        private static string ReadJsonString(string text, int start, out int end)
        {
            end = start;
            var escaped = false;
            for (var i = start; i < text.Length; i++)
            {
                var ch = text[i];
                if (escaped) { escaped = false; continue; }
                if (ch == '\\') { escaped = true; continue; }
                if (ch != '"') continue;
                end = i;
                var raw = text.Substring(start, i - start);
                try { return Regex.Unescape(raw.Replace("\\/", "/")); }
                catch { return raw; }
            }
            return null;
        }
    }
}

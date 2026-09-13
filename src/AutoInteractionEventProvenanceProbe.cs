using System;
using System.Collections.Generic;
using System.Text;
using BepInEx;
using UnityEngine;

namespace CalendarQuestsPins
{
    [BepInPlugin(PluginGuid, PluginName, PluginVersion)]
    public sealed class AutoInteractionEventProvenanceProbe : BaseUnityPlugin
    {
        public const string PluginGuid = "nikich.gyk.calendarquestspins.autointeractionaudit.v3";
        public const string PluginName = "Day Wheel Quest Markers - event provenance audit";
        public const string PluginVersion = "0.1.2";

        private static readonly string[] TargetEvents =
        {
            "morgue_quest",
            "on_back_to_snake_after_ritual",
            "snake_stone_ready",
            "witch_burning_enable",
            "inquisitor_after_dark_event"
        };

        private Type _mainGameType;
        private Type _controllerType;
        private bool _finished;
        private float _nextAttempt;
        private int _attempts;

        private void Awake()
        {
            _mainGameType = ReflectionUtil.FindType("MainGame");
            _controllerType = ReflectionUtil.FindType("FlowCanvas.FlowScriptController");
            _nextAttempt = Time.realtimeSinceStartup + 1f;
            Logger.LogInfo(PluginName + " " + PluginVersion + " loaded. Read-only one-shot scan for exact CustomEvent provenance literals.");
        }

        private void Update()
        {
            if (_finished || Time.realtimeSinceStartup < _nextAttempt) return;
            _nextAttempt = Time.realtimeSinceStartup + 1f;
            _attempts++;

            if (!RuntimeReady())
            {
                if (_attempts >= 120)
                {
                    Logger.LogError("AUTO3_ABORT runtime did not become ready after 120 attempts.");
                    _finished = true;
                    enabled = false;
                }
                return;
            }

            try { RunAudit(); }
            catch (Exception ex) { Logger.LogError("AUTO3_FATAL " + ex); }
            finally
            {
                _finished = true;
                enabled = false;
            }
        }

        private bool RuntimeReady()
        {
            if (_mainGameType == null || _controllerType == null) return false;
            object started;
            if (!TryReadStatic(_mainGameType, "game_started", out started) || !(started is bool) || !(bool)started) return false;
            try { return Resources.FindObjectsOfTypeAll(_controllerType).Length > 0; }
            catch { return false; }
        }

        private void RunAudit()
        {
            UnityEngine.Object[] controllers;
            try { controllers = Resources.FindObjectsOfTypeAll(_controllerType); }
            catch (Exception ex)
            {
                Logger.LogError("AUTO3_ABORT controller scan failed: " + ex.GetType().Name + ": " + ex.Message);
                return;
            }

            TextAsset[] textAssets;
            try { textAssets = Resources.FindObjectsOfTypeAll<TextAsset>(); }
            catch { textAssets = new TextAsset[0]; }

            Logger.LogInfo("AUTO3_BEGIN game=1.407 targets=" + TargetEvents.Length + " controllers=" + controllers.Length + " textAssets=" + textAssets.Length);

            var scannedGraphs = 0;
            var graphSeen = new HashSet<string>(StringComparer.Ordinal);
            for (var i = 0; i < TargetEvents.Length; i++)
            {
                var literal = TargetEvents[i];
                var graphMatches = 0;
                var graphControllers = new HashSet<string>(StringComparer.Ordinal);
                var textMatches = 0;

                for (var c = 0; c < controllers.Length; c++)
                {
                    var controller = controllers[c];
                    if (controller == null) continue;
                    object graph;
                    object serializedValue;
                    if (!ReflectionUtil.TryRead(controller, "_graph", out graph) || graph == null) continue;
                    if (!ReflectionUtil.TryRead(graph, "_serializedGraph", out serializedValue)) continue;
                    var serialized = serializedValue as string;
                    if (string.IsNullOrEmpty(serialized)) continue;

                    var controllerPath = GetObjectPath(controller);
                    var graphName = Safe(graph.ToString());
                    var graphKey = controllerPath + "|" + graphName + "|" + serialized.Length;
                    if (i == 0 && graphSeen.Add(graphKey)) scannedGraphs++;

                    var start = 0;
                    while (start < serialized.Length)
                    {
                        var pos = serialized.IndexOf(literal, start, StringComparison.Ordinal);
                        if (pos < 0) break;
                        graphMatches++;
                        graphControllers.Add(controllerPath);

                        string nodeType;
                        string nodeId;
                        FindAssociatedSerializedNode(serialized, pos, out nodeType, out nodeId);
                        Logger.LogInfo(
                            "AUTO3_GRAPH_MATCH event=" + literal +
                            " controller=" + Safe(controllerPath) +
                            " graph=" + graphName +
                            " nodeType=" + Safe(nodeType) +
                            " nodeId=" + Safe(nodeId) +
                            " context=" + Safe(CompactContext(serialized, pos, 260)));

                        start = pos + literal.Length;
                        if (graphMatches >= 80)
                        {
                            Logger.LogWarning("AUTO3_GRAPH_CAP event=" + literal + " cap=80");
                            break;
                        }
                    }
                    if (graphMatches >= 80) break;
                }

                for (var t = 0; t < textAssets.Length && textMatches < 40; t++)
                {
                    var asset = textAssets[t];
                    if (asset == null) continue;
                    string content;
                    try { content = asset.text; }
                    catch { continue; }
                    if (string.IsNullOrEmpty(content)) continue;
                    var pos = content.IndexOf(literal, StringComparison.Ordinal);
                    if (pos < 0) continue;
                    textMatches++;
                    Logger.LogInfo(
                        "AUTO3_TEXT_MATCH event=" + literal +
                        " asset=" + Safe(asset.name) +
                        " context=" + Safe(CompactContext(content, pos, 220)));
                }

                Logger.LogInfo(
                    "AUTO3_EVENT_SUMMARY event=" + literal +
                    " graphMatches=" + graphMatches +
                    " graphControllers=" + graphControllers.Count +
                    " textMatches=" + textMatches);
            }

            Logger.LogInfo("AUTO3_SUMMARY scannedUniqueGraphs=" + scannedGraphs + " loadedControllers=" + controllers.Length);
            Logger.LogInfo("AUTO3_END probe disabled after this snapshot");
        }

        private static void FindAssociatedSerializedNode(string serialized, int occurrence, out string nodeType, out string nodeId)
        {
            nodeType = null;
            nodeId = null;
            if (string.IsNullOrEmpty(serialized) || occurrence < 0) return;

            const string typeMarker = "\"$type\":\"";
            const string idMarker = "\"$id\":\"";
            var typePos = serialized.IndexOf(typeMarker, occurrence, StringComparison.Ordinal);
            if (typePos < 0 || typePos - occurrence > 1800) return;

            int typeEnd;
            nodeType = ReadJsonString(serialized, typePos + typeMarker.Length, out typeEnd);
            if (nodeType == null) return;

            var nextType = serialized.IndexOf(typeMarker, typeEnd, StringComparison.Ordinal);
            var idPos = serialized.IndexOf(idMarker, typeEnd, StringComparison.Ordinal);
            if (idPos < 0 || (nextType >= 0 && idPos > nextType) || idPos - typeEnd > 320) return;

            int idEnd;
            nodeId = ReadJsonString(serialized, idPos + idMarker.Length, out idEnd);
        }

        private static string ReadJsonString(string text, int start, out int end)
        {
            end = start;
            if (start < 0 || start >= text.Length) return null;
            var sb = new StringBuilder();
            var escaped = false;
            for (var i = start; i < text.Length; i++)
            {
                var ch = text[i];
                if (escaped)
                {
                    if (ch == 'n') sb.Append('\n');
                    else if (ch == 'r') sb.Append('\r');
                    else if (ch == 't') sb.Append('\t');
                    else sb.Append(ch);
                    escaped = false;
                    continue;
                }
                if (ch == '\\')
                {
                    escaped = true;
                    continue;
                }
                if (ch == '"')
                {
                    end = i;
                    return sb.ToString();
                }
                sb.Append(ch);
            }
            return null;
        }

        private static string CompactContext(string text, int center, int radius)
        {
            if (string.IsNullOrEmpty(text)) return "<none>";
            var start = Math.Max(0, center - radius);
            var end = Math.Min(text.Length, center + radius);
            var raw = text.Substring(start, end - start);
            return raw.Replace('\r', ' ').Replace('\n', ' ').Replace('\t', ' ');
        }

        private static string GetObjectPath(object obj)
        {
            var component = obj as Component;
            if (component == null) return obj == null ? "<null>" : obj.ToString();
            try
            {
                var names = new List<string>();
                var tr = component.transform;
                var guard = 0;
                while (tr != null && guard++ < 12)
                {
                    names.Add(tr.name);
                    tr = tr.parent;
                }
                names.Reverse();
                return string.Join("/", names.ToArray());
            }
            catch { return component.name; }
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

        private static string Safe(string value)
        {
            if (string.IsNullOrEmpty(value)) return "<none>";
            return value.Replace('\r', ' ').Replace('\n', ' ').Replace('\t', ' ').Replace(" ", "_");
        }
    }
}

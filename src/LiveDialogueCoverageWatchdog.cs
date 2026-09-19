using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using HarmonyLib;

namespace DayWheelQuestMarkersResearch
{
    /// <summary>
    /// Event-driven live-dialogue accounting for the accepted GK 1.407 six-weekday-NPC universe.
    /// Hooks only the native Flow_MultiAnswer execution and the native UI option Show seam.
    /// No graph scans or polling occur here.
    /// </summary>
    internal sealed class LiveDialogueCoverageWatchdog : IDisposable
    {
        private const string ResourceName = "DayWheelQuestMarkersResearch.LiveAnswerDispositions.tsv";
        private const int ExpectedOccurrences = 243;
        private const int ExpectedUniqueNpcAnswerIds = 224;

        private sealed class DispositionRecord
        {
            internal string Npc;
            internal int Multi;
            internal int Index;
            internal string Answer;
            internal string Disposition;
            internal string Owner;
            internal string[] Tasks;
            internal string Evidence;
        }

        private sealed class VisibleAnswer
        {
            internal string Id;
            internal bool CanBePicked;
        }

        private sealed class DialogueContext
        {
            internal string Npc;
            internal int Multi;
            internal readonly List<string> AuthoredAnswers = new List<string>();
            internal readonly List<VisibleAnswer> VisibleAnswers = new List<VisibleAnswer>();
        }

        private static LiveDialogueCoverageWatchdog _active;
        private static DialogueContext _current;

        private readonly RuntimeWatchdogPlugin _owner;
        private readonly Dictionary<string, DispositionRecord> _byOccurrence =
            new Dictionary<string, DispositionRecord>(StringComparer.Ordinal);
        private readonly Dictionary<string, List<DispositionRecord>> _byMenu =
            new Dictionary<string, List<DispositionRecord>>(StringComparer.Ordinal);

        private Harmony _harmony;
        private string _lastAccountingSignature;

        internal LiveDialogueCoverageWatchdog(RuntimeWatchdogPlugin owner)
        {
            _owner = owner;
        }

        internal bool Initialize(out string failure)
        {
            failure = null;
            if (!LoadFixture(out failure)) return false;

            var closureType = FindType("FlowCanvas.Nodes.Flow_MultiAnswer+<>c__DisplayClass1_0");
            var optionType = FindType("MultiAnswerOptionGUI");
            if (closureType == null || optionType == null)
            {
                failure = "Required GK 1.407 dialogue types were not found.";
                return false;
            }

            var multiCallback = closureType.GetMethod(
                "<RegisterPorts>b__0",
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            var optionShow = FindOptionShow(optionType);
            if (multiCallback == null || optionShow == null)
            {
                failure = "Required GK 1.407 live-dialogue methods were not found.";
                return false;
            }

            try
            {
                _active = this;
                _harmony = new Harmony("nikich.gyk.daywheelquestmarkers.runtimewatchdog.live");
                _harmony.Patch(
                    multiCallback,
                    prefix: new HarmonyMethod(typeof(LiveDialogueCoverageWatchdog), nameof(MultiPrefix)),
                    postfix: new HarmonyMethod(typeof(LiveDialogueCoverageWatchdog), nameof(MultiPostfix)),
                    finalizer: new HarmonyMethod(typeof(LiveDialogueCoverageWatchdog), nameof(MultiFinalizer)));
                _harmony.Patch(
                    optionShow,
                    prefix: new HarmonyMethod(typeof(LiveDialogueCoverageWatchdog), nameof(OptionShowPrefix)));

                _owner.LogLiveInfo(
                    "LIVE_WATCHDOG_READY occurrences=" + _byOccurrence.Count +
                    " uniqueNpcAnswers=" + CountUniqueNpcAnswers() +
                    " hook=Flow_MultiAnswer->MultiAnswerOptionGUI.Show");
                return true;
            }
            catch (Exception ex)
            {
                failure = "Harmony patch installation failed: " + ex.GetType().Name + ": " + ex.Message;
                Dispose();
                return false;
            }
        }

        public void Dispose()
        {
            _current = null;
            if (ReferenceEquals(_active, this)) _active = null;
            if (_harmony != null)
            {
                try { _harmony.UnpatchSelf(); } catch { }
                _harmony = null;
            }
        }

        private bool LoadFixture(out string failure)
        {
            failure = null;
            try
            {
                var assembly = Assembly.GetExecutingAssembly();
                using (var stream = assembly.GetManifestResourceStream(ResourceName))
                {
                    if (stream == null)
                    {
                        failure = "Embedded live-answer disposition fixture is missing.";
                        return false;
                    }

                    using (var reader = new StreamReader(stream))
                    {
                        string header = null;
                        string line;
                        while ((line = reader.ReadLine()) != null)
                        {
                            if (line.Length == 0 || line.StartsWith("#", StringComparison.Ordinal)) continue;
                            if (header == null)
                            {
                                header = line;
                                if (!string.Equals(
                                        header,
                                        "npc\tmulti\tindex\tanswer\tdisposition\towner\ttasks\tevidence",
                                        StringComparison.Ordinal))
                                {
                                    failure = "Unexpected live disposition fixture header.";
                                    return false;
                                }
                                continue;
                            }

                            var parts = line.Split(new[] { '\t' });
                            if (parts.Length != 8)
                            {
                                failure = "Malformed live disposition fixture row: " + line;
                                return false;
                            }

                            int multi;
                            int index;
                            if (!int.TryParse(parts[1], out multi) || !int.TryParse(parts[2], out index))
                            {
                                failure = "Invalid multi/index in live disposition fixture row: " + line;
                                return false;
                            }

                            var record = new DispositionRecord
                            {
                                Npc = parts[0],
                                Multi = multi,
                                Index = index,
                                Answer = parts[3],
                                Disposition = parts[4],
                                Owner = parts[5],
                                Tasks = string.IsNullOrEmpty(parts[6])
                                    ? new string[0]
                                    : parts[6].Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries),
                                Evidence = parts[7]
                            };

                            if (record.Disposition.IndexOf("UNKNOWN", StringComparison.Ordinal) >= 0)
                            {
                                failure = "Fixture contains UNKNOWN disposition for " +
                                          record.Npc + "/" + record.Multi + "/" + record.Answer + ".";
                                return false;
                            }

                            var occurrenceKey = OccurrenceKey(record.Npc, record.Multi, record.Index, record.Answer);
                            if (_byOccurrence.ContainsKey(occurrenceKey))
                            {
                                failure = "Duplicate live disposition occurrence: " + occurrenceKey;
                                return false;
                            }
                            _byOccurrence.Add(occurrenceKey, record);

                            var menuKey = MenuKey(record.Npc, record.Multi);
                            List<DispositionRecord> menu;
                            if (!_byMenu.TryGetValue(menuKey, out menu))
                            {
                                menu = new List<DispositionRecord>();
                                _byMenu.Add(menuKey, menu);
                            }
                            menu.Add(record);
                        }
                    }
                }

                if (_byOccurrence.Count != ExpectedOccurrences)
                {
                    failure = "Live disposition fixture occurrence count=" + _byOccurrence.Count +
                              " expected=" + ExpectedOccurrences + ".";
                    return false;
                }

                if (CountUniqueNpcAnswers() != ExpectedUniqueNpcAnswerIds)
                {
                    failure = "Live disposition fixture unique NPC+answer count=" + CountUniqueNpcAnswers() +
                              " expected=" + ExpectedUniqueNpcAnswerIds + ".";
                    return false;
                }

                foreach (var pair in _byMenu)
                    pair.Value.Sort((a, b) => a.Index.CompareTo(b.Index));

                return true;
            }
            catch (Exception ex)
            {
                failure = "Live disposition fixture load failed: " + ex.GetType().Name + ": " + ex.Message;
                return false;
            }
        }

        private int CountUniqueNpcAnswers()
        {
            var unique = new HashSet<string>(StringComparer.Ordinal);
            foreach (var record in _byOccurrence.Values)
                unique.Add(record.Npc + "\t" + record.Answer);
            return unique.Count;
        }

        private static void MultiPrefix(object __instance)
        {
            if (_active == null) return;
            _active.BeginMulti(__instance);
        }

        private static void MultiPostfix()
        {
            if (_active == null) return;
            _active.EndMulti();
        }

        private static Exception MultiFinalizer(Exception __exception)
        {
            if (_active != null) _active.AbortMulti(__exception);
            return __exception;
        }

        private static void OptionShowPrefix(object[] __args)
        {
            if (_active == null || _current == null || __args == null || __args.Length == 0) return;
            _active.RecordVisibleOption(__args[0]);
        }

        private void BeginMulti(object closure)
        {
            _current = null;
            var multi = ReadMember(closure, "<>4__this");
            if (multi == null) return;

            var wgo = ReadMember(multi, "wgo") ?? InvokeNoArg(multi, "get_wgo");
            var npc = ReadString(wgo, "_obj_id");
            if (!IsWeekdayNpc(npc)) return;

            var multiId = ReadInt(multi, "_ID") ?? ReadInt(multi, "ID");
            if (!multiId.HasValue)
            {
                _owner.ReportLiveFailure("LIVE_DIALOGUE_CONTEXT",
                    "Weekday NPC " + npc + " Flow_MultiAnswer node ID could not be read.");
                return;
            }

            var answers = ReadMember(multi, "answers") as IEnumerable;
            if (answers == null)
            {
                _owner.ReportLiveFailure("LIVE_DIALOGUE_CONTEXT",
                    "Weekday NPC " + npc + " multi=" + multiId.Value + " answers list unavailable.");
                return;
            }

            var context = new DialogueContext { Npc = npc, Multi = multiId.Value };
            foreach (var item in answers)
                context.AuthoredAnswers.Add(item == null ? string.Empty : item.ToString());

            _current = context;
            ValidateAuthoredMenu(context);
        }

        private void ValidateAuthoredMenu(DialogueContext context)
        {
            List<DispositionRecord> expected;
            if (!_byMenu.TryGetValue(MenuKey(context.Npc, context.Multi), out expected))
            {
                _owner.ReportLiveFailure("LIVE_AUTHORED_MENU_UNKNOWN",
                    "npc=" + context.Npc + " multi=" + context.Multi +
                    " executed but has no accepted authored-menu record.");
                return;
            }

            if (expected.Count != context.AuthoredAnswers.Count)
            {
                _owner.ReportLiveFailure("LIVE_AUTHORED_MENU_DRIFT",
                    "npc=" + context.Npc + " multi=" + context.Multi +
                    " authoredCount=" + context.AuthoredAnswers.Count +
                    " expected=" + expected.Count + ".");
                return;
            }

            for (var i = 0; i < expected.Count; i++)
            {
                if (expected[i].Index != i ||
                    !string.Equals(expected[i].Answer, context.AuthoredAnswers[i], StringComparison.Ordinal))
                {
                    _owner.ReportLiveFailure("LIVE_AUTHORED_MENU_DRIFT",
                        "npc=" + context.Npc + " multi=" + context.Multi +
                        " index=" + i + " answer=" + Safe(context.AuthoredAnswers[i]) +
                        " expected=" + Safe(expected[i].Answer) + ".");
                    return;
                }
            }
        }

        private void RecordVisibleOption(object answer)
        {
            if (_current == null || answer == null) return;
            var id = ReadString(answer, "id");
            var canBePicked = ReadBool(answer, "can_be_picked");
            _current.VisibleAnswers.Add(new VisibleAnswer
            {
                Id = id,
                CanBePicked = canBePicked.HasValue && canBePicked.Value
            });
        }

        private void EndMulti()
        {
            var context = _current;
            _current = null;
            if (context == null) return;

            var details = new List<string>();
            var unexplained = 0;
            var pickable = 0;
            var reminderShaped = 0;

            for (var v = 0; v < context.VisibleAnswers.Count; v++)
            {
                var visible = context.VisibleAnswers[v];
                if (visible.CanBePicked) pickable++;

                var index = FindAuthoredIndex(context.AuthoredAnswers, visible.Id);
                if (index < 0)
                {
                    unexplained++;
                    _owner.ReportLiveFailure("LIVE_DIALOGUE_UNKNOWN",
                        "npc=" + context.Npc + " multi=" + context.Multi +
                        " displayed answer=" + Safe(visible.Id) +
                        " is not present in the executing authored answer list.");
                    continue;
                }

                DispositionRecord record;
                if (!_byOccurrence.TryGetValue(
                        OccurrenceKey(context.Npc, context.Multi, index, visible.Id), out record))
                {
                    unexplained++;
                    _owner.ReportLiveFailure("LIVE_DIALOGUE_UNKNOWN",
                        "npc=" + context.Npc + " multi=" + context.Multi +
                        " index=" + index + " displayed answer=" + Safe(visible.Id) +
                        " has no accepted disposition.");
                    continue;
                }

                if (visible.CanBePicked &&
                    (string.Equals(record.Disposition, "REMINDER_DIALOGUE_OWNER", StringComparison.Ordinal) ||
                     string.Equals(record.Disposition, "REMINDER_TASK_OWNED", StringComparison.Ordinal)))
                    reminderShaped++;

                details.Add(
                    Safe(visible.Id) + ":" + record.Disposition +
                    ":" + (visible.CanBePicked ? "pickable" : "blocked"));
            }

            var signature = context.Npc + "|" + context.Multi + "|" + string.Join(",", details.ToArray());
            if (!string.Equals(signature, _lastAccountingSignature, StringComparison.Ordinal))
            {
                _lastAccountingSignature = signature;
                _owner.LogLiveInfo(
                    "LIVE_DIALOGUE_ACCOUNTING npc=" + context.Npc +
                    " multi=" + context.Multi +
                    " authored=" + context.AuthoredAnswers.Count +
                    " visible=" + context.VisibleAnswers.Count +
                    " pickable=" + pickable +
                    " reminderShaped=" + reminderShaped +
                    " unexplained=" + unexplained +
                    " options=[" + string.Join(";", details.ToArray()) + "]");
            }
        }

        private void AbortMulti(Exception exception)
        {
            if (_current == null) return;
            var context = _current;
            _current = null;
            if (exception != null)
            {
                _owner.ReportLiveFailure("LIVE_DIALOGUE_HOOK_EXCEPTION",
                    "npc=" + context.Npc + " multi=" + context.Multi +
                    " exception=" + exception.GetType().Name + ".");
            }
        }

        private static MethodInfo FindOptionShow(Type optionType)
        {
            foreach (var method in optionType.GetMethods(
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic))
            {
                if (!string.Equals(method.Name, "Show", StringComparison.Ordinal)) continue;
                var parameters = method.GetParameters();
                if (parameters.Length != 7) continue;
                if (!string.Equals(parameters[0].ParameterType.Name, "AnswerVisualData", StringComparison.Ordinal)) continue;
                return method;
            }
            return null;
        }

        private static int FindAuthoredIndex(List<string> answers, string id)
        {
            var found = -1;
            for (var i = 0; i < answers.Count; i++)
            {
                if (!string.Equals(answers[i], id, StringComparison.Ordinal)) continue;
                if (found >= 0) return -1;
                found = i;
            }
            return found;
        }

        private static string OccurrenceKey(string npc, int multi, int index, string answer)
        {
            return npc + "\t" + multi + "\t" + index + "\t" + (answer ?? string.Empty);
        }

        private static string MenuKey(string npc, int multi)
        {
            return npc + "\t" + multi;
        }

        private static bool IsWeekdayNpc(string id)
        {
            return string.Equals(id, "npc_astrologer", StringComparison.Ordinal) ||
                   string.Equals(id, "npc_inquisitor", StringComparison.Ordinal) ||
                   string.Equals(id, "npc_cultist", StringComparison.Ordinal) ||
                   string.Equals(id, "npc_merchant", StringComparison.Ordinal) ||
                   string.Equals(id, "npc_actress", StringComparison.Ordinal) ||
                   string.Equals(id, "npc_bishop", StringComparison.Ordinal);
        }

        private static object InvokeNoArg(object target, string name)
        {
            if (target == null) return null;
            for (var type = target.GetType(); type != null; type = type.BaseType)
            {
                var method = type.GetMethod(name,
                    BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic |
                    BindingFlags.DeclaredOnly,
                    null, Type.EmptyTypes, null);
                if (method == null) continue;
                try { return method.Invoke(target, null); } catch { return null; }
            }
            return null;
        }

        private static object ReadMember(object target, string name)
        {
            if (target == null || string.IsNullOrEmpty(name)) return null;
            for (var type = target.GetType(); type != null; type = type.BaseType)
            {
                var field = type.GetField(name,
                    BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic |
                    BindingFlags.DeclaredOnly);
                if (field != null)
                {
                    try { return field.GetValue(target); } catch { return null; }
                }

                var property = type.GetProperty(name,
                    BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic |
                    BindingFlags.DeclaredOnly);
                if (property != null && property.GetIndexParameters().Length == 0)
                {
                    try { return property.GetValue(target, null); } catch { return null; }
                }
            }
            return null;
        }

        private static string ReadString(object target, string name)
        {
            var value = ReadMember(target, name);
            return value == null ? null : value.ToString();
        }

        private static int? ReadInt(object target, string name)
        {
            var value = ReadMember(target, name);
            if (value == null) return null;
            try { return Convert.ToInt32(value); } catch { return null; }
        }

        private static bool? ReadBool(object target, string name)
        {
            var value = ReadMember(target, name);
            return value is bool ? (bool?)value : null;
        }

        private static string Safe(string value)
        {
            return string.IsNullOrEmpty(value) ? "<null>" : value;
        }

        private static Type FindType(string fullName)
        {
            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                try
                {
                    var type = assembly.GetType(fullName, false);
                    if (type != null) return type;
                }
                catch { }
            }
            return null;
        }
    }
}

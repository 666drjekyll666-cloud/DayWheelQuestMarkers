# inquisitor_talk diagnostic probe 0.1.0

Purpose: determine how the visible `inquisitor_talk` objective transitions at the Witch Hill meeting without modifying save state or production Day Wheel Quest Markers behavior.

## Identity

- Research branch: `research/inquisitor-talk-probe`
- Frozen source ref: `frozen/inquisitor-talk-probe-0.1.0`
- Exact build source: `3809809b55c03adb18e742b5b4bf605b4a1a58cd`
- CI run: `34752753713`
- CI job: `103711937416`
- Artifact: `InquisitorTalkProbe-0.1.0`
- Artifact ID: `10315678366`
- Raw DLL: `DayWheelQuestMarkers.InquisitorTalkProbe.0.1.0.dll`
- Raw DLL size: 13,824 bytes
- Raw DLL SHA-256: `0160265c05c4e8a1d05f1ea74644f3791b746d32d2bdbf78af8251748c6af41a`
- Artifact ZIP SHA-256: `cc78c65bac6a53a98c6d59b14ae48c2063fd8f2d8452f912c6f147400d51b82f`
- Build result: Release build succeeded with 0 warnings / 0 errors.

## Runtime behavior

The probe is a separate BepInEx plugin with GUID `nikich.gyk.calendarquestspins.inquisitortalkprobe`. It can coexist with stable Day Wheel Quest Markers 1.0.30.

It is read-only. Every 0.5 seconds it snapshots only:

- task states whose IDs are `inquisitor_talk` or begin with `inquisitor_`, across all known NPC owners;
- unlocked and blacklisted phrase IDs containing `inquisitor`;
- active scene and player position for context.

It logs the initial snapshot, then only state/phrase changes plus a 30-second heartbeat. It does not modify tasks, phrases, save data, dialogue state, or Day Wheel marker behavior.

## Requested test

1. Install the probe alongside the accepted Day Wheel Quest Markers 1.0.30.
2. Load the save where the journal objective is to meet the Inquisitor on Witch Hill on Wrath day.
3. Keep the probe installed until the meeting/cutscene/objective transition occurs.
4. Exit the game and provide the resulting BepInEx log.

The key evidence is expected around `TASK_CHANGED`, `TASK_ADDED`, `TASK_REMOVED`, `PHRASE_*`, and `PROBE_CONTEXT` lines. Remove the probe DLL after the diagnostic run.

Status: awaiting player runtime evidence.

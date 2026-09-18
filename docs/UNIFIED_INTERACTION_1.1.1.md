# Day Wheel Quest Markers 1.1.1 — root-aware unified interaction candidate

Status: **candidate / awaiting player validation / do not merge or release**.

Stable baseline remains **1.0.35**. The handed 1.1.0 candidate is superseded and must not be accepted.

## Runtime evidence that invalidated 1.1.0

Both player launches of 1.1.0 failed while rebuilding schema 3. The exact failure was:

`navigation bootstrap failed: no interaction-root navigation path for required answer npc_cultist / snake_back_12a`

The gameplay-safe fallback then left the manifest unavailable, so all markers disappeared. This explains the missing Astrologer marker even though the same log proves `@astrologer_diary` / the diary hand-in is rendered in the live Astrologer menu.

The current save also exposes a separate accepted-1.0.35 observation: three Astrologer markers were visible before handing over the diary. That contributor count is not yet attributed to specific rules; 1.1.1 intentionally does not guess the cause.

## Corrected contract

The accepted product rule already requires an exact-self-consuming answer to be reachable from an authored interaction root. 1.0.35 enforced this at runtime per candidate. 1.1.0 accidentally injected the non-`@` candidates into the production topic set before navigation's required-answer validation, turning one root-unreachable internal answer into a fatal bootstrap error.

1.1.1 restores the intended ordering:

1. derive the accepted owner/cross/`@` rules;
2. build and verify the interaction-root navigation index;
3. audit the complete non-`@` exact-self universe;
4. exclude task-completion-owned candidates;
5. exclude exact-self candidates with no interaction-root path;
6. admit the remaining candidates into the unified TopicRule representation;
7. validate and persist one schema-3 manifest.

The complete audited universe remains 6 graphs / 77 unique non-`@` IDs / 19 exact-self / 0 reversible / 0 utility. The 19 exact-self candidates must now partition exactly into admitted + completion-excluded + navigation-excluded.

## Build identity

- Branch: `dev/1.1.1`
- Candidate ref: `candidate/1.1.1`
- Exact executable/build source: `6ec1077560311e608fde8667a99df82fbca6112d`
- CI run: `35401247903`
- CI job: `105781395380`
- Build result: success; 0 warnings / 0 errors
- Artifact: `DayWheelQuestMarkers-1.1.1` (`10570606342`)
- Raw DLL: 92,672 bytes
- Raw DLL SHA-256: `f6b55b2b172675af07a890034ccf160350e1a90966a96ff5948d64f71d9e94f6`

## Required player validation

Use the preserved save before handing the diary to the Astrologer. Do not delete cache files.

First launch must bootstrap schema 3 successfully and must not report the old `snake_back_12a` failure. Before consuming the diary, record the Astrologer marker count. If it is anything other than one, do not consume the diary yet; return the log so a narrow contributor diagnostic can be made against the preserved state.

If the first launch is sane, fully restart once. The second launch must load the schema-3 manifest directly and report FlowCanvas graph parsing skipped, while preserving the same marker behavior.

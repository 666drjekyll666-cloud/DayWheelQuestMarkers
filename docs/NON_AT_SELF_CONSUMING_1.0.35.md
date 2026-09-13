# 1.0.35 — generalized non-`@` exact-self-consuming reminders

Status: **accepted stable**.

## Why this replaces 1.0.33 / 1.0.34

Two consecutive false negatives had the same structural cause:

- Astrologer `astrologer_2a_1b_6c`;
- Snake `snake_1a` with authored `Item:faith=5`.

Both are authored selectable answers whose IDs do not start with `@`. The accepted generic one-shot classifier therefore ignored them even though Graveyard Keeper persists their completion by adding the exact answer ID to `GameSave.black_list_of_phrases`.

The read-only six-NPC universe audit (`docs/NON_AT_ANSWER_UNIVERSE_AUDIT.md` on `research/non-at-answer-universe`) proved that this is a general structural class:

- six graphs audited;
- 243 answer occurrences;
- 91 non-`@` occurrences;
- 77 unique non-`@` IDs;
- 19 exact-self-blacklisting non-`@` candidates;
- 0 reversible candidates;
- 0 utility-like (`Leave`/`Back`/`Trade`) candidates.

Both known false negatives are among those 19. `snake_1a` additionally proves that exact CustomFunction UID reverse jumps are required for this class.

## Runtime architecture

1.0.35 starts from accepted 1.0.32 `main`, not from the tactical 1.0.33/1.0.34 source line.

The accepted schema-2 `rules-1.407.bin` and its owner/cross/`@`-topic/navigation contracts are unchanged.

A new `NonAtSelfConsumingRuleCache` handles only the newly verified structural class:

- bootstrap parses only the six weekday-NPC graphs and only during the loading window;
- a candidate must be a real `Flow_MultiAnswer` answer whose selected flow reaches a blacklist operation for the exact same answer ID;
- reverse tracing understands numbered `Flow_WaitForFlow` inputs and exact `CustomFunctionCall._sourceOutputUID -> CustomFunctionEvent._UID` jumps;
- the bootstrap hard-validates the audited 1.407 universe: 6 graphs / 77 unique non-`@` IDs / 19 exact-self candidates / 0 reversible / 0 utility;
- answers already owned by the existing production-style task-completion census are excluded from this supplemental generic layer to prevent duplicate markers;
- authored `Flow_Answer` price/lock gates are persisted structurally and evaluated at runtime with game-owned SmartRes / `Player.IsEnough`;
- current actionability also requires the accepted `NavigationReachabilityCache` root-to-answer path;
- unknown/unsupported structures fail closed;
- no translated/display-text matching exists;
- no gameplay FlowCanvas traversal exists.

The supplemental structural cache is persisted separately at:

`BepInEx/cache/DayWheelQuestMarkers/non-at-self-consuming-1.407.bin`

The accepted schema-2 file remains:

`BepInEx/cache/DayWheelQuestMarkers/rules-1.407.bin`

Upgrading to 1.0.35 therefore does **not** require deleting or rebuilding `rules-1.407.bin`.

## Frozen accepted identity

- Development branch: `dev/1.0.35`.
- Exact executable/build source: `5e8305ea6c2515dd0694343a07eb70e403ad0528`.
- Candidate ref: `candidate/1.0.35` at that exact source.
- Accepted baseline ref: `baseline/1.0.35-accepted` at that exact source.
- CI run: `34784684310`.
- CI job: `103797890060`, success.
- Release build: **0 warnings / 0 errors**.
- Artifact: `DayWheelQuestMarkers-1.0.35` (`10325673885`).
- Artifact ZIP digest: `sha256:508b338e2b3f6296f0ee5aef89ce62c4cd4562fb873eb0a1ae2ed8eb979618bf`.
- Raw DLL: 98,816 bytes.
- Raw DLL SHA-256: `a7752d2058d4728db054adc049f650cad037cede41166b6c4c7329f4ea169879`.
- Build workflow was returned to manual-only after candidate production; later docs/workflow commits do not alter the frozen executable source or DLL bytes.

## Player acceptance

The player confirmed the intended live lifecycle on the preserved save:

- Snake had a weekday marker while `snake_1a` / “Попытаться убедить” was available with the required 5 Faith;
- after selecting that interaction, the Snake marker contribution disappeared naturally;
- Ms. Charm simultaneously had a marker for her own 5-Faith-gated interaction;
- spending the same 5 Faith on Snake made Charmel's authored requirement unsatisfied, and her marker disappeared as expected.

This validates both halves of the generalized runtime contract: exact self-consumption closes the one-shot reminder, while supported SmartRes gates continue to track live resource sufficiency rather than remaining latched.

The user explicitly approved promotion to `main` and stable release on 2026-09-14.

## Architectural follow-up

The functional rule is accepted. A separate research-only architecture audit is warranted because production now contains several historically accumulated actionability sources (`WeekdayInteractionRuleCache`, `VerifiedCompletionReminderRules`, persisted `@` one-shots, non-`@` self-consuming supplements, cross-owner rules). Any consolidation must preserve the accepted behavior and performance evidence rather than refactor for aesthetics alone.
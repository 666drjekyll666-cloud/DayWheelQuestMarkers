# Non-`@` answer universe audit — GK 1.407

Status: **completed; general structural class verified**.

## Question

Two consecutive real player false negatives exposed weekday-NPC interactions whose final selectable answer IDs do not start with `@`:

- Astrologer `astrologer_2a_1b_6c` (portal progression);
- Snake `snake_1a` (persuade, authored `Item:faith = 5` gate).

Accepted generic one-shot production logic required an exact self-consuming persisted `@...` topic. The research question was whether `@` is merely an identifier convention and the actual safe structural discriminator is instead:

`currently reachable authored answer + exact self-blacklist of that same answer ID + supported/satisfied gates -> reminder`

## Existing evidence before the census

1. The accepted production parser explicitly discarded blacklist additions whose ID does not start with `@`; this was a code-policy boundary, not a proven game semantic boundary.
2. Historical runtime save evidence contains non-`@` IDs in `GameSave.black_list_of_phrases`, including `snake_1a`, `snake_1b`, `snake_back_12a`, and `snake_back_12b`.
3. Decompiled GK 1.407 evidence checks `black_list_of_phrases.Contains("astrologer_2a_1b_6c")` before applying the portal-progression transition. Therefore blacklist persistence is demonstrably used for a non-`@` answer ID.
4. Existing accepted generic one-shot semantics already treat a unique authored self-consuming conversation as reminder-worthy even without a journal-task mutation.

## Probe method

The read-only probe audited all six weekday NPC serialized graphs. It:

- enumerated every authored `Flow_MultiAnswer` answer occurrence;
- counted non-`@` answer occurrences and unique IDs;
- inspected every `Flow_BlackListPhrase` / non-removing `Flow_AddPhraseToBlacklist` operation;
- reverse-traced to exact authored answer anchors, including exact `CustomFunctionCall._sourceOutputUID -> CustomFunctionEvent._UID` jumps and numbered `Flow_WaitForFlow` inputs;
- emitted only cases where the selected answer ID exactly equals the ID added to the blacklist;
- reported price/lock gate structure;
- checked matching blacklist-removal operations (`remove=true`);
- checked utility-like IDs (`Leave`, `Back`, `Trade`);
- explicitly verified `astrologer_2a_1b_6c` and `snake_1a`.

## Runtime result

The supplied current-save runtime log completed the full six-graph census:

- graphs: **6/6**;
- authored answer occurrences: **243**;
- non-`@` occurrences: **91**;
- unique non-`@` answer IDs: **77**;
- exact-self-blacklisting non-`@` candidates: **19**;
- unique NPC/answer exact-self candidates: **19**;
- reversible candidates: **0**;
- utility-like candidates: **0**.

Both known false negatives are members of the exact-self class:

- `npc_astrologer / astrologer_2a_1b_6c`: exact-self = true;
- `npc_cultist / snake_1a`: exact-self = true, one exact CustomFunction jump, authored `Item:faith=5` price gate.

The same class also contains additional authored finite interactions, including the three Inquisitor dark-organ hand-ins, Snake's early alternative branches, Merchant one-time/crop answers, `actress_2a_2`, and Bishop/Astrologer answers that may already overlap the task-completion classifier.

No `Leave`, `Back`, or `Trade` control entered the exact-self class. No exact-self non-`@` candidate is later removed from the blacklist in the six audited graphs.

## Conclusion

The `@` prefix is **not** a semantic boundary for one-shot dialogue state in Graveyard Keeper 1.407. The safe structural rule is exact self-consumption, not the identifier prefix.

Production may therefore generalize reminder coverage to non-`@` answers when all of the following hold:

1. the answer is an authored `Flow_MultiAnswer` entry;
2. its selected flow reaches a blacklist operation for the **exact same answer ID**;
3. the answer is not reversible in the audited graph;
4. supported authored `Flow_Answer` price/lock gates are satisfied through game-owned SmartRes / `Player.IsEnough`;
5. the existing persisted root-to-answer navigation contract says the answer is reachable now;
6. answers already owned by the task-completion classifier are excluded from the supplemental generic layer to avoid duplicate markers;
7. unsupported/unknown structures fail closed.

For the non-`@` exact-self reverse trace, normal flow traversal alone is insufficient: `snake_1a` proves that exact CustomFunction UID jumps must be understood. This stronger traversal should be isolated to exact-self discovery rather than silently changing the accepted owner/cross task classifier.

No translated/display-text matching or gameplay graph traversal is needed.

## Production decision

The two tactical candidates 1.0.33 and 1.0.34 should not become the long-term architecture. They are superseded conceptually by a generalized loading-derived/persisted non-`@` exact-self cache in the 1.0.35 line. The accepted schema-2 owner/cross/`@` manifest remains unchanged; the new structural class is isolated so the already-accepted 75/6 owner, 8/6/0 cross-owner, 55/54/1 `@` topic, and 210/270/151/0 navigation contracts are not broadened accidentally.

## Probe identity

Research branch: `research/non-at-answer-universe`.

Probe project: `research/NonAtAnswerAuditProbe`.

Probe identity: `Day Wheel Quest Markers - non-@ answer universe audit`, version `0.1.0`.

### Frozen build identity

- Exact executable/build source: `30809e5305fe86dbaab38ee8b4071119b678561d`.
- Frozen ref: `frozen/non-at-answer-audit-probe-0.1.0` at the exact build source.
- CI run: `34783667286`.
- CI job: `103795127129`, success; Release build **0 warnings / 0 errors**.
- Artifact: `DayWheelNonAtAnswerAuditProbe-0.1.0` (`10325573372`).
- Artifact ZIP digest: `sha256:0902d6d05f11365d9876aae7c13374ab6473ac319ea50745d0db56a82032f067`.
- Raw DLL: 22,016 bytes.
- Raw DLL SHA-256: `d23327dd8e12baff01bae191fdc5112cadd692eef64499aa68174067c4c77375`.
- The research workflow was returned to manual-only after the successful build.

The probe is now retired from production testing; remove it before validating 1.0.35.

# Non-`@` answer universe audit — GK 1.407

Status: **research in progress; no production behavior change**.

## Question

Two consecutive real player false negatives exposed weekday-NPC interactions whose final selectable answer IDs do not start with `@`:

- Astrologer `astrologer_2a_1b_6c` (portal progression);
- Snake `snake_1a` (persuade, authored `Item:faith = 5` gate).

Accepted generic one-shot production logic currently requires an exact self-consuming persisted `@...` topic. The research question is whether `@` is merely an identifier convention and the actual safe structural discriminator is instead:

`currently reachable authored answer + exact self-blacklist of that same answer ID + supported/satisfied gates -> reminder`

If so, the generic one-shot rule can be generalized to exact self-consuming answers regardless of prefix, removing both recent special cases and preventing the same blind spot from recurring.

## Existing evidence before the new census

1. The current production parser explicitly discards blacklist additions whose ID does not start with `@`; this is a code-policy boundary, not a proven game semantic boundary.
2. Historical runtime save evidence contains non-`@` IDs in `GameSave.black_list_of_phrases`, including `snake_1a`, `snake_1b`, `snake_back_12a`, and `snake_back_12b`.
3. Decompiled GK 1.407 evidence checks `black_list_of_phrases.Contains("astrologer_2a_1b_6c")` before applying the portal-progression transition. Therefore blacklist persistence is demonstrably used for a non-`@` answer ID.
4. Existing accepted generic one-shot semantics already treat a unique authored self-consuming conversation as reminder-worthy even without a journal-task mutation. The key unknown is therefore whether exact self-consumption remains a clean discriminator outside the `@` namespace.

## Required universe test

A new read-only probe audits all six weekday NPC serialized graphs. It must:

- enumerate every authored `Flow_MultiAnswer` answer occurrence;
- count non-`@` answer occurrences and unique IDs;
- inspect every `Flow_BlackListPhrase` / non-removing `Flow_AddPhraseToBlacklist` operation;
- reverse-trace to exact authored answer anchors, including exact `CustomFunctionCall._sourceOutputUID -> CustomFunctionEvent._UID` jumps and numbered `Flow_WaitForFlow` inputs;
- emit only cases where the selected answer ID exactly equals the ID added to the blacklist;
- report supported price/lock gate structure;
- flag any matching blacklist-removal operation (`remove=true`), because reversible state would weaken one-shot semantics;
- flag utility-like IDs (`Leave`, `Back`, `Trade`) as explicit controls;
- explicitly verify whether `astrologer_2a_1b_6c` and `snake_1a` belong to the exact-self-consume class.

## Decision boundary

A general production rule is acceptable only if the whole six-NPC census supports it. In particular:

- exact self-consuming non-`@` answers must not include ordinary repeatable utility/menu controls;
- reversible candidates require separate analysis and are not admitted automatically;
- runtime actionability must still use the existing root-to-answer navigation reachability and authored `Flow_Answer` gates;
- unknown/unsupported gate or navigation shapes continue to fail closed;
- no translated/display-text matching is allowed;
- no gameplay graph traversal may be introduced. Any generalized rule must be loading-derived and persisted like the existing schema-2 structural data.

## Probe

Research branch: `research/non-at-answer-universe`.

Probe project: `research/NonAtAnswerAuditProbe`.

Probe identity: `Day Wheel Quest Markers - non-@ answer universe audit`, version `0.1.0`.

The probe is read-only and performs one bounded census after the game/save and six weekday NPC graphs are available. It does not mutate save, task, phrase, resource, or production reminder state.

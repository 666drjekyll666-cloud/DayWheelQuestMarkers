# Verified Runtime Data — Graveyard Keeper 1.407

This document records runtime facts that production Day Wheel Quest Markers is allowed to depend on. Unknown structures fail closed.

## Weekday HUD

Verified calendar root:

`UI Root/HUD/hud left/hud spr/circle`

The six physical weekday-symbol objects remain in fixed positions while their semantic weekday changes as the wheel advances. `HUDSinIcon._sin_type` is therefore the authoritative current semantic value; fixed physical indices are not valid NPC mappings.

Verified periodic NPC mapping:

- Astrologer -> Sloth -> sin value 1
- Inquisitor -> Wrath -> 2
- Snake/Cultist -> Envy -> 3
- Merchant -> Gluttony -> 4
- Ms. Charm/Actress -> Lust -> 5
- Bishop -> Pride -> 6

Normal menus hide/deactivate the HUD rather than destroying it. If the actual HUD is recreated, marker objects must be rebuilt and current reminder state reapplied.

## Native marker styles

`GameSave.SetTaskState` uses these marker categories:

- base/default -> `icon_quest_mark_small`
- `dlc_stories_...` -> `dlc_quest_mrk`
- `dlc_refugees...` or `s_ev...` -> `quest_marker_violet`
- `dlc_souls...` -> `Icon_quest_mark_small_blue`

Verified dimensions are 10x10 for base/violet/Souls and 12x12 for Stories, with centered pivots and native pixel-art presentation.

The accepted visual contract is the native art and category color, placed at the established outward position, with no custom outline or silhouette.

Public production resolves these game-owned Sprite objects from the installed game's already-loaded runtime objects. The normal lookup is performed once during loading/prewarm and cached. If a requested DLC style was not resident then, at most one bounded fallback lookup is allowed when that real marker is first requested. Game-owned Sprite objects are referenced only and are never destroyed by the mod.

## Journal state and task-linked actionability

`KnownNPC.TaskState` contains task ID and state. `Visible` alone does not mean actionable: verified negative cases include missing quest items, quality requirements, relation requirements, restoration tools, wine, trade license, church/cemetery quality, and other prerequisites.

Normal task mutation path:

`FlowCanvas.Nodes.Flow_SetTaskState -> GameSave.SetTaskState`

For task-linked reminders production therefore requires an authored weekday-NPC route plus currently satisfied dialogue/resource/navigation gates, not merely a visible journal entry.

## Dialogue / resource gates

`GameSave.unlocked_phrases` and `black_list_of_phrases` participate in actual answer visibility.

Verified direct `Flow_Answer` price/lock requirements are evaluated by Graveyard Keeper through `Player.IsEnough(SmartRes)`. Production reconstructs the authored SmartRes and delegates sufficiency to the game rather than duplicating item/quality/relation rules.

If a route uses an unsupported answer/gate shape, no reminder is emitted.

## Verified persistent exact-self-consuming dialogue semantics

Graveyard Keeper has authored dialogue nodes that persistently consume an answer by adding its exact answer/phrase ID to `GameSave.black_list_of_phrases`:

- `FlowCanvas.Nodes.Flow_BlackListPhrase` calls `GameSave.AddPhraseToBlackList(phrase)`;
- `FlowCanvas.Nodes.Flow_AddPhraseToBlacklist` calls the same method when its authored `remove` input is false; when `remove` is true it removes the phrase from the blacklist instead.

The `@` prefix is an identifier convention used by many persisted topics, but accepted 1.0.35 evidence proves it is **not** the semantic boundary for a one-time authored interaction.

The full read-only GK 1.407 audit of all six weekday-NPC graphs established:

- 243 authored MultiAnswer occurrences;
- 91 non-`@` occurrences;
- 77 unique non-`@` answer IDs;
- exactly 19 non-`@` candidates whose selected branch blacklists that exact same answer ID;
- 0 reversible candidates among those 19;
- 0 utility-like `Leave` / `Back` / `Trade` candidates among those 19;
- both previously observed false negatives, Astrologer `astrologer_2a_1b_6c` and Snake `snake_1a`, belong to that exact-self-consuming class.

The accepted structural distinction is therefore:

- one-time conversations may use either `@...` or non-`@` answer IDs and consume their own exact ID;
- repeatable utility choices such as Trade / Leave / Back do not self-consume;
- submenu/container headers that do not consume themselves are not reminder interactions merely because their menu is visible.

Runtime evidence from earlier accepted releases established the same lifecycle for `@` topics such as `@inquisitor_magic_item`. Accepted 1.0.35 player evidence establishes it for non-`@` answers: Snake `snake_1a` produced a marker while its 5-Faith gate was satisfied, then disappeared after the player selected the interaction and the answer was consumed. In the same state Ms. Charm had another 5-Faith-gated interaction; spending the five Faith on Snake made that gate unsatisfied and her marker disappeared as well.

This establishes the general reminder source:

`currently reachable independent interaction + exact authored self-consuming answer + supported/satisfied answer gates -> weekday marker`

The rule intentionally does **not** require a journal task mutation or downstream quest effect. A unique authored conversation itself can be reminder-worthy.

Production guardrails for this rule:

1. the answer must be authored on one of the six weekday-NPC graphs;
2. its own authored route must add the exact same answer ID to the phrase blacklist;
3. `Flow_AddPhraseToBlacklist` nodes with `remove=true` are not consumption evidence;
4. the answer must not already be blacklisted; `@...` topics additionally require their authored unlocked-phrase state;
5. supported authored final-answer `Flow_Answer` price/lock gates must pass game-owned sufficiency checks;
6. every required ancestor menu/answer on at least one authored root-to-answer path must currently be reachable;
7. answers already owned by task-completion rules remain handled by the task-linked rule set rather than being double-counted by the generic exact-self-consuming layer;
8. a generic exact-self answer must have at least one authored root-to-answer path that does not first pass through an already task-owned answer; downstream choices that exist only inside the same interaction chain are part of that visit, not separate reminders;
9. unsupported or ambiguous topology/gates fail closed;
10. no translated/display text is used for classification.

Accepted 1.1.3 compiles both persisted `@` topics and the audited non-`@` exact-self class into one schema-4 persistent manifest at `BepInEx/cache/DayWheelQuestMarkers/rules-1.407.bin`. The old 1.0.35 `non-at-self-consuming-1.407.bin` file is no longer read by production and can remain harmlessly on disk.

## Verified root-to-answer navigation reachability

The 1.0.30 audit established that checking only a final answer's own gates is insufficient for nested dialogue. A final child answer can look structurally actionable while its parent menu is currently blocked or already consumed.

Verified defect/control cases:

- Charmel: `actress_2b -> @actress_2b_1a/@actress_2b_1b`; the children have no own `AnswerData` gate, but the parent requires the relevant relation gate. In the reported player state the parent was rendered but unpickable; the old final-answer-only classifier produced two false Lust-day markers.
- Merchant business submenu: `@merchant_business -> @merchant_marketing_done/@merchant_sales_done`; the child completions must not survive as reminders if the persisted parent route is no longer reachable.
- Merchant debt submenu: `@merchant_2e -> @merchant_2e_1f`; final-answer price alone is not enough if the parent route is unavailable.
- Bishop control: `about_cathedral` is an unconditional plain parent and therefore compiles away rather than becoming a recurring runtime predicate.

Accepted navigation contract:

- bootstrap derives one or more authored root-to-final-answer paths for reminder-bearing answers;
- within one path, required ancestor phrase predicates and supported ancestor `AnswerData` price/lock gates are AND conditions;
- alternative authored paths are OR;
- unconditional plain ancestors are omitted from persisted predicates;
- for generic non-`@` exact-self candidates, at least one path must contain no task-owned answer ancestor; otherwise the answer is a same-visit continuation rather than an independent reminder;
- unsupported or ambiguous navigation ancestry fails closed;
- gameplay evaluates only compact persisted predicates and never traverses FlowCanvas/navigation graphs.

First accepted schema-2 bootstrap produced: **210 answers, 270 paths, 151 predicates, 0 unsupported paths**. The verified contract checks for the known Charmel/Merchant chains and control cases passed before the manifest was accepted.

## Owner-local and cross-owner rules

Owner-local task reminder:

1. saved task is Visible;
2. owning weekday NPC graph contains the authored completion route;
3. route resolves to a supported task-linked answer;
4. phrase/blacklist state allows it;
5. verified price/lock requirements pass the game's own sufficiency check;
6. at least one authored root-to-final-answer navigation path is currently reachable.

Cross-owner task reminder is allowed only when a weekday NPC graph explicitly completes a task stored under another NPC and the same actionability/navigation gates pass.

These task-linked rules remain necessary because not every actionable quest interaction is represented by generic exact self-consumption.

## Verified completion-route supplements

The accepted direct owner-task classifier does not represent every verified completion topology. Accepted 1.0.32 therefore retains a narrow supplement:

- promoted task/topic pairs reuse existing persisted exact-self-consuming topic/navigation predicates;
- `npc_cultist/snake_trap` uses the verified `snake_stone_ready` answer plus `_rel >= 10` through game-owned SmartRes sufficiency;
- `npc_inquisitor/inquisitor_talk` and `npc_cultist/snake_back` are exact mandatory interaction-event stages whose visible task is the verified actionability boundary;
- unsupported `@souls_s_s33_ask` remains fail-closed;
- there is no broad `Visible task`, `CustomEvent`, or `AddInteractionEvent` classifier.

Research after accepted 1.0.35 shows that several answer-backed supplemental routes are candidates for future consolidation into a common graph compiler, but the two mandatory event-only stages remain a genuinely distinct evidence type unless a broader event contract is separately proved.

## Verified bridge / intermediate topics

Prior research established narrow objective stages that the direct task-completion model missed:

- Miller -> Astrologer mill-calculation bridge: `@astrologer_fix_mill` -> `@astrologer_fix`, continuation relation gate 60;
- Astrologer -> Snake instrument bridge: `@snake_instrument` -> `@snake_instrument_ready`, continuation relation gate 40;
- six verified 1.0.22 intermediate families covering `astrologer_daghter`, `bishop_invitation_2`, `inquisitor_guards`, `merchant_support`, `snake_help`, and `actress_necklace` stages.

Static persisted-topic evidence confirms these reminder-bearing stage topics are exact self-consuming authored topics. Production derives them through the generic one-shot classifier instead of maintaining parallel `VerifiedBridgeReminderRules` / `VerifiedIntermediateReminderRules` manifests.

### Verified Ms. Charm -> Snake counterfeit-coins submenu parent

A 2026-09-19 runtime/state capture plus existing GK 1.407 graph audits establish one additional exact intermediate shape that is intentionally **not** generalized into the exact-self topic compiler:

- Ms. Charm's `@actress_2b_1a` branch makes `npc_actress/actress_money` Visible and unlocks Snake phrase `@snake_1с`;
- Snake `@snake_1с` is top-level `Flow_MultiAnswer` entry `multi=106 index=2`, with authored price `Item:quest_fake_coins = 1`;
- the parent is a submenu boundary and does **not** directly blacklist itself;
- its two authored child answers `snake_1с_4a` and `snake_1с_4b` both blacklist parent `@snake_1с` and unlock `@actress_snake_back`;
- Ms. Charm `@actress_snake_back` then completes `actress_money` and exposes the next necklace stage;
- in the captured live state, `actress_money` is Visible, `@snake_1с` is unlocked/not blacklisted, `@snake_instrument` is simultaneously actionable, the game renders both Snake conversations, but accepted 1.1.3 contributes only `@snake_instrument`, producing one marker.

This is a verified **task-linked one-visit submenu parent**. It must not be admitted by a broad "open submenu" heuristic: production handling is allowed only as an exact verified route with the owner task state, exact phrase state, and authored `quest_fake_coins x1` SmartRes gate all satisfied.

## Authoritative zone-quality mirrors

GK 1.407 graphs can mirror live `WorldZone.GetTotalQuality()` into a player `GameRes` through an authored `Flow_SetPlayerParam <- Flow_GetQualityOfZone` value edge. The Snake `sacrifice_quality` case proved that the stored player parameter may be stale before the real dialogue branch refreshes it.

Production may derive an authoritative mirror only when that exact graph edge is unambiguous. For such an owner-local requirement it compares the live `WorldZone.GetTotalQuality()` against the authored requirement value instead of trusting the stale mirrored player parameter. Ambiguous or unresolved mirrors fail closed.

Cross-owner and generic exact-self-consuming routes retain their accepted SmartRes/`Player.IsEnough` semantics unless separate evidence establishes that authoritative zone substitution is required there.

## Accepted persistent loading/performance contract

Accepted runtime architecture as of 1.1.3:

- per frame: timer comparison only until the one-second refresh is due;
- one schema-4 manifest path: `BepInEx/cache/DayWheelQuestMarkers/rules-1.407.bin`;
- schema 4 stores accepted owner/cross task rules, unified self-consuming topics, and compact navigation predicates;
- `UnifiedSelfConsumingCompiler` exists only for loading/bootstrap derivation; there is no separate gameplay non-`@` cache path;
- the legacy 1.0.35 `non-at-self-consuming-1.407.bin` is ignored;
- when schema 4 is missing/incompatible, required graph parsing occurs only in the verified loading window;
- later full launches deserialize compact cached data and recreate only live runtime bindings; normal gameplay is not allowed to invoke the graph parser;
- once per second: evaluate cached task/interaction state, phrase state, cached navigation predicates, game-owned gate predicates, and live HUD semantics;
- approximately every 30 seconds: perform allocation-light runtime/known-NPC validation through cached references and a `ulong` fingerprint rather than list/sort/string construction;
- real known-NPC membership changes use cheap rebinding from the manifest, with no graph parse;
- no background worker and no save mutation.

Accepted 1.1.3 runtime evidence:

- schema-3 -> schema-4 rebuild behind loading: **900.79 ms**;
- first accepted schema-4 summary: owner 75/6, cross-owner 8/6/0, unified self-consuming 61/60/1, non-`@` universe 77 / exact-self 19 / admitted 6, navigation 210/270/151/0;
- full restart with cache untouched: schema-4 manifest loaded in **10.42 ms** and explicitly logged `FlowCanvas graph parse skipped`;
- the pre-diary Astrologer state showed exactly one marker instead of the earlier three; `astrologer_diary_9a/9b` are verified same-visit continuations and no longer contribute independent reminders;
- after diary completion, the player observed one marker for the actually executable Acid hand-in while Restoration Tools were absent, confirming live item gating remains authoritative;
- `fh=True` in dialogue logging is not sufficient by itself to prove an interaction is executable under all live resource gates; acceptance uses the actual production gate evaluation and player-observed executable state.

Historical performance lineage:

- 1.0.25 exposed a measured **302.22 ms** first-weekday-NPC runtime structural rebuild;
- 1.0.26 moved structural parsing behind loading and persisted it;
- 1.0.27 moved the cache under BepInEx;
- 1.0.28 removed recurring steady-state allocations and the previous roughly 30-second rhythmic freeze pattern disappeared in player testing;
- 1.0.30 added persisted navigation reachability while preserving the allocation-light steady state;
- 1.0.32 added narrow verified completion-route predicates;
- 1.0.35 added the audited non-`@` exact-self-consuming structural class as a separate supplement;
- 1.1.3 consolidated that supplement into schema 4 and added independent-path deduplication without adding gameplay graph traversal.

The remaining sparse hitches are not attributed to Day Wheel: the same modpack/control work established a comparable baseline without Day Wheel, and accepted logs contain separate Unity `UnloadUnusedAssets` operations around 0.7 s with roughly 934k loaded objects.

The rejected universal provenance-parser experiment pushed loading work toward roughly 1.8 seconds and is not an accepted architecture. Do not reintroduce arbitrary external dependency/provenance traversal into production.

## Accepted architecture consolidation in 1.1.3

The post-1.0.35 audit found that the semantic model was more unified than the implementation. Accepted 1.1.3 performs the narrow safe consolidation that runtime evidence justified:

- the separate `NonAtSelfConsumingRuleCache` production layer is removed;
- audited non-`@` exact-self candidates compile into the same `WeekdayInteractionRuleCache.TopicRule` representation used at runtime;
- schema 4 persists unified topic/navigation data in one primary manifest;
- navigation supplies the independent-root-path check that prevents same-visit downstream exact-self answers from becoming extra reminders;
- the owner/cross task derivation and the two verified mandatory event-only stages remain evidence-specific rather than being generalized into a universal parser.

The broader research recommendation for a single shared parsed graph index remains optional future engineering work. The rejected universal provenance parser remains rejected: production does not traverse arbitrary external quest dependency chains, and accepted 1.1.3 preserves bounded parsing of the six weekday-NPC graphs only.

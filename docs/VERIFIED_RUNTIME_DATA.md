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

## Verified persistent one-shot dialogue semantics

Graveyard Keeper has authored dialogue nodes that persistently consume a topic by adding its exact phrase ID to `GameSave.black_list_of_phrases`:

- `FlowCanvas.Nodes.Flow_BlackListPhrase` calls `GameSave.AddPhraseToBlackList(phrase)`;
- `FlowCanvas.Nodes.Flow_AddPhraseToBlacklist` calls the same method when its authored `remove` input is false; when `remove` is true it removes the phrase from the blacklist instead.

Raw GK 1.407 weekday-NPC graphs use persisted `@...` topic IDs in `Flow_MultiAnswer`. The authored-universe audit established a clean structural distinction relevant to the mod:

- one-time conversations such as `@inquisitor_magic_item` directly blacklist their own exact phrase after selection;
- repeatable utility choices such as Trade / Leave / Back do not self-blacklist;
- submenu/container headers such as `@snake_about_nacklase` do not self-blacklist, while the actual one-time child topics inside that submenu do.

Runtime evidence from the 1.0.22 player test confirmed `@inquisitor_magic_item` was visibly rendered and selectable by the Inquisitor while the old task/progression-only classifier emitted no marker. The earlier static universe audit had already classified that exact topic as supported, externally unlocked, self-consuming, and opening `@inquisitor_magic_100`; it was rejected only by the now-retired requirement that the source side also prove a task/progression dependency.

This establishes a broader valid reminder source:

`currently reachable + exact authored self-consuming @topic + supported/satisfied answer gates -> weekday marker`

The one-shot rule intentionally does **not** require a journal task mutation or downstream quest effect. The unique authored conversation itself is reminder-worthy.

Production guardrails for this rule:

1. the answer ID must be an authored persisted `@...` topic on one of the six weekday-NPC graphs;
2. its own authored route must add that same exact ID to the phrase blacklist;
3. `Flow_AddPhraseToBlacklist` nodes with `remove=true` are not consumption evidence;
4. the phrase must currently be unlocked and not blacklisted;
5. any supported authored final-answer `Flow_Answer` price/lock gates must pass `Player.IsEnough`;
6. every required ancestor menu/answer on at least one authored root-to-answer path must also currently be reachable;
7. direct task-completion answers remain handled by the task-linked rule set rather than being double-counted by the generic one-shot layer;
8. previously verified bridge/intermediate topics use this same exact self-consuming structure and no longer require separate hard-coded manifests.

No translated/display text is used for this classification.

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
- unsupported or ambiguous navigation ancestry fails closed;
- gameplay evaluates only the compact persisted predicates and never traverses FlowCanvas/navigation graphs.

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

These task-linked rules remain necessary because not every actionable quest interaction is represented by the generic one-shot condition.

## Verified bridge / intermediate topics

Prior research established narrow objective stages that the direct task-completion model missed:

- Miller -> Astrologer mill-calculation bridge: `@astrologer_fix_mill` -> `@astrologer_fix`, continuation relation gate 60;
- Astrologer -> Snake instrument bridge: `@snake_instrument` -> `@snake_instrument_ready`, continuation relation gate 40;
- six verified 1.0.22 intermediate families covering `astrologer_daghter`, `bishop_invitation_2`, `inquisitor_guards`, `merchant_support`, `snake_help`, and `actress_necklace` stages.

Static persisted-topic evidence confirms these reminder-bearing stage topics are exact self-consuming authored topics. Production derives them through the same generic one-shot classifier instead of maintaining parallel `VerifiedBridgeReminderRules` / `VerifiedIntermediateReminderRules` manifests.

## Authoritative zone-quality mirrors

GK 1.407 graphs can mirror live `WorldZone.GetTotalQuality()` into a player `GameRes` through an authored `Flow_SetPlayerParam <- Flow_GetQualityOfZone` value edge. The Snake `sacrifice_quality` case proved that the stored player parameter may be stale before the real dialogue branch refreshes it.

Production may derive an authoritative mirror only when that exact graph edge is unambiguous. For such an owner-local requirement it compares the live `WorldZone.GetTotalQuality()` against the authored requirement value instead of trusting the stale mirrored player parameter. Ambiguous or unresolved mirrors fail closed.

Cross-owner and generic one-shot routes retain their accepted SmartRes/`Player.IsEnough` semantics unless separate evidence establishes that authoritative zone substitution is required there.

## Accepted persistent loading/performance contract

Accepted 1.0.30 architecture:

- per frame: timer comparison only until one-second refresh is due;
- persistent manifest path: `BepInEx/cache/DayWheelQuestMarkers/rules-1.407.bin`;
- when the manifest is missing/incompatible, the six weekday-NPC graphs are parsed only in the verified loading window and the compact structural/navigation manifest is persisted;
- later full launches deserialize the manifest and recreate only live runtime bindings; normal gameplay is not allowed to invoke the graph parser;
- once per second: evaluate cached task/topic state, phrase state, cached navigation predicates, game-owned gate predicates, and live HUD semantics;
- approximately every 30 seconds: perform allocation-light runtime/known-NPC validation through cached references and a `ulong` fingerprint rather than list/sort/string construction;
- real known-NPC membership changes use cheap rebinding from the manifest, with no graph parse;
- no background worker and no save mutation.

Accepted player evidence:

- first 1.0.30 schema-2 bootstrap behind loading: **557.88 ms**;
- first bootstrap summary: owner 75/6, cross-owner 8/6/0, one-shot 55/54/1, navigation 210/270/151/0;
- subsequent full restart: persistent manifest loaded behind loading in **10.29 ms** and logged `FlowCanvas graph parse skipped`;
- the same canonical final-rule/navigation counts were restored on the second launch;
- the original early Charmel state with relation below 10 showed no two false Lust-day markers;
- no Day Wheel runtime structural rebuild or repeated graph parse was present in the accepted second-run log.

Performance lineage:

- 1.0.25 exposed a measured **302.22 ms** first-weekday-NPC runtime structural rebuild;
- 1.0.26 moved structural parsing behind loading and persisted it;
- 1.0.27 moved the cache under BepInEx;
- 1.0.28 removed recurring steady-state allocations and the previous roughly 30-second rhythmic freeze pattern disappeared in player testing;
- 1.0.30 preserves that steady-state architecture while adding persisted navigation reachability.

The remaining sparse hitches in the accepted 1.0.30 session are not attributed to Day Wheel: the same modpack/control work already established a comparable baseline without Day Wheel, and the 1.0.30 log contains separate Unity `UnloadUnusedAssets` operations around 0.7 s with roughly 934k loaded objects.

The rejected universal provenance-parser experiment pushed loading work toward roughly 1.8 seconds and is not an accepted architecture. Do not reintroduce arbitrary external dependency/provenance traversal into production.

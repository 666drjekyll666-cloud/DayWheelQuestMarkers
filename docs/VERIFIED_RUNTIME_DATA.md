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

Public production resolves these game-owned Sprite objects from the installed game's already-loaded runtime objects. The normal lookup is performed once during developed-save loading prewarm and cached. If a requested DLC style was not resident then, at most one bounded fallback lookup is allowed when that real marker is first requested. Game-owned Sprite objects are referenced only and are never destroyed by the mod.

## Journal state and task-linked actionability

`KnownNPC.TaskState` contains task ID and state. `Visible` alone does not mean actionable: verified negative cases include missing quest items, quality requirements, relation requirements, restoration tools, wine, trade license, church/cemetery quality, and other prerequisites.

Normal task mutation path:

`FlowCanvas.Nodes.Flow_SetTaskState -> GameSave.SetTaskState`

For task-linked reminders production therefore requires an authored weekday-NPC route plus currently satisfied dialogue/resource gates, not merely a visible journal entry.

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

`currently open + exact authored self-consuming @topic + supported/satisfied answer gates -> weekday marker`

The one-shot rule intentionally does **not** require a journal task mutation or downstream quest effect. The unique authored conversation itself is reminder-worthy.

Production guardrails for this rule:

1. the answer ID must be an authored persisted `@...` topic on one of the six weekday-NPC graphs;
2. its own authored route must add that same exact ID to the phrase blacklist;
3. `Flow_AddPhraseToBlacklist` nodes with `remove=true` are not consumption evidence;
4. the phrase must currently be unlocked and not blacklisted;
5. any supported authored `Flow_Answer` price/lock gates must pass `Player.IsEnough`;
6. direct task-completion answers remain handled by the task-linked rule set rather than being double-counted by the generic one-shot layer;
7. previously verified bridge/intermediate topics use this same exact self-consuming structure in accepted 1.0.24 and no longer require separate hard-coded manifests.

No translated/display text is used for this classification.

## Owner-local and cross-owner rules

Owner-local task reminder:

1. saved task is Visible;
2. owning weekday NPC graph contains the authored completion route;
3. route resolves to a supported task-linked answer;
4. phrase/blacklist state allows it;
5. verified price/lock requirements pass the game's own sufficiency check.

Cross-owner task reminder is allowed only when a weekday NPC graph explicitly completes a task stored under another NPC and the same actionability gates pass.

These task-linked rules remain necessary because not every actionable quest interaction is represented by the generic one-shot condition.

## Verified bridge / intermediate topics

Prior research established narrow special mappings for objective stages that the direct task-completion model missed:

- Miller -> Astrologer mill-calculation bridge: `@astrologer_fix_mill` -> `@astrologer_fix`, continuation relation gate 60;
- Astrologer -> Snake instrument bridge: `@snake_instrument` -> `@snake_instrument_ready`, continuation relation gate 40;
- six verified 1.0.22 intermediate families covering `astrologer_daghter`, `bishop_invitation_2`, `inquisitor_guards`, `merchant_support`, `snake_help`, and `actress_necklace` stages.

Static persisted-topic evidence confirms these reminder-bearing stage topics are exact self-consuming authored topics. Accepted 1.0.24 therefore derives them through the same generic one-shot classifier instead of maintaining parallel `VerifiedBridgeReminderRules` / `VerifiedIntermediateReminderRules` manifests. Runtime parity testing on the portal-item lifecycle confirmed that retiring the supplemental layer did not regress the accepted one-shot behavior.

## Authoritative zone-quality mirrors

GK 1.407 graphs can mirror live `WorldZone.GetTotalQuality()` into a player `GameRes` through an authored `Flow_SetPlayerParam <- Flow_GetQualityOfZone` value edge. The Snake `sacrifice_quality` case proved that the stored player parameter may be stale before the real dialogue branch refreshes it.

Production may derive an authoritative mirror only when that exact graph edge is unambiguous. For such an owner-local requirement it compares the live `WorldZone.GetTotalQuality()` against the authored requirement value instead of trusting the stale mirrored player parameter. Ambiguous or unresolved mirrors fail closed.

Cross-owner and generic one-shot routes retain their accepted SmartRes/`Player.IsEnough` semantics unless separate evidence establishes that authoritative zone substitution is required there.

## Accepted unified loading/performance contract

Accepted 1.0.24 architecture:

- per frame: timer comparison only until one-second refresh is due;
- developed save: once save/player/weekday-NPC objects and serialized graphs are verified ready while loading is still active, build one `WeekdayInteractionRuleCache` there;
- each of the six weekday-NPC graphs is parsed once per cache build and the same structural index yields owner-local, cross-owner, and self-consuming one-shot rules;
- gameplay start: rebind/revalidate prewarmed structure against final runtime objects;
- once per second: evaluate cached task state, cached one-shot topics, phrase state, game-owned gate predicates, and live HUD semantics;
- approximately every 30 seconds: check structural staleness, including known-NPC signature changes;
- fresh save with no known periodic NPC: no graph parse and no HUD/marker-resource work;
- no background worker and no save mutation.

Accepted player runtime evidence for 1.0.24 on the developed regression save:

- `Weekday interaction cache prewarmed behind loading screen in 308.07 ms. owner supported=75, cross-owner tasks=8, one-shot topics=55.`
- steady-state summary: owner supported=75, owner unsupported=6, cross-owner tasks=8, cross-owner supported=6, cross-owner unsupported=0, one-shot topics=55, one-shot supported=54, one-shot unsupported=1;
- the expected three portal-item markers appeared before the Inquisitor conversation; selecting `@inquisitor_magic_item` removed the Inquisitor marker;
- no Day Wheel Quest Markers error/warning was present in the supplied log.

For comparison, accepted 1.0.23 required three separate structural caches and measured **782.89 ms** on the corresponding developed-save test. The unified 1.0.24 cache measured **308.07 ms**, reducing this loading-prewarm work by about **60.6%** while preserving the tested behavior.

The rejected universal provenance-parser experiment pushed loading work toward roughly 1.8 seconds and is not an accepted architecture. Do not reintroduce arbitrary external dependency/provenance traversal into production.

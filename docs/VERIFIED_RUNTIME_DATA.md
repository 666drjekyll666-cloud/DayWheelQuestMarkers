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

## Journal state and actionability

`KnownNPC.TaskState` contains task ID and state. `Visible` alone does not mean actionable: verified negative cases include missing quest items, quality requirements, relation requirements, restoration tools, wine, trade license, church/cemetery quality, and other prerequisites.

Normal task mutation path:

`FlowCanvas.Nodes.Flow_SetTaskState -> GameSave.SetTaskState`

Production therefore requires an authored weekday-NPC completion route plus currently satisfied dialogue/resource gates, not merely a visible journal entry.

## Dialogue / resource gates

`GameSave.unlocked_phrases` and `black_list_of_phrases` participate in actual answer visibility.

Verified direct `Flow_Answer` price/lock requirements are evaluated by Graveyard Keeper through `Player.IsEnough(SmartRes)`. Production reconstructs the authored SmartRes and delegates sufficiency to the game rather than duplicating item/quality/relation rules.

If a route uses an unsupported answer/gate shape, no reminder is emitted.

## Owner-local and cross-owner rules

Owner-local reminder:

1. saved task is Visible;
2. owning weekday NPC graph contains the authored completion route;
3. route resolves to a supported task-linked answer;
4. phrase/blacklist state allows it;
5. verified price/lock requirements pass the game's own sufficiency check.

Cross-owner reminder is allowed only when a weekday NPC graph explicitly completes a task stored under another NPC and the same actionability gates pass.

## Verified bridge exceptions

The full authored-universe research found two base-game objective families that do not expose the normal task-completion anchor and are intentionally represented by narrow explicit internal-ID rules:

- Miller -> Astrologer mill-calculation bridge: `@astrologer_fix_mill` -> `@astrologer_fix`, continuation relation gate 60;
- Astrologer -> Snake instrument bridge: `@snake_instrument` -> `@snake_instrument_ready`, continuation relation gate 40.

Each family contributes at most one reminder across entry/continuation. Arbitrary relationship-only follow-ups are not generalized from these exceptions.

## Loading/performance contract

Accepted production architecture:

- per frame: timer comparison only until one-second refresh is due;
- developed save: once save/player/six periodic NPC objects and serialized graphs are verified ready while loading is still active, build structural caches there;
- gameplay start: rebind/revalidate prewarmed structure against final runtime objects;
- once per second: evaluate cached task state, phrase state, game-owned gate predicates, and live HUD semantics;
- approximately every 30 seconds: check structural staleness;
- fresh save with no known periodic NPC: no graph parse and no HUD/marker-resource work;
- no background worker and no save mutation.

The accepted 1.0.12/1.0.17 line moved the former roughly half-second developed-save graph parsing cost behind the loading screen. The rejected universal provenance-parser experiment pushed loading work toward roughly 1.8 seconds and is not an accepted architecture.

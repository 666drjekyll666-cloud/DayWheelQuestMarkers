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

## Verified persistent dialogue-lifecycle semantics

Graveyard Keeper has authored dialogue branches that persistently consume selectable entries through the phrase blacklist:

- `FlowCanvas.Nodes.Flow_BlackListPhrase` calls `GameSave.AddPhraseToBlackList(phrase)`;
- `FlowCanvas.Nodes.Flow_AddPhraseToBlacklist` does the same when `remove=false`; `remove=true` is reversible behavior and is not accepted as one-shot ownership evidence.

Earlier accepted versions proved the exact-self case. Accepted 1.1.6 proves the broader authored rule:

> **A dialogue interaction is owned by the nearest selectable entry on the concrete authored root-to-answer path whose persistent lifetime is consumed by the progressing branch.**

Accepted precedence:

1. If the selected/progressing answer persistently blacklists itself, self wins.
2. Otherwise inspect selectable ancestors on that same root path nearest-first.
3. The first persistently blacklisted selectable ancestor is the lifecycle owner.
4. If no self/ancestor is persistently consumed, the generic dialogue layer emits no interaction.
5. Alternative children that resolve to the same owner are variants of one interaction.
6. A lifecycle owner already represented by a task-owned visit is suppressed from the generic layer.
7. Descendants reachable only through an already task-owned interaction are same-visit continuations, not separate reminders.
8. Current phrase state, supported AnswerData/SmartRes gates, and navigation reachability remain authoritative.
9. Reversible, ambiguous, utility-like, unsupported, or root-unreachable structures fail closed.
10. Display text is irrelevant.

The `@` prefix is only an identifier convention. Accepted 1.0.35 proved non-`@` exact-self interactions; accepted 1.1.6 proves child-consumed parent ownership.

### Complete six-NPC lifecycle census

The read-only GK 1.407 census over all six weekday-NPC graphs established:

- 243 authored answer occurrences;
- 161 branches with persistent blacklist effects;
- 204 path-local self-owned paths;
- 12 path-local ancestor-owned paths;
- 0 reversible self/ancestor owners admitted.

The 12 ancestor-owned paths collapse to exactly six unique lifecycle owners:

- Astrologer `@tr_quest_13_research_1` — admitted;
- Snake `@snake_1с` — admitted;
- Merchant `@merchant_2b` — task-owned, suppressed;
- Merchant `@merchant_2e_1e` — admitted on a concrete authored path;
- Merchant `@merchant_favore_done` — task-owned, suppressed;
- Bishop `bishop_2_1a` — admitted.

Therefore schema 5 expects **6 ancestor-owner candidates / 2 task-owned exclusions / 4 admitted / 4 supported / 0 unsupported**.

### Exact-self/non-`@` retained evidence

The earlier complete non-`@` audit remains valid:

- 77 unique non-`@` answer IDs;
- exactly 19 exact-self-consuming candidates;
- 0 reversible candidates;
- 0 utility-like `Leave` / `Back` / `Trade` candidates;
- 9 task/completion-owned exclusions;
- 6 admitted independent exact-self topics.

Exact self-consumption is no longer a separate semantic model; it is the first-precedence case of the general lifecycle-owner rule.

### Runtime regression proving ancestor ownership

The accepted 1.1.6 Snake test provides direct player proof:

- `@snake_1с` was reachable with its authored fake-coins gate satisfied;
- Restoration Tools was independently actionable at the same time;
- the wheel showed **2 markers**;
- after a child branch consumed parent `@snake_1с`, the wheel showed **1 marker**;
- after consuming Restoration Tools, the wheel showed **0 markers**.

The 1.1.5 hard-coded Snake rule is retired. Schema 5 derives `@snake_1с` through the common lifecycle compiler.

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

These task-linked rules remain necessary because not every actionable quest interaction is represented by the generic dialogue-lifecycle class.

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

Static persisted-topic evidence confirms these reminder-bearing stage topics are persistent dialogue-lifecycle interactions. Production derives them through the common dialogue-lifecycle path instead of maintaining parallel `VerifiedBridgeReminderRules` / `VerifiedIntermediateReminderRules` manifests.

### Ms. Charm -> Snake counterfeit-coins evidence

The 2026-09-19 state capture remains canonical evidence for ancestor consumption:

- Ms. Charm's `@actress_2b_1a` makes `npc_actress/actress_money` Visible and unlocks Snake `@snake_1с`;
- Snake `@snake_1с` is a top-level item-gated selectable entry requiring `Item:quest_fake_coins = 1`;
- the parent does not consume itself;
- both authored children `snake_1с_4a` and `snake_1с_4b` persistently blacklist parent `@snake_1с`;
- both branches unlock `@actress_snake_back`.

In 1.1.6 this is **not an exact special rule**. The common path-local lifecycle compiler resolves `@snake_1с` as the nearest consumed selectable ancestor and attaches the owner's authored gate/navigation predicates.

## Authoritative zone-quality mirrors

GK 1.407 graphs can mirror live `WorldZone.GetTotalQuality()` into a player `GameRes` through an authored `Flow_SetPlayerParam <- Flow_GetQualityOfZone` value edge. The Snake `sacrifice_quality` case proved that the stored player parameter may be stale before the real dialogue branch refreshes it.

Production may derive an authoritative mirror only when that exact graph edge is unambiguous. For such an owner-local requirement it compares the live `WorldZone.GetTotalQuality()` against the authored requirement value instead of trusting the stale mirrored player parameter. Ambiguous or unresolved mirrors fail closed.

Cross-owner and generic dialogue-lifecycle routes retain their accepted SmartRes/`Player.IsEnough` semantics unless separate evidence establishes that authoritative zone substitution is required there.

## Accepted persistent loading/performance contract

Accepted runtime architecture as of **1.1.6**:

- per frame: timer comparison only until the one-second refresh is due;
- one schema-5 manifest path: `BepInEx/cache/DayWheelQuestMarkers/rules-1.407.bin`;
- schema 5 stores owner/cross task rules, unified dialogue-lifecycle topics, compact navigation predicates, and lifecycle census integrity counts;
- `UnifiedDialogueLifecycleCompiler` exists only for loading/bootstrap derivation;
- the legacy 1.0.35 `non-at-self-consuming-1.407.bin` is ignored;
- when schema 5 is missing/incompatible, graph parsing occurs only during the verified loading window;
- cached loads deserialize compact data and recreate only live runtime bindings; normal gameplay does not invoke the graph parser;
- once per second: evaluate cached task/interaction state, phrase state, navigation predicates, game-owned gate predicates, and live HUD semantics;
- approximately every 30 seconds: perform allocation-light runtime/known-NPC validation through cached references and a `ulong` fingerprint;
- real known-NPC membership changes use cheap rebinding from the manifest;
- no background worker and no save mutation.

Accepted 1.1.6 runtime evidence:

- schema-4 -> schema-5 rebuild behind loading: **1005.16 ms**;
- canonical summary: owner 75/6, cross-owner 8/6/0, dialogue-lifecycle 65/64/1, non-`@` 77 / 19 / 6, ancestor owners 6 / 2 / 4 / 4 / 0, navigation 210/270/151/0;
- subsequent same-process save reload: schema-5 manifest read in **3.89 ms** with `FlowCanvas graph parse skipped`;
- final live-object transition invalidated the prewarmed binding once, causing a guarded **3.21 ms** manifest re-read with no graph parse before `Ready`;
- the fallback is bounded to the load/runtime ownership transition and is not recurring gameplay work;
- the Snake two-interaction regression passed **2 -> 1 -> 0**.

Historical performance lineage:

- 1.0.25 exposed a measured **302.22 ms** first-weekday-NPC runtime structural rebuild;
- 1.0.26 moved structural parsing behind loading and persisted it;
- 1.0.27 moved the cache under BepInEx;
- 1.0.28 removed recurring steady-state allocations and the previous roughly 30-second rhythmic freeze pattern disappeared in player testing;
- 1.0.30 added persisted navigation reachability;
- 1.0.32 added narrow verified completion-route predicates;
- 1.0.35 added the audited non-`@` exact-self class;
- 1.1.3 unified exact-self persistence and independent-path deduplication;
- 1.1.6 generalized exact-self ownership to nearest persistent lifecycle ownership without adding gameplay graph traversal.

The rejected universal provenance-parser experiment pushed loading work toward roughly 1.8 seconds and is not an accepted architecture. Do not reintroduce arbitrary external dependency/provenance traversal into production.

## Accepted architecture consolidation in 1.1.6

The accepted architecture is now substantially more general than the historical implementation.

### What is unified

- persisted `@` and non-`@` one-time dialogue use one dialogue-lifecycle representation;
- exact-self and child-consumed-parent cases use one **nearest persistent lifecycle owner** rule;
- navigation ancestry, phrase state, and supported AnswerData/SmartRes gates are shared predicates;
- task-owned and same-visit deduplication are part of compilation rather than tactical runtime exceptions;
- the Snake counterfeit-coins hard-code from 1.1.5 is removed;
- one schema-5 manifest persists the resulting compact runtime model.

### What intentionally remains separate

1. **Task ownership.** A visible journal task with an authored weekday-NPC route is a different source of evidence from dialogue lifetime. Owner-local and cross-owner are provenance variants inside this class.
2. **Verified completion supplement.** Five promoted task routes already reuse normal topic/navigation predicates; `snake_trap` remains an exact verified relation-gated completion topology.
3. **Mandatory event-only stages.** `npc_inquisitor/inquisitor_talk` and `npc_cultist/snake_back` have no selectable answer representing the required visit and therefore cannot honestly be forced into the dialogue-lifecycle rule.

A future maintainability refactor may build one shared parsed `WeekdayGraphIndex` so the task, navigation, and lifecycle derivation passes stop duplicating bootstrap parsing/indexing. That would be a code-organization improvement, not a new gameplay algorithm, and should only be attempted with static parity guards.

Likewise, the remaining promoted completion routes may be audited for derivation by a bounded task-effect compiler. Do **not** replace the current small verified supplement with the previously rejected universal provenance parser merely to claim a single algorithm.

The correct target is therefore **one compact interaction engine with a small number of evidence-backed derivation passes**, not one artificial predicate pretending that Graveyard Keeper authors every required visit through the same mechanism.


## Accepted interaction-universe coverage evidence

Research snapshots 0.1.2 and 0.1.3 establish the bounded completeness contract for the six weekday-NPC authored graphs used by Day Wheel Quest Markers on GK 1.407.

Accepted raw universe:

- **243** authored `Flow_MultiAnswer` answer occurrences;
- **224** unique `NPC + answer ID` pairs;
- **150** task-state transitions;
- **66** `CustomEvent` nodes;
- **19** `Flow_AddInteractionEvent` nodes;
- **4** `Flow_RemoveInteractionEvent` nodes;
- **72** owner-local completion routes = **70 selectable + 2 event-only**;
- **270** production-derived navigation paths.

Of the 224 unique answer IDs, **210** have a normal interaction-root navigation path. Read-only frontier probe 0.1.3 proved that the remaining **14** are entered from `CustomEvent` / scripted-event topology rather than a fresh player-initiated weekday-NPC interaction. Their accepted classification is:

`EVENT_INVOKED_NON_REMINDER`

The four verified event-root families are:

- Inquisitor node 95 / `first_meet_under_mountains`: 7 answers;
- Inquisitor node 343 / `on_came_to_mountain_for_witch_burning`: 2 answers;
- Inquisitor node 1711 / `inquisitor_after_dark_event`: 3 answers;
- Snake node 318 / `player_back_to_cultist`: 2 answers.

Therefore the accepted six-NPC graph universe has **UNKNOWN = 0**.

This is a bounded completeness claim for the graph/node families relevant to this mod, not a claim that every Graveyard Keeper world interaction for every NPC is represented by `Flow_MultiAnswer`. New production generalization must preserve this boundary or update it with new accepted evidence; unexplained raw answers must fail the coverage watchdog rather than being silently ignored.

The 0.1.3 topology capture is research-only and does not change production Day Wheel Quest Markers 1.1.6.

# Nested dialogue reachability audit

Date: 2026-09-12

Scope: Graveyard Keeper 1.407, all six weekday-NPC graphs, and all three current Day Wheel Quest Markers reminder layers (owner-local tasks, cross-owner tasks, self-consuming persisted `@` topics).

This is a research-only audit. It does not change runtime code, version, manifest schema, or the accepted stable baseline.

## Question

The 1.0.29 Charmel fix proved a specific false-positive shape:

`root menu -> gated parent entry -> nested reminder-bearing child`

The production classifier evaluated the child phrase and the child's own `AnswerData`, but did not require the parent entry to be reachable. The audit asks whether this is only a Charmel special case or a general defect in the reminder model.

## Current production limitation

`WeekdayInteractionRuleCache` stores actionability at the final answer only:

- owner/cross task variants store the matched completion answer and its own price/lock requirements;
- generic one-shot topics store the self-consuming answer and its own price/lock requirements;
- `IsTopicActionable` / `AnyVariantActionable` check the final answer phrase plus those final-answer requirements.

The cache does not retain the authored navigation path from the NPC's root interaction menu to a nested `Flow_MultiAnswer` containing that final answer. Therefore an otherwise open child can be misclassified as actionable when an ancestor menu entry is unavailable.

This is a model defect across all reminder layers, not a one-shot-only defect.

## Evidence universe reviewed

Existing accepted/read-only evidence was reused instead of creating another runtime probe:

- `DayWheelQuestMarkers-persisted-topic-audit-0.1.25.txt`: all authored persisted `@` entries on the six weekday graphs, direct branch effects and own AnswerData gates;
- `DayWheelQuestMarkers-strict-action-chain-probe-0.1.24.txt`: direct flow plus exact CustomFunctionCall UID jumps, useful for menu topology without the rejected broad traversal behavior;
- `DayWheelQuestMarkers-topic-provenance-universe-audit-0.1.27.txt`: 152 authored `@` occurrences / 141 unique IDs over all six weekday NPCs and 20 relevant source graphs;
- `CalendarQuestsPins-runtime-probe-0.1.7/0.1.8/0.1.13`: serialized graph topology and exact task completion / SmartRes evidence;
- user runtime log `лог 2 меток Шармель.txt`: actual unpickable parent at Charmel.

The previous universal action-chain/provenance experiments remain rejected as production architecture. This audit uses their static evidence only and does not propose recurring arbitrary graph traversal.

## Confirmed affected paths

### 1. Charmel / Actress: `actress_2b -> @actress_2b_1a / @actress_2b_1b`

Confirmed player-visible false positive.

Runtime shows `actress_2b` ("I have questions") rendered but `_can_be_picked = False`. Static graph evidence shows its nested menu contains exactly two self-consuming persisted topics:

- `@actress_2b_1a`: no own AnswerData; makes `actress_money` Visible, activates `@snake_1с`, blacklists itself and `actress_2b`;
- `@actress_2b_1b`: no own AnswerData; blacklists itself and `actress_2b`.

Because the current classifier sees both child topics as open/pickable in isolation, both can emit markers even though the parent cannot be entered.

1.0.29's `VerifiedNestedDialogueGate` is therefore a correct tactical fix for this exact state, but not a complete model fix.

### 2. Merchant business submenu: `@merchant_business -> @merchant_marketing_done / @merchant_sales_done`

Confirmed same-class latent false-positive path.

`@merchant_business` is a root-menu entry that opens the business submenu. Inside that submenu:

- `@merchant_marketing_done` has its own `storage_quality >= 3` lock, completes `merchant_marketing`, and blacklists both itself and parent `@merchant_business`;
- `@merchant_sales_done` has its own `crates_sold_total >= 7` lock, completes `merchant_sales`, and blacklists both itself and parent `@merchant_business`.

Current owner-task actionability evaluates the child completion answer and its own resource gate, but not `@merchant_business` phrase reachability. Once one authored branch closes the parent, another child can in principle remain individually open/satisfied but no longer be reachable through dialogue. The current model can therefore emit a phantom Merchant marker.

This proves the defect is not limited to generic one-shot topics.

### 3. Merchant debt submenu: `@merchant_2e -> @merchant_2e_1f`

Confirmed parent phrase dependency in a task-linked route.

`@merchant_2e` is a root persisted submenu entry. It opens a menu containing `@merchant_2e_1e` and `@merchant_2e_1f`. A deeper plain choice (`merchant_2e_1d_4a`) makes `merchant_debt` Visible and activates `@merchant_2e_1f`. The final `@merchant_2e_1f` branch:

- costs 10 money;
- completes `merchant_debt`;
- activates `@merchant_on_deal_done_4a`;
- blacklists itself and parent `@merchant_2e`.

At the completion stage the required navigation chain is root -> `@merchant_2e` -> `@merchant_2e_1f`. The current owner-task rule stores only the final answer and therefore omits the parent phrase predicate.

`@merchant_2e_1e` is not a required ancestor at the final completion stage: it is the setup submenu used to expose `@merchant_2e_1f`, after which the completion answer is present in the parent `multi=190` menu.

## Structurally nested paths that are not currently proven bugs

### Bishop cathedral submenu

The main-menu entry `about_cathedral` opens the cathedral submenu and has no AnswerData or persisted phrase gate. Nested entries include `@bishop_cathidral`, `@bishop_get_citezen`, `@bishop_town_cathidral`, and `@bishop_statue_ready`.

Because `about_cathedral` is an unconditional plain entry, it adds no runtime predicate. This is an important control: nested structure alone is not a problem. Only ancestors whose own phrase/gate state can make the path unavailable need to be persisted/evaluated.

### Snake / Cultist necklace and ritual submenus

Root persisted headers include:

- `@snake_about_nacklase` -> `@snake_nacklase_0`, `@snake_nacklase_again`, Leave;
- `@snake_ritual_help` -> `@snake_help`, `@snake_help_done`, Leave.

Both parents have no own SmartRes gate. Existing source/lifecycle evidence shows the parent phrase and relevant child phrase are authored to unlock together in the known progression transitions; no branch was found that independently blacklists these parent headers while leaving a reminder-bearing child intentionally active.

No current false-positive state is therefore proven here. Nevertheless a general path representation should retain the parent phrase-open predicate, because it is a real authored navigation dependency and costs almost nothing to evaluate once cached.

### Inquisitor

Current reminder-bearing persisted topics are on the main `multi=736` / `multi=843` menus. Nested inspected menus contain plain branching/lore choices and Back navigation, but this audit found no current reminder-bearing completion/one-shot route whose actionability depends on a gated nested parent.

No current defect is identified for Inquisitor.

### Astrologer

The authored universe contains nested reminder-shaped topic `@astrologer_2a_1a_1` in `multi=67`. It self-consumes, activates `@astrologer_trade`, and blacklists plain parent `astrologer_2a`. Existing evidence proves the nesting and parent closure, but this audit does not have sufficient accepted evidence to assert a non-trivial independent gate on `astrologer_2a`, nor a runtime lifecycle where the child remains available while that parent is unavailable.

Therefore this is a required parent-chain representation case, but not yet classified as a confirmed player-visible false positive.

The `multi=507` `@astrologer_about_acid` / `@astrologer_about_tools` group is also nested. Existing evidence is sufficient for the child branches but not for a fully verified ancestor chain, so a production extractor must resolve it from the serialized graph and fail closed if ancestry is ambiguous.

## General actionability rule

The correct rule is not:

`final answer phrase open + final answer own gates satisfied`

It is:

`there exists at least one authored root-to-final-answer navigation path for which every required entry is currently reachable`

For one path, conditions are AND:

1. every persisted `@` ancestor entry is unlocked and not blacklisted;
2. every plain ancestor entry is not blacklisted if the game persists/consults that exact ID in the blacklist;
3. every ancestor AnswerData price/lock that controls entering the next menu passes the same game-owned `Player.IsEnough(SmartRes)` evaluation used for final answers;
4. the final answer phrase is open;
5. the final answer's own supported price/lock gates pass;
6. an unsupported or ambiguous required ancestor makes that path fail closed.

If multiple authored root-to-target paths exist, the paths are OR: one fully supported/reachable path is sufficient.

Unconditional plain ancestors with no phrase-state dependency and no AnswerData add no runtime condition and can be discarded during loading/bootstrap.

## Recommended implementation architecture

Do not add a gameplay graph walker.

The graph parser already runs only on a persistent-manifest bootstrap miss behind the loading screen. Extend that bootstrap parser to derive a compact navigation-path contract for each production `RuleVariant`:

- final answer requirement remains as today;
- attach zero or more supported ancestor-entry predicates;
- deduplicate equivalent paths;
- reject ambiguous/unsupported ancestry paths rather than guessing;
- persist the compact paths in the manifest;
- gameplay evaluates only phrase lists plus cached SmartRes predicates.

This keeps normal runtime complexity proportional only to the small number of cached predicates, not graph size.

### Manifest implication

Schema 1 cannot represent ancestor predicates. A general fix should use manifest schema 2 (or an equivalent explicitly versioned format).

On 1.0.30 first load, schema-1 `rules-1.407.bin` should simply fail the schema check during the verified loading window and be regenerated there. No manual cache deletion or migration code is required.

Canonical structural counts should be re-baselined for schema 2 around rule/path predicates, not assumed to remain meaningful solely as the old 75/6, 8/6/0, 55/54/1 totals.

## What not to do

- Do not generalize 1.0.29 by hard-coding more NPC/topic IDs one by one.
- Do not recursively traverse arbitrary graph/provenance chains every second.
- Do not resurrect the rejected universal action-chain parser in gameplay.
- Do not infer parent/child relationships from translated text or identifier naming patterns.
- Do not assume every nested menu needs an extra gate; unconditional parent entries should compile away to no predicate.

## Decision

The Charmel issue is a **general actionability-model defect**. At least two additional Merchant task-linked families demonstrate the same missing parent-reachability condition. 1.0.29 is therefore tactical evidence/fallback only and should not become the final architectural fix.

Recommended next runtime version: **1.0.30**, replacing `VerifiedNestedDialogueGate` with a generic, loading-derived, persisted root-to-answer reachability contract. Before handing a DLL, static bootstrap validation must prove the derived paths for the confirmed Charmel and Merchant cases and preserve the known-safe Bishop/Snake/Inquisitor controls.

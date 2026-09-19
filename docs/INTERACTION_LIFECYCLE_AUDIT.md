# Interaction lifecycle audit — parent-consuming dialogue entries

Status: **research conclusion / no runtime change**.

Baseline:
- accepted stable runtime: **1.1.3**;
- tested but unpromoted candidate: **1.1.5**, exact build source `923fa06bcbce43c2498f39d8eaa27c417aa2c7c6`;
- 1.1.5 player validation passed on 2026-09-19 with the required Snake count transition **2 -> 1 -> 0**.

## Question

The 1.1.5 counterfeit-coins fix is deliberately narrow. This audit asks whether the bug exposes a missing **general dialogue-authoring rule**, rather than a one-off exception.

The existing accepted generic dialogue rule is:

`reachable selected answer X -> branch persistently blacklists X -> X is one one-visit interaction`

That exact-self rule is proven and useful, but the Snake case proves it is not the whole authored lifecycle.

## What the game data actually shows

For the six weekday NPC graphs, the existing persisted-topic audit contains **16 menu-boundary entries that do not consume themselves**.

| NPC | Parent entry | Direct shape | Audit interpretation |
| --- | --- | --- | --- |
| Astrologer | `@astrologer_diary` | task + gated submenu | task-owned interaction; not a generic parent-consumption candidate |
| Astrologer | `@tr_quest_13_research_1` | plain submenu | navigation/container |
| Astrologer | `@astrologer_2a_1b_1` | plain submenu | navigation/container |
| Inquisitor | `@inquisitor_dark` | plain submenu | navigation/container |
| Inquisitor | `@inquisitor_magic_100` | relation-gated submenu | navigation/container |
| Snake | `@snake_1с` | item-gated submenu | **verified child-consumed parent** |
| Snake | `@snake_about_nacklase` | plain submenu | navigation/container; child topics own their interactions |
| Snake | `@snake_ritual_help` | plain submenu | navigation/container; child topics own their interactions |
| Snake | `@snake_magic_100` | relation-gated submenu | navigation/container |
| Merchant | `@merchant_2b` | task + submenu | **child-consumed parent, but already task-owned** |
| Merchant | `@merchant_2e` | plain submenu | container; self-consuming child routes own progression |
| Merchant | `@merchant_business` | plain submenu | container; completion children self-consume and also close the parent |
| Merchant | `@merchant_magic_100` | relation-gated submenu | navigation/container |
| Merchant | `@merchant_2e_1e` | plain nested submenu | **second non-task child-consumed parent candidate** |
| Bishop | `@bishop_5_1` | item-gated task + activate + submenu | task-owned interaction |
| Bishop | `@bishop_magic_100` | relation-gated submenu | navigation/container |

This census is intentionally structural. A menu boundary by itself is **not** reminder evidence.

## Verified child-consumed-parent family

### Snake counterfeit coins

`@snake_1с` is a top-level item-gated entry. It does not blacklist itself when entered.

Inside its submenu:
- `snake_1с_4a` activates `@actress_snake_back` and blacklists `@snake_1с`;
- `snake_1с_4b` does the same.

The player's 2026-09-19 1.1.5 test confirms the authored lifecycle at runtime: the parent exists before the choice, disappears after either completing branch, while the independent Restoration Tools interaction remains.

Therefore the persistent one-visit identity is the **parent** `@snake_1с`, even though persistence is mutated by a child answer.

### Merchant nested debt control

The static strict-chain evidence contains a second structurally important **candidate**:

- `@merchant_2e_1e` is a submenu entry with no direct task mutation, activation, or blacklist;
- child `merchant_2e_1d_4a` makes `npc_merchant/merchant_debt` Visible, activates `@merchant_2e_1f`, and blacklists `@merchant_2e_1e`;
- sibling `merchant_2e_1d_4b` does not consume that phrase.

However, an older runtime dialogue log also proves that the same `merchant_2e_1d_4a` answer is reachable through the ordinary `@merchant_2e -> merchant_2e_1d` “ask about seeds” route. Therefore the blacklist effect alone is **not enough** to claim that `@merchant_2e_1e` owns that interaction.

This is an important falsification control: ancestor consumption must be evaluated **per concrete authored root-to-answer path**. A blacklisted phrase counts as lifecycle owner only when that selectable phrase is actually an ancestor on the same path that reaches the progressing answer. The existing navigation cache already preserves ordered answer ancestors, so the production model has the right raw information to enforce this.

The Merchant case therefore supports the need for path-sensitive ownership, but remains **unadmitted** until the complete lifecycle census proves a real `@merchant_2e_1e -> ... -> merchant_2e_1d_4a` path.

### Merchant `@merchant_2b` control

Deep child answers below `@merchant_2b` also blacklist that parent.

However `@merchant_2b` itself is already a task-bearing route: it completes the cross-owner `horadric_garden` task. A generic parent-consumption rule must therefore **not** add a second marker for the same visit.

This is a useful positive control for the existing task-owned deduplication principle.

### Merchant business control

`@merchant_marketing_done` and `@merchant_sales_done` each:
- perform their own task completion;
- blacklist themselves;
- also blacklist the container `@merchant_business`.

These must remain interactions owned by the **self-consuming child**, not become an extra `@merchant_business` reminder.

This gives the ordering rule: when the selected progressing answer consumes itself, **self wins** over any consumed ancestor.

## Revised semantic model

The exact-self rule is a special case of a broader concept:

> **An authored dialogue interaction is owned by the nearest selectable entry on the active answer path whose persistent lifetime is consumed by the progressing branch.**

For a selected/progressing answer `A`:

1. If the branch persistently blacklists `A`, lifecycle owner = `A`.
2. Otherwise, inspect selectable ancestors on that same authored root-to-`A` path from nearest to farthest.
3. The first ancestor persistently blacklisted by the progressing branch is the lifecycle owner.
4. If no self/ancestor is persistently consumed, this mechanism emits no generic interaction.
5. A branch that only backs out/reactivates the parent does not become a reminder source.
6. If several alternative progressing children resolve to the same lifecycle owner, they compile as variants of **one** interaction.
7. If the lifecycle owner/visit is already owned by a task-completion rule, generic dialogue ownership is suppressed to prevent duplicate markers.
8. Existing phrase, blacklist, AnswerData/SmartRes, and root-navigation predicates still decide whether the lifecycle owner is currently reachable/actionable.
9. Blacklist removals/reversible persistence remain fail-closed unless separately verified.
10. Display text is irrelevant.

A useful short name is **nearest persistent lifecycle owner**.

## Why this is more general without becoming the rejected universal parser

This does **not** require solving arbitrary quest provenance across the whole game.

It uses only local facts already parsed from the same six weekday-NPC dialogue graphs:
- MultiAnswer parent/child topology;
- root-to-answer navigation;
- exact blacklist add/remove effects;
- task-state effects;
- phrase state;
- authored AnswerData gates.

The old rejected universal provenance experiment tried to follow arbitrary external dependency chains and became both expensive and semantically noisy. The lifecycle-owner rule stays local to an authored NPC interaction and asks a much narrower question: **which selectable entry does this branch persistently consume?**

## Relationship to task rules

The resulting model is much smaller conceptually than the historical implementation.

There are three evidence classes:

1. **Task-owned selectable interaction**
   - a current Visible task has an authored route through the weekday NPC;
   - that route is currently reachable and all supported gates pass.

2. **Dialogue-lifecycle interaction**
   - a currently reachable progressing branch persistently consumes itself or its nearest selectable ancestor;
   - the lifecycle owner is not already owned by the task layer;
   - current phrase/navigation/resource predicates pass.

3. **Verified mandatory event stage**
   - no selectable answer represents the interaction;
   - currently limited to the already verified exact event-only cases `npc_inquisitor/inquisitor_talk` and `npc_cultist/snake_back`.

Owner-local vs cross-owner is provenance data inside class 1, not a separate player-facing concept.

`@` vs non-`@` is identifier/phrase-state data inside class 2, not a semantic class boundary.

## What this says about Graveyard Keeper's authoring mechanism

Current evidence supports a FlowCanvas composition model rather than a single discovered high-level “this quest interaction is actionable now” flag.

The relevant game behavior is authored from low-level graph primitives: MultiAnswer choices, task-state mutation, phrase activation/blacklisting, SmartRes gates, function/event jumps, and navigation. We should not claim that no higher-level editor abstraction existed for the developers, but the shipped runtime data we can inspect does **not** give the mod one authoritative quest-action record to query.

The right engineering target is therefore not to imitate a hypothetical editor UI. It is to compile those shipped graph semantics into one stable interaction model.

## Architectural consequence

The older `UNIFIED_INTERACTION_ARCHITECTURE_AUDIT.md` recommendation remains correct: bootstrap should eventually parse the six graphs once into a common graph index and run small derivation passes over it.

The new lifecycle evidence sharpens the dialogue pass:

- replace “exact-self-consuming answer” with “nearest persistent lifecycle owner”;
- use the existing navigation ancestry to resolve candidate owners;
- group child variants by owner;
- perform task-owned suppression during compilation;
- persist the resulting interaction, so gameplay performs no graph traversal.

The current separate parsers in `WeekdayInteractionRuleCache`, `NavigationReachabilityCache`, and `UnifiedSelfConsumingCompiler` still duplicate graph indexing. That is maintainability debt, but it is a bootstrap-only concern. The first correctness change should be the lifecycle semantics; the shared `WeekdayGraphIndex` refactor can be done only if static parity proves it does not destabilize accepted behavior.

## 1.1.5 disposition

1.1.5 is a valid, successfully tested repair of the reported Snake false negative and remains frozen exactly as handed out.

It should **not** be silently rewritten or promoted while this audit is open. Because a numbered DLL has already been handed to the player, any generalized runtime replacement is a new version, **1.1.6**.

The 1.1.5 exact Snake hard-code is best treated as a proven executable specification for one instance of the more general lifecycle-owner rule.

## Required proof before 1.1.6

Before production code changes, a bootstrap/static research pass should enumerate the complete six-NPC universe of:

`progressing answer -> persistent self/ancestor blacklist -> resolved lifecycle owner -> task ownership/dedup result`

The output must explicitly show:
- all accepted exact-self interactions remain represented;
- `@snake_1с` resolves to itself through child consumption;
- `@merchant_2e_1e` is classified and reviewed;
- `@merchant_2b` is excluded as a duplicate task-owned visit;
- Merchant business completions remain owned by their self-consuming children;
- ordinary navigation containers and utility Back/Leave/Trade paths remain excluded;
- reversible/ambiguous/unsupported cases fail closed;
- the two mandatory event-only stages remain separately accounted for.

Only after that census passes should the hard-coded Snake special case be removed and a 1.1.6 candidate built.

## Current verdict

The architecture is **not** best modeled as an endless list of unrelated quest exceptions. The concrete weakness is narrower and now identifiable: the generic dialogue classifier currently recognizes only **self-consumption**, while the Snake runtime proves at least one real **path-local ancestor-consumption** lifecycle.

That is now the leading missing general rule. It is not yet promoted as universal until a complete path-sensitive census attempts to falsify it across all six weekday-NPC graphs.

The next step is therefore a bounded static lifecycle census, not another gameplay special case and not a return to the rejected universal provenance parser.

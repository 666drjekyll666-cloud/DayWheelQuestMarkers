# Intermediate Progression Audit — GK 1.407

This document records the static dependency audit used for the 1.0.22 candidate. It is engineering evidence, not user-facing release documentation.

## Product rule

A weekday reminder is valid only when the currently visible objective already requires an interaction with that weekday NPC.

For intermediate stages that are not direct `Flow_SetTaskState(... Complete)` anchors, inclusion requires both:

1. verified provenance from the active objective to the target interaction;
2. verified downstream progression dependency/effect caused by consuming that interaction.

If either side is unproved, the stage fails closed.

Primary evidence used for this audit:

- `DayWheelQuestMarkers-persisted-topic-audit-0.1.25.txt`;
- `DayWheelQuestMarkers-strict-action-chain-probe-0.1.24.txt`;
- the completed 0.1.26-0.1.29 authored-universe research retained in the legacy repository;
- exact GK 1.407 assembly/runtime facts already recorded in `docs/VERIFIED_RUNTIME_DATA.md`;
- legacy research commit `8cfd4c517fa18c9425387d855d5932e4c46e164e` for the graph-derived zone-quality mirror resolver.

## Verified production manifest

### Astrologer daughter -> Ms. Charm

- Active objective: `npc_astrologer|astrologer_daghter` (`daghter` is the authored spelling).
- Required target: `npc_actress`.
- Required topic: `@actress_father`.
- Authored gate: `GameRes:_rel >= 50`.
- Consuming the topic opens the Astrologer continuation `@astrologer_charm_no`, which completes the objective.

### Bishop invitation -> Merchant

- Active objective: `npc_bishop|bishop_invitation_2`.
- Required target: `npc_merchant`.
- Required topic: `@merchant_ceremony`.
- Authored gate: `GameRes:_rel >= 90`.
- Consuming the Merchant interaction opens the Bishop continuation `@bishop_invitation`, whose authored item gate leads to objective completion.

### Inquisitor guards -> portal guard stage

- Active objective: `npc_inquisitor|inquisitor_guards`.
- Required target: `npc_inquisitor`.
- Required intermediate topic: `@inquisitor_portal_guard`.
- No SmartRes gate on the intermediate topic.
- Consuming it opens `@inquisitor_portal_done`; the continuation has `_rel >= 60` and completes `inquisitor_guards`.

### Merchant support -> Ms. Charm

- Active objective: `npc_merchant|merchant_support`.
- Required target: `npc_actress`.
- Stage 1: `@actress_marketing`, authored gate `_rel >= 40`.
- Stage 2: exact authored topic `@actress_ jewelry` (contains a literal space), authored price `Item:bijouterie_gold >= 1`.
- The jewelry stage opens `@merchant_actress`; the Merchant continuation completes `merchant_support`.

### Snake help entry

- Active objective: `npc_cultist|snake_help`.
- Required target: `npc_cultist`.
- Required intermediate topic: `@snake_help`.
- Consuming it makes `snake_stars` visible and opens `@snake_help_done`.
- `@snake_help_done` is the normal completion-anchor stage for `snake_stars`; its authored lock is `GameRes:sacrifice_quality >= 20`.

### Ms. Charm necklace -> Snake

- Active objective: `npc_actress|actress_necklace`.
- Required target: `npc_cultist`.
- Verified Snake progression states:
  - `@snake_nacklase_0`;
  - `@snake_nacklase_again`, gate `_rel >= 30`;
  - `@snake_nacklase_again_10a`;
  - `@snake_nacklase_again_10b`;
  - `@snake_nacklase_again_10c`.
- The final Snake state opens `@actress_money_back`; the Ms. Charm continuation completes `actress_necklace`.
- These states form one manifest family and therefore contribute at most one Snake marker.

## Rejected / unresolved candidates

### `@inquisitor_tent`

Rejected for the intermediate manifest. The topic itself sets `npc_inquisitor|inquisitor_tent` to `Visible`; it starts that objective rather than being a proven interaction required by an already-active objective. Once the objective is active, the ordinary completion-anchor path handles `@inquisitor_tent_ready`.

### `@snake_give_key`

Rejected. External provenance from the Astrologer chain is known, and the topic has an authored key-item price, but no required downstream dependency from consuming the Snake topic to progression of the active objective has been proved. Fail closed.

### `@astrologer_necronamicon`

Not included in 1.0.22. Provenance to the visible Snake Necronomicon objective and the `_rel >= 40` gate are known; consuming the topic opens `@light_keeper_book`. The preserved evidence does not fully prove the downstream Lighthouse Keeper dependency required to complete/progress that objective. This remains a static-audit follow-up, not a runtime heuristic.

### `@astrologer_items`

Rejected. The topic is relation-gated and self-consuming, but the audit found no task mutation, required continuation unlock, progression item transfer, technology/craft unlock, or other proved downstream dependency. The reason for exclusion is lack of required progression effect, not the presence of a relationship gate.

## Authoritative zone-quality mirrors

Some authored `GameRes` parameters are cached mirrors rather than authoritative live values. Static graph evidence proves the pattern:

`Flow_GetQualityOfZone(zone)` -> `Flow_SetPlayerParam(parameter)`.

For the six weekday-NPC graphs, the relevant unambiguous mappings are:

- `graveyard_quality -> graveyard`;
- `church_quality -> church`;
- `storage_quality -> storage`;
- `sacrifice_quality -> sacrifice`.

The 1.0.22 implementation derives these relationships from the existing bounded graph parse instead of hard-coding individual parameter names. An ambiguous parameter-to-zone mapping is removed and fails closed. When an authored `GameRes` requirement maps to a verified zone mirror, evaluation uses `WorldZone.GetZoneByID(...).GetTotalQuality()`; all other requirements continue through authored `SmartRes` plus `Player.IsEnough`.

This directly fixes the known `snake_stars -> @snake_help_done` case where the Dark Church UI can already show quality 20 while cached `sacrifice_quality` is still stale.

## Runtime cost

The intermediate manifest does not traverse quest graphs during gameplay. Every normal refresh checks only:

- six fixed manifest families;
- current cached visible task state;
- current phrase unlock/blacklist state;
- native `Player.IsEnough` for the few authored gates whose topic is currently open.

The zone-mirror derivation runs only as part of the existing bounded graph parse/prewarm. No background worker, broad recurring traversal, hierarchy scan, or new per-frame work is introduced.

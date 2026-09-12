# 1.0.26 Persistent Rule Manifest

## Problem established by runtime evidence

Fresh-game testing of 1.0.25 proved two separate facts:

1. cheap rebinding works: after the structural cache exists, newly added `known_npc` entries can be rebound without reparsing the six weekday FlowCanvas graphs;
2. the first weekday NPC still caused a visible synchronous structural build because the intended loading prewarm window in 1.0.25 used the wrong `game_starting` polarity. The supplied fresh-game log measured `302.22 ms` at the first Bishop introduction.

The accepted 1.0.12/1.0.24 readiness evidence places the graph-ready pre-game window at `game_started == false && game_starting == false`, after the real save/player/world objects and all six serialized weekday graphs are present.

## 1.0.26 architecture

1.0.26 separates expensive structural discovery from normal gameplay.

### Persistent data

`PersistentRuleManifest` stores only compact structural rule data for Graveyard Keeper 1.407:

- weekday target NPC ID;
- owner-local task ID and answer variants;
- cross-owner task owner/task IDs and answer variants;
- self-consuming one-shot answer IDs and variants;
- supported price/lock requirement type, ID and value;
- authoritative zone ID where the accepted owner-quality rule needs it;
- canonical rule counts used as an integrity guard.

It does **not** serialize `GameSave`, `Player`, `KnownNpc`, `WorldGameObject`, `SmartRes`, Unity objects, translated text, or copied game sprite pixels.

The manifest file is `DayWheelQuestMarkers.rules.1.407.bin` beside the plugin DLL. Its schema and game version are checked before use.

### Bootstrap

When the manifest is missing or invalid, the existing verified 1.0.24 parser is used only as a bootstrap generator in the established loading-screen window. Bootstrap creates placeholder known-NPC ownership entries so the complete six-graph structural universe can be generated even for a brand-new save before the player has met the weekday NPCs.

A bootstrap result is accepted/persisted only if it matches the accepted 1.0.24 canonical structural counts:

- owner supported: 75;
- owner unsupported: 6;
- cross-owner tasks: 8;
- cross-owner supported: 6;
- cross-owner unsupported: 0;
- one-shot topics: 55;
- one-shot supported: 54;
- one-shot unsupported: 1.

If the counts differ, bootstrap fails closed and does not persist a potentially misleading manifest.

### Normal launches and gameplay

When the manifest exists:

- loading deserializes the compact data and recreates only game-owned `SmartRes` predicates and live target WGO references;
- the current save/player/known-NPC objects are rebound;
- later `known_npc` changes only refresh those references;
- the one-second gameplay path continues to evaluate visible task state, phrase blacklist/unlock state, `Player.IsEnough`, and accepted live zone-quality checks;
- no FlowCanvas graph traversal/parser is permitted in gameplay;
- if a manifest cannot be loaded during gameplay, reminders fail closed for that save/session rather than paying a synchronous graph-parse hitch.

The 30-second low-frequency signature check remains only to catch rare same-count known-NPC replacement/recreation; it performs rebind, not graph parsing.

## Native marker sprites

The project contract continues to prohibit embedding copied Graveyard Keeper pixel payloads. 1.0.26 therefore keeps the existing game-owned Sprite lookup/cache model. The broad loaded-Sprite lookup is bounded to the verified loading/prewarm path whenever possible, and resolved game-owned Sprite references are retained rather than rescanned every refresh.

## Expected runtime evidence

First 1.0.26 launch without a manifest should log a loading-screen bootstrap and create `DayWheelQuestMarkers.rules.1.407.bin`. The first Bishop/weekday introduction must not log a runtime structural rebuild.

Subsequent launches should log that the persistent manifest was loaded behind the loading screen and that FlowCanvas graph parsing was skipped. New known NPCs should log only a cheap persistent-manifest rebind.

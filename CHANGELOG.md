# Changelog

Only player-accepted builds are stable releases. Candidates remain explicitly marked until accepted.

## 1.0.21 — public migration candidate

- Preserves the accepted 1.0.17 quest/actionability logic and marker layout.
- Replaces embedded copies of Graveyard Keeper quest-marker pixels with bounded runtime resolution of the game's own loaded marker sprites.
- Moves the normal native-sprite lookup into the existing loading-screen prewarm window on developed saves.
- Allows one bounded fallback lookup only when a real marker needs a sprite that was not resident during prewarm.
- No intended changes to reminder eligibility, weekday mapping, task categories, marker position, same-day layout, cache cadence, or save behavior.

## 1.0.17 — accepted stable legacy baseline

- Added the two verified rare objective-bridge reminder families without restoring the rejected universal provenance parser.
- Preserved the accepted 1.0.12 loading-screen cache-prewarm architecture.
- Astrologer mill bridge was player-tested end to end; the structurally equivalent Snake instrument bridge uses the same narrow verified rule shape.

## 1.0.12 — accepted performance baseline

- Moved heavy developed-save FlowCanvas cache parsing into the verified loading window before gameplay.
- Preserved smooth fresh-new-game startup and normal marker behavior.

## 1.0.4 — accepted functional baseline

- Separate native-category marker for each actionable objective.
- Symmetric same-day marker layout within the weekday sector.
- Stable menu/HUD lifecycle and semantic weekday-slot tracking.

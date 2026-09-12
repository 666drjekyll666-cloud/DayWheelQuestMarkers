# Changelog

## 1.0.24

- Consolidates weekday-NPC quest and dialogue discovery into one unified loading-time cache.
- Parses each of the six weekday-NPC dialogue graphs once instead of reparsing them through separate owner, cross-owner, and one-shot caches.
- Removes the transitional hard-coded bridge/intermediate reminder manifests; those verified one-time interactions now use the same authored self-consuming dialogue rule as other one-shot reminders.
- Preserves owner-local and cross-owner task handling, supported resource/relation gates, native marker categories, and authoritative live zone-quality handling where previously verified.
- Reduces measured developed-save cache prewarm from 782.89 ms in 1.0.23 to 308.07 ms in the accepted regression test, with no recurring graph parsing during normal gameplay.

## 1.0.23

- Broadens reminders from only task/progression-proven interactions to any currently actionable authored one-time dialogue with a weekday NPC.
- Detects one-shot dialogue structurally through the game's exact self-blacklist behavior rather than translated/display text.
- Keeps repeatable utility/menu choices such as Trade, Leave, Back, and non-consuming submenu headers out of the reminder set.
- Preserves existing owner-local, cross-owner, bridge, and verified intermediate quest reminder handling.
- Uses the game's own gate checks for supported item/resource/relation requirements and fails closed on unsupported structures.
- Keeps graph discovery in the loading-screen prewarm; normal gameplay continues to use cached rules and low-frequency refresh work.

## 1.0.21

- Initial public release.
- Adds actionable quest markers to the weekday wheel for the six vanilla weekday NPCs.
- Supports verified owner-local and cross-owner objectives plus the Miller → Astrologer and Astrologer → Snake bridge chains.
- Uses the game's quest-marker categories and colors.
- Supports multiple simultaneous actionable objectives on the same weekday.
- Preserves correct marker placement as the weekday wheel rotates and across normal HUD/menu transitions.
- Keeps normal gameplay overhead low through cached quest structure and bounded refresh work.

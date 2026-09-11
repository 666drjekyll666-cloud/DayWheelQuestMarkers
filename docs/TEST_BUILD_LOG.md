# Test Build Log

Every handed DLL is immutable and tied to exact committed source plus build artifact identity.

## 1.0.17 — accepted legacy stable baseline

- Frozen source: `CalendarQuestsPins-legacy-private`, `frozen/1.0.17`, commit `507fc6dd192993bf6290f7b6735718e3b98430a4`.
- CI run: `34499501221`; artifact `DayWheelQuestMarkers-1.0.17` (`10161273309`).
- Raw DLL: 55,296 bytes.
- SHA-256: `5dbfd4d4542978ebb14d0284f6ab82bad5be1593df9f097243e63af20d6b76d5`.
- Player result: accepted stable. The Astrologer mill bridge was tested end to end; marker persisted across the verified continuation and disappeared when the calculations were produced. Accepted 1.0.12 loading-prewarm behavior remained smooth.

## 1.0.21 — public migration candidate

- Date: 2026-09-11.
- Branch: `dev/1.0.21`.
- Base: accepted 1.0.17 runtime logic, not the unaccepted private 1.0.18–1.0.20 experiments.
- Goal: remove copied marker-pixel payloads from the distributable source while preserving the exact native visual language and accepted reminder behavior.
- Runtime change: replace embedded PNG decoding with cached references to the game's own loaded marker Sprite objects. Normal lookup is attempted during the existing developed-save loading prewarm; at most one runtime fallback lookup is allowed if a required style was not resident then.
- Unchanged by design: owner-local/cross-owner/bridge eligibility, game-owned SmartRes checks, semantic weekday mapping, marker categories, outward position, same-day sector layout, one-second gameplay cadence, ~30-second structural check, save behavior, and graph-cache architecture.
- Candidate source SHA: pending bootstrap commit.
- Candidate CI run/artifact: pending.
- Candidate DLL SHA-256: pending.
- Requested player test:
  1. load a developed save with at least one visible reminder and verify the marker appears with the expected native color/art;
  2. open/close ordinary menus and confirm the marker survives correctly without duplicates;
  3. if practical, observe more than one marker/category already present in the save;
  4. confirm entering gameplay does not reintroduce a noticeable post-load hitch.
- Player result: pending.
- Status: **candidate / not accepted yet**.

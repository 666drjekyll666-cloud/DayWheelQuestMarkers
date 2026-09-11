# Public Migration Provenance

The clean public repository starts a new Git history from the accepted production line rather than importing the legacy development history.

## Accepted legacy baseline

- Legacy private repository: `666drjekyll666-cloud/CalendarQuestsPins-legacy-private`
- Accepted stable version: 1.0.17
- Frozen accepted source: `frozen/1.0.17`
- Exact accepted source commit: `507fc6dd192993bf6290f7b6735718e3b98430a4`
- Accepted CI run: `34499501221`
- Accepted artifact: `DayWheelQuestMarkers-1.0.17` (`10161273309`)
- Accepted raw DLL SHA-256: `5dbfd4d4542978ebb14d0284f6ab82bad5be1593df9f097243e63af20d6b76d5`

## Public migration scope

1.0.21 starts from the accepted 1.0.17 runtime logic. Quest eligibility, bridge rules, cache architecture, weekday semantics, marker layout, and update cadence are preserved.

One production implementation detail is intentionally replaced before public release: 1.0.17 carried exact exported quest-marker PNG payloads. The public line does not import those pixel payloads. Instead it resolves the same native marker Sprite objects from the installed game's loaded runtime resources, normally during the existing loading-screen prewarm window, and caches references to them.

The runtime lookup itself is not new research: the earlier production line used the same exact sprite-name resolution before the embedded-pixel optimization. 1.0.21 bounds that lookup so it does not become steady-state work.

Private 1.0.18–1.0.20 development experiments are not accepted release baselines and are not imported into the public candidate. Their version numbers remain consumed; the clean public candidate therefore advances to 1.0.21.

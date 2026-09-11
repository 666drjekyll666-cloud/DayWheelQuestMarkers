# Public Migration Provenance

The public repository starts a new Git history from the accepted production line rather than importing the legacy development history.

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

The public line does not import the copied marker-pixel payloads used by the private baseline. It resolves the game's marker Sprite objects from loaded runtime resources with bounded lookup/caching instead.

Private 1.0.18–1.0.20 development experiments are not accepted release baselines and are not imported into the public stable line. Their version numbers remain consumed.

## Accepted public baseline

- Stable public version: 1.0.21
- Exact executable/build source: `7638343438dad6cdf522e37595f3fb21b442193a`
- Candidate ref: `candidate/1.0.21`
- Accepted ref: `baseline/1.0.21-accepted`
- CI run: `34639351706`
- Artifact: `DayWheelQuestMarkers-1.0.21` (`10278859840`)
- Raw DLL SHA-256: `b609da9c35cd40ce09259a4c580e371dad15c3889f4e5cf9bdb0190a00e23c9a`
- Player acceptance: 2026-09-11
- Stable distribution: GitHub Release `v1.0.21`, using the exact accepted DLL without rebuilding.

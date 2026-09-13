# Day Wheel Quest Markers 1.0.31 candidate

Date built: 2026-09-13.

Status: **candidate / awaiting player runtime A/B**. This is an allocation-pressure experiment, not an accepted root-cause fix. Do not merge to `main`, create an accepted baseline, or publish a stable release before player confirmation.

## Identity

- Development branch: `dev/1.0.31`.
- Exact executable/build source: `b531097b9073e36bc3afc3d402e75a3aceff307d`.
- Frozen candidate ref: `candidate/1.0.31` at the exact source above.
- CI run: `34730959916`; job `103653535597`; success on `windows-latest`.
- Release build: **0 warnings / 0 errors**.
- Artifact: `DayWheelQuestMarkers-1.0.31`, artifact ID `10308174959`.
- Artifact ZIP SHA-256: `603b24e8810a57d9bce080446fa5dc67062971253d9e3de42af9905a36e26fb6`.
- Raw DLL: `Day Wheel Quest Markers 1.0.31.dll`, 79,360 bytes.
- Raw DLL SHA-256: `a611825502ac5d4cd07ec3904d431e669f1152f1762d6ba4040f6cbf7b1e48bf`.
- The temporary branch push trigger used only to produce the handoff artifact was restored to manual-only immediately afterward; the frozen candidate remains the exact source built by the successful run.

## Diagnostic premise

Cross-project performance diagnostics have confirmed Unity/Mono Boehm GC as the immediate mechanism of the characteristic residual ~0.68–0.75 s stalls, but have **not** established Day Wheel Quest Markers as the owner of the allocation/heap pressure.

1.0.31 deliberately tests a narrower hypothesis: the generic navigation-reachability layer added in 1.0.30 increased recurring allocation churn in the one-second actionability path enough to contribute materially to that GC pressure.

## Runtime change

The accepted 1.0.30 structural model is preserved unchanged: same schema-2 manifest, rule counts, 210 navigation answers / 270 paths / 151 predicates, loading/bootstrap behavior, one-second cadence, phrase semantics, SmartRes semantics, and marker logic.

A new `RuntimeActionabilityEvaluator` evaluates the existing persisted rule/navigation data with lower steady-state allocation pressure:

- ordinary SmartRes gates bind a compiled direct `Player.IsEnough` invoker once per live player/method binding, avoiding nested `MethodInfo.Invoke`, fresh argument arrays, and boxed return values in the normal gate path;
- if the runtime cannot compile that invoker, the fallback preserves the same MethodInfo behavior but reuses a single argument array;
- `List<string>` / `ICollection<string>` phrase-state checks use direct `Contains` instead of enumerating through non-generic `IEnumerable`, avoiding boxed list enumerators;
- the rare authoritative live-zone-quality mirror path deliberately retains the accepted 1.0.30 implementation rather than broadening this experiment.

No FlowCanvas graph traversal, new scan, per-frame logging, save mutation, or manifest-format change was introduced.

## Requested player test

Keep the same modpack/configuration and keep `GK Frame Spike Probe (Diagnostic) 0.4.0` installed. Replace only Day Wheel Quest Markers 1.0.30 with this exact 1.0.31 candidate.

1. Confirm the log reports `Day Wheel Quest Markers 1.0.31` and reaches normal `Loading manifest ready` / `Ready` state with the accepted rule/navigation counts.
2. Confirm ordinary day-wheel markers still look/function normally in the current save.
3. Reproduce the same ordinary gameplay / Witch Hill route long enough to encounter the characteristic residual freeze family. Do not change other mods/configuration for this comparison.
4. Send the runtime log after a characteristic freeze, or after a comparable period if the previous ~0.7 s stalls fail to recur.

Interpretation:

- a material suppression/disappearance of the characteristic GC stalls versus the 1.0.30 baseline supports Day Wheel steady-state allocation pressure as a contributor;
- unchanged ~0.68–0.75 s stalls substantially weaken this Day Wheel path as the relevant owner and the broader cross-mod isolation should continue;
- functional marker regressions reject the candidate regardless of performance.

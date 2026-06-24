# OmenCore v3.8.1 - Patch Release Draft

**Release Date:** TBD  
**Release Status:** Implementation in progress; hardware validation pending  
**Type:** Patch release  
**Base Version:** v3.8.0

---

## Purpose

v3.8.1 is reserved for post-3.8.0 field reliability work. Scope now includes GitHub #141-#145, saved Custom fan-curve selection, GPU overclock persistence clarity, Quick Access safety/customization, and a measured background-resource/responsiveness pass. Detailed evidence, implementation constraints, tests, and hardware acceptance criteria are tracked in [3.8.1-BUG-REPORTS.md](3.8.1-BUG-REPORTS.md).

### Backend: Reduced Redundant WMI Memory Queries

- `WmiBiosMonitor` and `LibreHardwareMonitorImpl` each independently re-queried `Win32_ComputerSystem.TotalPhysicalMemory` via WMI on every monitoring tick — in `WmiBiosMonitor`'s case, twice per tick (once directly, once nested inside the used-memory calculation), every 1-5 seconds, for the life of the process. Total physical RAM cannot change while Windows is running, so this is now memoized after the first successful read in both classes. Net effect: one WMI round-trip per tick instead of up to three, reducing WMI/EC query pressure that this codebase has previously linked to instability (see the EC-contention notes in `FanService.cs`).
- Added missing `ManagementObject.Dispose()` calls in the WMI enumeration loops touched by this fix (`WmiBiosMonitor.GetSystemModel/GetTotalPhysicalMemoryGB/GetUsedMemoryGB`, `LibreHardwareMonitorImpl.GetTotalPhysicalMemoryGB` and its RAM-fallback paths) — minor on their own, but free to fix alongside the caching change since the same lines were already being touched.
- No behavior change to fan/thermal control; verified against the existing `WmiBiosMonitorFallbackTests`/`WmiV2VerificationTests`/`LibreHardwareMonitor`-adjacent suites plus a full test run.
- `WmiBiosMonitor.UpdateReadings()` also queried CPU clock speed (`Win32_Processor`) and SSD temperature (a separate `root\Microsoft\Windows\Storage` WMI namespace connection, which is more expensive than a same-namespace query) on every monitoring tick, unthrottled — unlike the adjacent GPU-telemetry section, which already throttles its expensive reads on battery/static-tray. Both are display/diagnostics-only values with no control-logic dependency, so they are now throttled to once every 2s (CPU clock) and 5s (SSD temp) respectively, matching the existing GPU-telemetry throttling idiom. No behavior or safety change; covered by the existing `WmiBiosMonitorFallbackTests` suite plus a full test run.

### GPU Power: Closed A Battery-Safety Gap In Startup OC Restore

- **Safety fix.** The interactive GPU OC "Apply" and "Test Apply" buttons already blocked an OC *increase* while running on battery power (unstable clocks, rapid drain, sudden throttling), via `ValidateGpuOcPowerSourceForIncrease()`. The startup-restore path (`ApplyGpuOcStartupRestore`, run after boot when a profile was confirmed via Test Apply -> Keep) called `ApplyGpuOcValues` directly and bypassed this check entirely — a confirmed startup OC profile would apply unconditionally even if the laptop booted on battery.
- Fixed by gating the startup-restore path through the same check. Added a `showDialogIfBlocked` parameter so startup restore skips silently with a logged reason instead of popping the "AC Power Recommended" modal dialog during boot (which would be disruptive before the user even sees the main window) — the interactive paths still show the dialog as before.
- Not unit-tested in this pass: exercising this requires a working `NvapiService` instance, which has no interface for mocking and depends on native NVAPI; the fix mirrors the already-tested interactive-path logic exactly, so it is correct by construction, but physical reboot-on-battery validation is still open (see the GPU OC reboot validation item already tracked in `3.8.1-BUG-REPORTS.md`).

### AMD GPU OC: Decoded ADL2 Error Codes For Field Diagnosis

- `AmdGpuService.SetCoreClockOffset/SetMemoryClockOffset/SetPowerLimit` logged only the raw numeric ADL2 return code on failure (e.g. "failed with code -8"), which is meaningless without the ADL SDK headers in hand. Added `DescribeAdlResult()`, mapping the standard documented ADL2 codes (`ADL_ERR_NOT_SUPPORTED`, `ADL_ERR_INVALID_PARAM`, `ADL_ERR_RESOURCE_CONFLICT`, etc.) to readable reasons, so a field log now reads "failed: Not supported by this adapter/driver (code -8)" instead of just the bare number. Pure logging change, no control-flow change. Covered by `AmdGpuServiceTests`.

### Investigated And Found Already-Correct (No Change Needed)

A targeted pass over GPU power (`NvapiService`, `AmdGpuService`) and RGB/keyboard lighting (`KeyboardLightingServiceV2` and its backends) for 3.8.1 turned up several plausible-sounding concerns that did not hold up under direct verification:
- NVAPI "Set" calls (`SetPerformanceStates20`, etc.) appear to discard a return value, but the underlying NvAPIWrapper library signals failure via exceptions for these calls (confirmed by checking that the library's "Get" methods return values while "Set" methods don't, and both are uniformly wrapped in try/catch here) — the existing exception handling is the complete error path, not a gap.
- The RGB service's backend-fallback locking looked asymmetric at first read (one method takes the semaphore internally, another doesn't), but every actual call site was confirmed to hold `_backendOperationLock` exactly once per operation — no missing lock, no double-lock, no race.
- A claimed "lock released during the 2-second test-pattern delay" was the opposite of the actual code: the lock is held for the full `RunTestPatternAsync` duration including the delay, which is correct, deliberate serialization for a rare, explicit diagnostic action.
- Fan-speed WMI calls were checked for the same redundant-query pattern fixed above; the one repeating fan-count query found is `HpWmiBios`'s deliberate 60-second keep-alive heartbeat (the round-trip itself is the point, not the returned value), not a cacheable lookup.

## Planned Scope

### GitHub #145 - OMEN Slim 16 `8D40` Identity And Performance-Profile Persistence

- Added an exact, conservative capability profile for `8D40` / "OMEN Slim Gaming Laptop 16-an0xxx", a new thin-chassis line. WMI V1 fan/profile control is retained (matches the reporter's working family-fallback behavior); direct EC writes, independent curves, MUX switching, keyboard/RGB, and undervolt are all left unclaimed pending hardware confirmation of this new chassis.
- **Root-caused and fixed** the "Performance Profile always resets to Balanced on relaunch" report. The bug was not specific to this model: changing performance mode via the tray menu, the tray's combined quick-profile item, the `Ctrl+Shift+E` hotkey cycle, or the General page's Performance/Balanced/Quiet buttons applied the mode to hardware but never persisted `LastPerformanceModeName`, because all four paths used a UI-sync method that is also shared with the genuinely-automatic power-source-change handler (where not persisting is correct). Added `SystemControlViewModel.SelectModeByNameNoApplyAndSave()` for the deliberate-action paths and left the automatic-sync path untouched.
- Battery Care (Charge Limit) failure on this model is not fixed — it is not gated by model identity at all (the WMI call is attempted unconditionally regardless of capability match), so this is a raw firmware/WMI command question that needs BIOS version and `wmi-command-history.txt` evidence before any change is made.
- Found and fixed the same persistence gap for fan presets reached through the same tray/hotkey/General quick-profile/OMEN-key entry points (`FanControlViewModel.SelectPresetByNameNoApplyAndSave`), so Restore OEM Auto, Max-fan toggles, and quick-profile fan changes also now survive a relaunch.

### Discord Reports - Fan Spikes To ~5000 RPM At Idle Temp, Then Drops Back

- Triaged (not fixed): multiple Victus 16-s0xxx and OMEN 16-xd0010ax users report fans briefly jumping to ~5000 RPM "for no reason" at idle temperature, then dropping back. Suspected mechanism: `FanService.CheckThermalProtection()`'s emergency tier (95C) activates immediately on a single unsmoothed sample by deliberate, already-tested design (no debounce — safety critical), so a transient/glitchy reading can visibly spike fans before the 15s release hysteresis lets them drop back.
- The no-debounce activation was intentionally **not** weakened — that would contradict an existing test that encodes it as required safety behavior and would slow real-emergency response. Instead, added diagnostics: thermal-protection activation and release now record the **individual** cpu/gpu readings (not just the combined max) in `FanService`'s command history, so the next occurrence is diagnosable from an exported `core-control-readiness.txt` instead of requiring a live app-log capture.
- See BUG-3810-005 in [3.8.1-BUG-REPORTS.md](3.8.1-BUG-REPORTS.md) for the full investigation and evidence still needed before any activation-timing change is considered.

### Quick Access Shortcut Safety

- Added a General setting for the middle Quick Access shortcut: **Display Off**, **Lock Windows**, or **Disabled**.
- The popup refreshes this preference each time it opens, so disabling the shortcut removes the accidental display-off target without requiring a restart.

### GitHub #144 - OMEN 17 `8A18` Thermal/Fan Report

- Added an exact, conservative `8A18` / OMEN 17-ck1xxx capability profile: WMI V1 control retained, direct EC and independent curves disabled, physical RPM readback marked unsupported, and max level fixed at the observed 55-level scale.
- Corrected V1 fan-level fallback so `level × 100` is labeled **Estimated**, not measured WMI RPM. Diagnostics now show the source and cannot use the derived value as independent physical fan evidence.
- Fan verification now keeps firmware command success separate from verification, accepts matching level evidence without comparing it against a second derived RPM curve, and reports unverified physical response as inconclusive rather than inventing an RPM failure.
- Corrected worker-backed CPU authority reporting so the model override remains `LHM Worker Override` instead of immediately relabeling itself as generic `LHM Fallback`.
- Physical fan response, long-load temperature stability, and Performance-to-Auto release still require validation on `8A18`; no direct EC policy or firmware hold behavior was guessed from the log.

### GitHub #143 - Victus 15 `8DCD` Fan/Thermal Regression

- Investigate sustained fan-speed collapse below 2000 RPM at CPU temperatures above 80C while Performance mode is selected.
- Preserve the model's conservative no-direct-EC policy unless hardware readback proves a safer exact path.
- Require physical `8DCD` validation before release.

### GitHub #141 - OMEN 16-ap0xxx Key Routing

- Resolve the discrepancy between the shipped 3.8.0 behavior and current source tests for `VK=0xFF / Scan=0x002B`.
- Ensure Fn+F2 can never invoke OmenCore.
- Discover the actual dedicated OMEN-key and Fn+P event path on ProductId `8D26` without introducing broad WMI false positives.

### GitHub #142 - HyperX OMEN MAX 16 `8E9A` Identity

- Add exact, conservative capability and keyboard identity for the 2026 `16t-ah100` only after diagnostics establish safe routing.
- Do not inherit legacy EC writes or guessed tuning/RGB capabilities from adjacent models.

### Saved Custom Fan-Curve Selection

- **Implemented.** `ResolveInitialPresetSelection()` now migrates a saved ad-hoc `CustomFanCurve` to the `Custom` preset and repairs `LastFanPresetName` whenever the saved name is missing, empty, or stale (e.g. after an external rename), instead of silently falling back to Auto.
- Preserved the safety boundary between hydrating the saved UI selection and actually reapplying fan hardware at startup: the repair path only rewrites config metadata, and constructor-time selection still cannot call `FanService.ApplyPreset`.
- Added `FanControlViewModelTests.Constructor_MigratesSavedAdHocCurveWhenLastPresetIsMissingOrStale` covering null/empty/renamed `LastFanPresetName`, asserting zero hardware-apply calls during construction.
- Deleted/renamed custom presets already clear stale `LastFanPresetName` via the existing `ClearDeletedPresetConfigState` path; reviewed and confirmed correct, no change needed.

### Background Resources And Responsiveness

- Reproduce and reduce reported 400+ MB background footprints using combined main-process and hardware-worker measurements.
- Measure focused, dashboard, tray, popup, OSD, curve-active, and two-hour tray scenarios.
- Bound retained histories and subscriptions, defer optional providers, reduce duplicate polling/dispatcher work, and preserve thermal-control cadence.
- Target a sub-300 MB combined tray working set after warm-up, stable long-session memory, idle background CPU below 1%, and responsive window/popup activation on the reference system.
- **Partial progress (no hardware available in this environment to measure the above budgets):** audited `BackgroundTimerRegistry` against actual recurring background loops and registered three that were invisible to diagnostics: the global `HpWmiBios` heartbeat (`Critical` tier — keeps WMI BIOS commands active on 2023+ models), the `WmiFanController` countdown-extension reassert loop (`Critical` tier — directly relevant to the #143 fan-hold investigation), and the optional `RazerService` Chroma SDK heartbeat (`Optional` tier). `core-control-readiness.txt` and other diagnostic exports that read this registry will now show all three. The full before/after scenario-matrix measurement still requires physical OMEN/Victus hardware and is unchanged from "planned."

### OMEN-Key Field Diagnostics (Supports #141 Investigation)

- Added last-candidate tracking to `OmenKeyService`: every accepted or rejected key-hook candidate now records its source, VK/scan codes, accept/reject reason, and age, exposed via `GetDiagnosticSnapshot()` and a new `LastOmenKeyCandidate` line in `core-control-readiness.txt`.
- This directly targets the bug tracker's required coverage ("diagnostic snapshot test that records active watchers, selected query/event codes, and the last accepted/rejected candidate reason") without changing any acceptance/rejection logic — the existing classifier behavior, including the `VK=0xFF / Scan=0x002B` rejection, is unchanged.
- Added 3 new tests in `OmenKeyServiceTests` covering accepted candidates, rejected candidates, and overwrite-on-latest behavior.
- This does not resolve #141 itself (still needs shipped-artifact provenance verification and physical `8D26` hardware capture per the investigation order below), but gives field testers a queryable answer to "what did OmenCore see and why was it accepted/rejected" without relying on debug-level logs.

### GPU Overclock Profile Persistence

- **Implemented (discoverability fix; behavior was already safe).** Added a `Startup:` status chip on the Tuning page next to the existing Confirmed/Requested chips, driven by a new `StartupRestorePolicy.DescribeTuningStartupReapplyState()` helper, that tells the user in one line whether the confirmed GPU OC profile is `Enabled`, `Blocked` (and by which gate: global Startup Hardware Restore, Tuning-category restore, or the sensitive-model override), or `Not confirmed`.
- The underlying confirmation-based safety logic (Save Profile vs. ordinary Apply vs. Test Apply -> Keep) was already correct on review; this patch only makes the resulting state visible and explained, per the release condition that selecting/saving a profile must never imply startup authorization.
- **Safety fix:** `StartupRestorePolicy.IsSensitiveModel()` (used to gate startup hardware restore on OMEN 16/Victus systems) previously matched only the literal substring `"OMEN 16"`. Real HP WMI model strings vary (`"OMEN 16-xd0xxx"`, `"OMEN Gaming Laptop 16-ap0xxx"`, `"OMEN by HP Laptop 16-..."`), so the safety override could fail to engage on genuine OMEN 16 hardware. Replaced with a board-pattern regex match (`16-`/`16t-` board suffix) that also catches OMEN MAX 16. Victus matching (any size) is unchanged. Added 9 new tests in `StartupRestorePolicyTests`.
- Added an explicit "Manual apply only: AMD offsets are not saved and do not reapply when OmenCore starts" notice to the AMD GPU OC panel, since AMD ADL2 has no equivalent config-persistence/startup-restore path yet.

## Release Conditions

- No item moves to "Fixed" from issue text or command-success logs alone.
- #143 requires a bounded real-hardware thermal/fan test.
- #141 requires real-hardware key tests plus shipped-artifact verification.
- #142 requires exact identity verification and validation of every exposed control surface.
- #144 requires bounded `8A18` load testing with an independent physical RPM/temperature source; source-level truthfulness fixes do not prove hardware response.
- Custom selection must restore without an unauthorized startup fan write.
- Resource claims require before/after scenario evidence; one Task Manager screenshot is not sufficient.
- GPU OC startup reapply requires explicit confirmation, effective safety gates, backend readiness, and readback validation.
- Version files and release artifacts remain at 3.8.0 until the patch is implemented and the release gate is ready to run.

## Current Validation Status

- .NET 8 SDK (8.0.422) installed in the working environment; previously only the runtime was present.
- `dotnet build OmenCore.sln -c Release`: passed, 0 errors.
- `dotnet test src\OmenCoreApp.Tests\OmenCoreApp.Tests.csproj -c Release`: passed, 893/893 (up from an 864/864 baseline before this patch cycle's changes).
- Latest pass added a resource-efficiency fix mirroring the existing RAM-query memoization above: `WmiBiosMonitor.UpdateReadings()` queried CPU clock speed and SSD temperature (the latter via a separate, more expensive `root\Microsoft\Windows\Storage` WMI namespace connection) on every monitoring tick with no throttling, unlike the adjacent GPU-telemetry code. Both are display/diagnostics-only values, so they are now throttled to once every 2s and 5s respectively. Also closed a background-timer-visibility gap found during the same pass: `AudioReactiveRgbService`'s 33ms WASAPI buffer-read timer (active only while the Audio-Reactive RGB scene is running) was not registered with `BackgroundTimerRegistry`, so it was invisible to `core-control-readiness.txt` and other diagnostic exports; it now registers/unregisters alongside Start()/Stop() like the Razer Chroma heartbeat fix earlier in this cycle. Neither change alters fan/thermal/RGB behavior; verified by a full Release build and test run before and after.
- Version metadata aligned to `3.8.1` across `VERSION.txt`, all active project files (`OmenCoreApp`, `OmenCore.Avalonia`, `OmenCore.Linux`, `OmenCore.HardwareWorker`), the installer script, the Avalonia version fallback, README, INSTALL, and the `launch-readiness.txt`/`field-validation-script.txt`/`priority-model-validation-cards.txt`/`rc-validation-matrix.txt` diagnostic headings (and their snapshot tests). `src/OmenCore.Desktop` was left unversioned per existing policy.
- No #144 hardware acceptance claim is made until an `8A18` tester completes the bounded checks in the bug tracker.
- No claim is made that this session's GPU OC chip, OMEN-key diagnostics, or background-timer registrations have been validated on physical OMEN/Victus hardware — this development environment is not HP hardware (`Win32_ComputerSystem.Manufacturer` is ASUS). All of #141, #142, #143, #144 hardware acceptance, and the PERF-3810-001 resource scenario matrix still require a real reference machine.

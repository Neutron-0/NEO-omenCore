# OmenCore v3.8.2 - Critical Hang Fix

**Release Date:** TBD
**Release Status:** Code-complete, test-verified, and artifacts built in this environment; field confirmation from the original reporter on physical `8BCD` hardware still pending before tagging
**Type:** Patch release (release-blocker fix)
**Base Version:** v3.8.1

---

## Purpose

v3.8.2 exists solely to fix a critical regression reported within hours of the v3.8.1 release: OmenCore hangs and is force-closed by Windows ("Application Hang", Event ID 1002) within 10-20 seconds of launch. v3.8.1 is withdrawn as a recommended download pending this fix; see [3.8.1-BUG-REPORTS.md](3.8.1-BUG-REPORTS.md) for the full incident writeup (`BUG-3820-001`).

## Fixed

### Critical: Application Hang Within Seconds Of Launch (BUG-3820-001)

**Reported by:** OsamaBiden (Discord, OMEN 16-xd0010ax / ProductId `8BCD`), 2026-06-24, immediately after the v3.8.1 release. Two consecutive launches hung and were force-closed by Windows; Event Viewer confirmed `Application Hang`, `HangType=Cross-process`, `OmenCore.exe 3.8.1.0`.

**Root cause:** `HardwareWorkerClient.SendRequestAsync()` (the named-pipe client that talks to the out-of-process `OmenCore.HardwareWorker.exe`) reused a single `NamedPipeClientStream` across every request with no serialization and no recovery path after a timed-out read:

- If a worker response took longer than the client's `RequestTimeoutMs` (2000ms) — plausible under GC pauses, AMD ADL2/NVAPI driver calls, or just system load — the client's read was cancelled, but the now-late response message was left sitting, unconsumed, in the pipe's receive buffer.
- The *next* request's read would then consume that stale message instead of its own reply, permanently shifting every subsequent request/response pair by one. There was no request/response correlation and no reconnect-on-failure, so the connection never resynchronized itself.
- This was already visible in the field logs as the repeated `🥶 CPU/GPU temperature appears frozen` warnings (`WmiBiosMonitor.UpdateReadings`) immediately before the hang. The affected model (`8BCD`) has `_workerBackedCpuTempOverrideEnabled` set, so it calls into this exact pipe path on every monitoring cycle (every 2-5s) — far more aggressively than models that don't rely on the worker-backed CPU temperature override — which explains why this model hit the bug hard enough to hang within seconds while it went unnoticed elsewhere.
- The escalating retries from this desync (`WmiBiosMonitor`'s `Task.Run(...).Wait(timeout)` wrapper around the now-permanently-failing worker calls, fired every monitoring tick) is consistent with the eventual thread-pool/responsiveness exhaustion that Windows reported as a cross-process hang.

**Fix (`HardwareWorkerClient.cs`):**
- Added a `SemaphoreSlim(1, 1)` request gate around the entire write+read round-trip in `SendRequestAsync`, so concurrent callers queue instead of racing reads/writes on the shared pipe handle.
- On any write/read failure or timeout, the pipe handle is now disposed and nulled instead of reused. The existing `ShouldRecoverConnection`/`TryConnectToExistingWorkerAsync`/`TryRestartWorkerAsync` machinery (already used for worker-process-death recovery) now also handles this case, establishing a fresh, correctly-synchronized connection on the next call instead of perpetuating a desynced one.
- `WriteAsync`/`FlushAsync` are now covered by the same per-request cancellation token as the read (previously unguarded).
- Also fixed two newly-introduced bare `catch {}` blocks flagged by the repo's release-gate hygiene test (`ReleaseGateCodeHygieneTests`) to log the swallowed exception instead of silently discarding it.

**Why this was not a fan/thermal control change:** This is an IPC reliability fix in the telemetry transport layer only. No fan-control activation timing, debounce, EC-write gating, or thermal-protection threshold was touched — consistent with this project's standing rule that those require physical-hardware evidence before any change (see `feedback-omencore-safety` norms). The hang reproduces independent of any specific thermal event.

**Verification performed in this environment (not OMEN hardware):**
- Added `HardwareWorkerClientPipeTests` (2 new tests) using a real `NamedPipeServerStream`/`NamedPipeClientStream` pair with reflection-injected pipe state: one proves a non-responding server now disposes the client pipe instead of leaving it reusable; one proves 5 concurrent requests against a deliberately slow echo server each get back their *own* response, never another caller's.
- Full Release build: 0 errors.
- Full Release test suite: 895/895 passed (up from 894 — includes the 2 new tests and the hygiene-gate fix).
- Smoke-launched `OmenCore.exe` (Release build) on this dev machine (non-OMEN hardware) for 18+ seconds: process stayed responsive (`Get-Process.Responding = True`), clean shutdown, no errors/exceptions in the session log.

**Not yet done / explicitly still open:**
- The original reporter has not yet confirmed v3.8.2 resolves the hang on their `8BCD` hardware. Do not mark this "Fixed" in the public sense until they confirm — see Release Conditions below.
- This fix addresses the protocol-desync/hang mechanism; it does not change the deeper question of *why* the worker sometimes responds slowly on this model (driver contention, etc.). If slow responses continue, they should now degrade gracefully (timeout + clean reconnect) instead of cascading into a hang.

### Critical (Safety): Fans Stuck At Max Independent Of Temperature; Lid-Close Failed To Suspend, Followed By A BIOS Thermal Shutdown (BUG-3820-004)

**Reported by:** nsilveri ([GitHub #146](https://github.com/theantipopau/omencore/issues/146)), 2026-06-25. OMEN Laptop 15-en1xxx, ProductId `88D2`, AMD Ryzen 7 5800H + NVIDIA RTX 3070 hybrid GPU, v3.8.0. Fans were observed stuck at maximum speed independent of temperature; the reporter closed the lid to trigger standby, the laptop did not actually enter low-power standby (fans kept running, screen stayed black), and was later found powered on with a BIOS message reporting a shutdown due to overheating — while sitting closed inside a backpack.

**Root cause:** `WmiFanController`'s Max-mode keepalive/reassertion timer (`CountdownExtensionCallback`) runs on its own independent `System.Threading.Timer`, separate from `FanService`'s monitor loop, with no suspend awareness of its own:

- It was only stopped as a side effect reached partway through a *successful* `RestoreAutoControl()` call. `RestoreAutoControl()` had an early `if (!IsAvailable) return false;` guard *before* that point — any transient WMI unavailability (plausible during a suspend transition) skipped stopping the timer entirely.
- `FanService.HandleSystemSuspend()` only attempted the restore `if (FanWritesAvailable)` and silently discarded the result either way, so a failed or skipped restore during suspend left the keepalive timer running with no further attempt to stop it — while the rest of the system correctly proceeded to suspend.
- Net effect: if Max mode was active at lid-close and the BIOS auto-control restore failed or was skipped for any reason during the brief suspend transition window, the timer kept reasserting Max fan mode via WMI every ~8 seconds for as long as the process had threads scheduled, directly matching "fans remained at full speed" through lid-close — periodic background hardware I/O of exactly the kind that can interfere with a clean Modern Standby/S0ix transition.

**Fix:**
- `IFanController.StopCountdownExtension()` added as a default no-op interface method, overridden in `WmiFanControllerWrapper` to delegate to the real timer.
- `WmiFanController.RestoreAutoControl()` now stops the countdown timer unconditionally, first, before the `IsAvailable` check and before any reset-sequence logic — closing the gap for every caller (manual "switch to Auto," preset switching, suspend handling), not just suspend.
- `FanService.HandleSystemSuspend()` additionally calls `StopCountdownExtension()` directly and unconditionally (defense in depth for the case where fan writes are unavailable entirely and `RestoreAutoControl()` is never invoked), and now correctly logs whether the BIOS auto-control restore actually succeeded instead of unconditionally claiming success regardless of outcome.

**Scope discipline — what this does NOT change:** No fan curve, thermal-protection threshold, or EC-write gating logic was touched while the system is awake. The change is scoped entirely to suspend-time and restore-to-auto behavior, and strictly *reduces* background WMI write activity during those transitions rather than adding any.

**Investigated but deliberately not fixed in this patch:** the reporter also described the displayed "GPU temperature" as ambiguous and consistently higher than CPU temperature even with the dGPU confirmed idle. Code review found the root cause — `WmiBiosMonitor` unconditionally prefers the NVIDIA dGPU's NVAPI die-temperature reading over the WMI BIOS's own GPU reading whenever NVAPI is available, regardless of which GPU is actually active, and that value feeds fan-curve evaluation via `Math.Max(cpuTemp, gpuTemp)`. A correct fix needs a reliable "is the dGPU actually active" signal wired into the monitoring loop without regressing temperature accuracy on the majority of models where the NVIDIA dGPU genuinely is the active GPU — broader and riskier than this patch's evidence (a single report) justifies. See `BUG-3820-004` in [3.8.1-BUG-REPORTS.md](3.8.1-BUG-REPORTS.md) for the full writeup.

**Verification performed in this environment (not OMEN hardware):**
- Added `FanServiceSuspendTests.cs` (5 new tests) proving the keepalive timer is stopped on suspend in every failure mode: restore succeeds, restore returns `false`, restore throws, fan writes unavailable entirely, and a failure stopping the timer itself is caught rather than bubbling up.
- Full Release build: 0 errors, 0 warnings.
- Full Release test suite: 895/895 passed (see Current Validation Status below for the post-#146 count).

**Not yet done / explicitly still open:**
- The reporter has not yet confirmed v3.8.2 resolves the stuck-fan/failed-standby behavior on their physical `88D2` hardware.
- The GPU-temperature-source root cause remains unfixed pending a second corroborating report (ideally with the full diagnostics zip, not just the session log).
- Per explicit instruction, this fix was **not** rolled into a new installer/portable build for this round — see Current Validation Status.

### Diagnostics: WMI Command History, Hardware Info, And EC State Were Never Actually Collected (Affects GitHub #145 Evidence Gap)

**How this was found:** the `wmi-command-history.txt` supplied by a `MODEL-3810-002`/#145 reporter as the Battery Care evidence requested in [3.8.1-BUG-REPORTS.md](3.8.1-BUG-REPORTS.md) contained only the literal placeholder string `"WMI fan controller not available"` — on a machine where WMI fan control was demonstrably working throughout the same session's logs. That contradiction led to the real defect.

**Root cause:** `DiagnosticExportService.CollectAndExportAsync()` accepts optional `wmiController`/`hwMonitor`/`ecAccess` parameters that three collectors (`wmi-command-history.txt`, `hardware-info.txt`, `ec-state.txt`) depend on — but every production call site (`SettingsViewModel.ExportDiagnosticsAsync`, the "Export Diagnostics" button users are told to attach to GitHub issues, and `MainViewModel.ReportModelAsync`, the "Report Model" button) called it with none of these arguments. These three files have been the same "not available" placeholder in every diagnostics export any user has ever produced, regardless of their actual hardware state. Separately, even with correct wiring, `HpWmiBios.SetBatteryCareMode`/`GetBatteryCareMode` had no command-history recording at all — only `WmiFanController`'s fan commands were ever tracked — so Battery Care evidence specifically still would not have appeared.

**Fix (diagnostics-only; no fan/thermal/EC control behavior changed):**
- `HpWmiBios.cs`: added a command-history list and `GetCommandHistory()`, recorded at every success/failure exit point of `SendBiosCommand`/`SendBiosCommandLegacy` — the chokepoint every BIOS command (fan, GPU power, battery care, lighting, overdrive) routes through — so any WMI BIOS command attempt is now visible in diagnostics exports, not just fan commands.
- `DiagnosticExportService.cs`: `wmiController` is now also accepted as a constructor parameter with the same `?? ` fallback pattern already used for `monitoringService`/`fanService`, so future call sites can't silently omit it the way both existing ones did.
- `SettingsViewModel.cs` / `MainViewModel.cs`: both `DiagnosticExportService` construction sites now pass `_wmiBios`; the "Report Model" path now also passes `_hardwareMonitoringService`/`_fanService`, which it was missing entirely (its `hardware-info.txt`/resource-footprint sections were degraded too).

**Verification performed in this environment:** Full Release build 0 errors/warnings; full Release test suite 900/900 (no new tests added — this is plumbing existing, already-tested collector logic to data it never previously received; the collectors' own reflection-based parsing was already exercised by existing diagnostics tests).

**Not yet done / explicitly still open:** the next diagnostics zip exported by any user is the actual remaining evidence needed before any `CMD_BATTERY_CARE` command-handling change for #145 — this fix only makes that evidence collectible, it does not itself resolve the underlying Battery Care WMI failure.

### Power Automation Never Applied The Battery/AC Profile At Boot (Discord, OMEN MAX 16 `8D41`)

**Reported by:** ACF (Discord), 2026-06-27, OMEN 16 Max ah0500na — one of nine items reported on this model; see `BUG-3820-005` in [3.8.1-BUG-REPORTS.md](3.8.1-BUG-REPORTS.md) for the full list, most of which are confirmed deliberate model-safety gates or NVAPI/firmware-reported hardware locks rather than bugs.

**Root cause:** `PowerAutomationService.ApplyCurrentProfile()` — explicitly commented "useful for initial setup" — had no caller anywhere in the codebase. The service only reacted to `SystemEvents.PowerModeChanged`; on a fresh launch that event hasn't fired, so a user with Power Automation enabled kept whatever fan/performance state was last manually set or startup-restored, regardless of their configured AC/Battery profile, until the next AC↔battery transition. This was not model-specific.

**Fix:** added a call to `_powerAutomationService.ApplyCurrentProfile()` at the end of `MainViewModel.RestoreSettingsOnStartupAsync()`, after the existing GPU-Power-Boost/fan-preset/TCC-offset startup restores, wrapped in its own try/catch. It runs last so Power Automation (if enabled) has final say over the generic last-state restores, matching the feature's purpose. The method already no-ops internally when Power Automation is disabled, so this is zero-risk for users who don't use the feature.

**Verification performed in this environment:** Full Release build 0 errors/warnings; full Release test suite 900/900.

**Not yet done / explicitly still open:** reporter confirmation that the Battery/AC profile is now applied at boot. CPU/GPU power-limit locks and Dynamic Boost remain confirmed-by-design (not bugs). RGB light-bar routing and battery-preset-name substitution to "Custom"/"Balanced" remain evidence-needed — see below for the ninth item, which is also now fixed.

### Optimizer "Disable Last Access Timestamps" Always Reported Itself As Failed (Same Report, Item 9 Of 9)

**Root cause:** the apply action (`StorageOptimizer.DisableLastAccessAsync()` running `fsutil behavior set disablelastaccess 1`) was already correct — OmenCore already runs `requireAdministrator`, so elevation was never the issue. The bug was in *verifying* the result: `OptimizationVerifier.VerifyLastAccessDisabled()` (which drives the Settings toggle's displayed state) read the `NtfsDisableLastAccessUpdate` registry value and compared it to exactly `1`. `fsutil behavior set disablelastaccess <0-3>` actually stores the mode in the low 2 bits and ORs in `0x80000000` to mark it as explicitly configured, so a successful "disable" apply leaves the registry at `0x80000001` — never `1` — so the toggle always showed the optimization as inactive immediately after correctly applying it. A second, duplicate check (`StorageOptimizer.IsLastAccessDisabled()`, used by the drift-report export path) had the same class of bug via fragile `fsutil behavior query` text matching.

**Fix:** both checks now mask the low 2 bits (`value & 0x3`) before comparing, matching what `fsutil` actually writes; `StorageOptimizer.IsLastAccessDisabled()` was also switched to the same direct registry read used by the verifier, removing the duplicate/inconsistent logic. No change to the apply path itself — this was purely a state-verification bug, not a control-write bug, and touches no fan/thermal/EC code. The mode-decoding logic was extracted into a pure static `NtfsBehaviorFlags` helper so it's unit-testable without mocking the registry; added `NtfsBehaviorFlagsTests` covering the `0x80000001`-style explicitly-configured encoding directly. Also added `PowerAutomationServiceApplyCurrentProfileTests` closing the test-coverage gap noted for the Power Automation boot-apply fix above.

**Verification performed in this environment:** Full Release build 0 errors/warnings; full Release test suite 911/911 (11 new tests: 2 for `PowerAutomationService.ApplyCurrentProfile()`, 9 for `NtfsBehaviorFlags`). One transient full-suite crash was caught and fixed during this work: an earlier draft of the new `PowerAutomationService` test left a `FanService` instance undisposed, and its background monitor-loop timer kept running and crashed the test host partway through a later, unrelated test — fixed by disposing all constructed services at the end of each test, matching the disposal pattern already used elsewhere in this test suite.

### FanService Background Monitor Loop Could Crash On An Unhandled Exception During Shutdown (Timing-Dependent)

**How this was found:** while investigating the test-host crash above, a *second*, independent full-suite run aborted with the same symptom (unhandled exception from a `Task.Delay` continuation inside `FanService.MonitorLoop`) even after the leaked-`FanService` test bug was already fixed — meaning a second, real, separate defect was responsible, not just a leaked test resource. A third run with no code changes completed cleanly, confirming the crash is timing-dependent (a race), not deterministic.

**Root cause:** `FanService.Stop()` (called from `Dispose()`) does `_cts.Cancel(); _cts.Dispose(); _cts = null;` with no wait for the background `MonitorLoop` task to actually observe the cancellation and return. `MonitorLoop`'s main work is already wrapped in a catch-all (`catch (Exception ex)` logs and continues — this part was already safe), but its two `await Task.Delay(..., token)` calls only caught `OperationCanceledException`. If `Dispose()` disposes the `CancellationTokenSource` while a delay is in flight, `Task.Delay` can race and throw `ObjectDisposedException` instead of `OperationCanceledException` — which was not caught, escaping uncaught from a fire-and-forget background task (`MonitorLoop` is started via `_ = Task.Run(...)`, never awaited), which is exactly the kind of unobserved-task-exception that some hosts (including the test runner used in this session) treat as fatal.

**Fix:** added a sibling `catch (ObjectDisposedException)` next to both existing `catch (OperationCanceledException)` blocks in `MonitorLoop`, with the same graceful-exit behavior either way (log "Fan monitor loop stopped" and return/break). No change to any fan-control, thermal-protection, or EC-write logic — this only makes the loop's *shutdown* path tolerant of a known `CancellationTokenSource`-disposal race, the same race many `IDisposable` consumers of background loops need to guard against.

**Why this matters beyond tests:** this is a real (if narrow-timing) crash path in the shipped application too — any time `FanService.Dispose()` runs (app shutdown, or any code path that disposes and recreates the fan service) while a monitor-loop tick happens to be mid-delay, the same unhandled exception could in principle propagate. Whether that crashes the actual WPF app process depends on .NET's unobserved-task-exception handling at the time, which is not something to rely on — the fix removes the ambiguity entirely.

**Verification performed in this environment:** Full Release build 0 errors/warnings; full Release test suite passed cleanly after the fix (this race is timing-dependent so a single clean run does not prove it can never occur, but the fix is correct by construction — it treats the documented CTS-disposal race exactly like the already-handled cancellation case).

## Minor Improvements (Code-Quality / Reliability Polish)

These are small, hardware-independent cleanups verified by build + the full test suite. They do **not** touch any fan/thermal/EC control path and carry no behavior risk; they ride along with the hotfix because they are zero-risk and thematically aligned with its telemetry-reliability/diagnosability focus.

- **Removed per-poll wasted work in the hardware-worker telemetry path.** `HardwareWorkerClient.GetSampleAsync()` previously logged a debug line on every ~2-second worker poll that ran an `O(n)` `json.Contains("GpuTemperature")` substring scan over the full telemetry payload purely to format a boolean — work that executed on every poll even though the message is dropped at the sink in production. Removed; the adjacent "Deserialized sample" debug line already records the parsed result. Eliminates a recurring per-poll string-build + substring scan on the monitoring hot path (relevant to the standing background-resource concern).
- **Made the diagnostic-logging subsystem's own failures visible.** Converted three bare `catch {}` blocks in `DiagnosticLoggingService` (capture-task shutdown wait, and the relevant-process enumeration loop) to typed catches — `Debug.WriteLine` for single-point failures, a typed silent skip for the per-process inspection loop where logging would be noise across normal system processes. This is the same "logs just stop with no trace" lesson that made `BUG-3820-001` hard to diagnose, applied to the diagnostic subsystem itself. Baseline updated in `ReleaseGateCodeHygieneTests`.
- **Cleared all build warnings and a stale hygiene-baseline entry** (carried from earlier this session): four `CS1998` async-without-`await` warnings in `BloatwareManagerViewModel` (the preset methods did purely synchronous work) were resolved by making the methods synchronous; the resolved `HardwareWorkerClient` bare-catch baseline entry from the hang fix was removed. The main app now builds with **0 warnings**.

## Carried Forward From v3.8.1 (Unchanged, Still Hardware-Gated)

These items were already pending hardware validation in v3.8.1 and are out of scope for this hang-fix patch. They are listed here only so the release gate isn't mistaken for "everything closed." See [3.8.1-BUG-REPORTS.md](3.8.1-BUG-REPORTS.md) for full detail:

- GitHub #141 (OMEN 16-ap0xxx key routing / shipped-artifact provenance) — needs physical `8D26` key-event capture.
- GitHub #142 (HyperX OMEN MAX 16 `8E9A` identity) — needs full diagnostic evidence before any exact-identity entry is added.
- GitHub #143 (Victus 15 `8DCD` fan/thermal regression) — needs a bounded, abortable physical load test.
- BUG-3810-005 (Discord fan-spike-at-idle reports) — diagnostics-only change shipped in 3.8.1. A first full session log (OMEN Transcend 14 `8E41`, 2026-06-27) was reviewed during this v3.8.2 cycle: the activation-to-release timeline shows a sustained multi-second decay under active max-fan cooling, not an instant single-sample bounce, which leans toward real (if brief) thermal excursions rather than a sensor glitch. No activation-timing change was made — see the full write-up in [3.8.1-BUG-REPORTS.md](3.8.1-BUG-REPORTS.md). Still needs diagnostics-zip-level raw per-poll evidence before any behavior change is considered.
- PERF-3810-001 resource/responsiveness scenario matrix — needs physical OMEN/Victus hardware to measure against budget.
- AMD GPU OC startup-restore — still manual-only by design; not revisited in this patch.

## Release Conditions

- This patch does not get marked "Fixed" for BUG-3820-001 from this environment's testing alone — it requires the original reporter (or another `8BCD`/worker-override-enabled user) to confirm v3.8.2 launches and runs without hanging.
- All carried-forward items above remain pending exactly as documented in v3.8.1; nothing here should be read as resolving them.
- Version files and release artifacts move to 3.8.2 only for this hang fix; no unrelated version-gated claims are made.

## Current Validation Status

- `dotnet build OmenCoreApp.csproj -c Release`: passed, 0 errors, 0 warnings.
- All 5 projects in `OmenCore.sln` (`OmenCoreApp`, `OmenCoreApp.Tests`, `OmenCore.HardwareWorker`, `OmenCore.Linux`, `OmenCore.Avalonia`) build clean in Release with 0 warnings as of this patch — the last 2 remaining `CS1998` warnings (test-only, no behavior change) were cleared alongside the `FanService.MonitorLoop` fix above.
- `dotnet test OmenCoreApp.Tests.csproj -c Release`: passed, 911/911 (895 from the BUG-3820-001 hang fix and minor improvements, plus 5 new `FanServiceSuspendTests` for BUG-3820-004, plus 11 new tests added afterward for `PowerAutomationServiceApplyCurrentProfileTests`/`NtfsBehaviorFlagsTests` covering the two BUG-3820-005 fixes. The diagnostics-export wiring fix for #145 added no new tests of its own).
- Smoke launch of the freshly rebuilt `OmenCore.exe` (`publish/win-x64-build/OmenCore.exe`, the same binary packaged into the artifacts below) on this (non-OMEN, AMD desktop) dev machine, 2026-06-28: ran for 2m8s end to end (`OmenCore_20260628_150028.log`), `Responding = True` throughout, full capability-detection and monitoring-loop startup sequence completed normally, then a clean, complete shutdown (`OmenCore shutting down...` through `OmenCore shutdown complete`, including `FanService` disposal) — no `[ERROR]`-level entries anywhere in the log, only expected `[WARN]`s for hardware this desktop doesn't have (no NVIDIA GPU, PawnIO needing a reboot, a couple of hotkeys that queued for retry). This is also the first real-world exercise of the `FanService.MonitorLoop` shutdown-race fix above outside the test suite, and it shut down cleanly.
- Version metadata bumped to `3.8.2` across `VERSION.txt`, `OmenCoreApp`, `OmenCore.Avalonia`, `OmenCore.Linux`, `OmenCore.HardwareWorker` project files, the installer script (`OmenCoreInstaller.iss`), the wizard-image generator default, and the Avalonia version fallback string.
- **Artifacts rebuilt 2026-06-28** to include every fix in this document: the BUG-3820-004 suspend fix, the #145 diagnostics-export wiring fix, both BUG-3820-005 fixes (Power Automation startup-apply, Optimizer last-access-timestamp verification), and the `FanService.MonitorLoop` shutdown-race fix. These hashes supersede the 2026-06-26 build recorded earlier in this branch's history, which predated all of the above.
  - Windows artifacts built with `build-installer.ps1`: `OmenCoreSetup-3.8.2.exe` and `OmenCore-3.8.2-win-x64.zip`.
  - Linux artifact built with `build-linux-package.ps1 -SkipBinaryVersionCheck`: `OmenCore-3.8.2-linux-x64.zip`, `.sha256`, and `version.json`. Binary execution smoke was skipped because this run was on Windows, not Linux/WSL (`binaryExecutionSkipped: true` in the verification manifest).
  - Artifact SHA256 (also recorded in `artifacts/SHA256SUMS-3.8.2.txt`):
    - `3D688E472B420DABE8B69F791663E45944304F954D91A626F7A87AC2B277B58B  OmenCoreSetup-3.8.2.exe`
    - `DA543C5E3DF5BF563E5D062DA61B3D82D6D3F0E5EB11A5F36202E1F65CA1D623  OmenCore-3.8.2-win-x64.zip`
    - `4853C73F548564E64A53EECE4F78FF2D8759DDB920C9ACDB323C96326CD879BF  OmenCore-3.8.2-linux-x64.zip`
- No claim is made that either fix has been validated on physical OMEN hardware; this development environment is not HP hardware. Reporter confirmation is the actual acceptance criterion for both BUG-3820-001 and BUG-3820-004.

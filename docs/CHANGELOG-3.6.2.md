# OmenCore v3.6.2 Changelog (Stabilization)

## Test Suite Validation (Final Pass)
- [x] Cadence mismatch resolved: Test `GetEffectiveCadenceInterval_UsesActiveCadence2s_WhenOverlayRealtimeModeEnabledInTray` updated to expect 2s active cadence.
  Root cause: Test was stale. v3.6.2 deliberately reduced active monitoring cadence from 1s to 2s for focused-window overhead reduction. This change was already documented in Fixed section and implemented in [src/OmenCoreApp/Services/HardwareMonitoringService.cs:24](../src/OmenCoreApp/Services/HardwareMonitoringService.cs).
  Fix: [src/OmenCoreApp.Tests/Services/HotkeyAndMonitoringTests.cs:326-345](../src/OmenCoreApp.Tests/Services/HotkeyAndMonitoringTests.cs) now expects `TimeSpan.FromSeconds(2)` with comment noting OSD overlay remains responsive at 2s cadence.
- [x] Post-RC1 focused hardening passed: `dotnet build src/OmenCoreApp/OmenCoreApp.csproj -c Debug --no-restore` -> **0 errors, 0 warnings**.
- [x] Post-RC1 focused regression tests passed: `dotnet test src/OmenCoreApp.Tests/OmenCoreApp.Tests.csproj -c Debug --no-restore --filter "FullyQualifiedName~RgbManagerTests|FullyQualifiedName~WmiBiosMonitorTests|FullyQualifiedName~RuntimeCommandDispatcherTests"` -> **21 passed, 0 failed**.
- [x] Full solution build passed: `dotnet build OmenCore.sln -c Debug --no-restore`.
- [x] Full Windows test suite passed: `dotnet test src/OmenCoreApp.Tests/OmenCoreApp.Tests.csproj -c Debug --no-build` -> **642 passed, 0 failed**.
- [x] Focused cadence/concurrency regressions passed: targeted run covering cadence + latest-wins coordinator tests -> **2 passed, 0 failed**.
- [x] All modified source files verified for compile errors: No errors found.
- [x] Focused regression tests passed (dashboard dormancy, GeneralViewModel projection, runtime counters, tray behavior, release gates): **36 passed, 0 failed**.
- [x] Windows app build succeeded (OmenCoreApp.csproj, Debug configuration).

## Release Readiness
- **Status**: RC hardening in progress; code-side stabilization complete, awaiting final hardware matrix sign-off.
- **Scope**: Runtime authority correction, UI quieting, thermal safety, focused-window overhead reduction.
- **Field Impact**: Addresses mode drift, OSD anomalies, hidden-surface churn, and tray/popup dispatcher overhead.
- **Remaining**: Manual performance matrix on real hardware (captures frame pacing, load averages, GC pressure with sustained gaming session) and Scenario A operator runbook evidence.
## Architecture Remediation Status
- v3.6.2 is a stabilization release, not a feature release.
- The focus is runtime authority correction, deterministic hotkey cycling, thermal safety, and lower focused-window telemetry overhead.
- The release narrows field-reported mode drift and OSD anomalies by removing phantom runtime states and making the canonical mode labels match the actual fan policy.

## Fixed
- [x] CPU thermal authority attribution no longer flaps during same-poll fallback evaluation.
  Root cause: WMI/ACPI authority could be recorded before the LibreHardwareMonitor fallback decision finished, then immediately switch to fallback within the same polling pass. That made diagnostics noisier and could inflate authority switch counts without a real source transition.
  Fix: [src/OmenCoreApp/Hardware/WmiBiosMonitor.cs](../src/OmenCoreApp/Hardware/WmiBiosMonitor.cs) now defers primary CPU authority assignment until fallback evaluation completes, only records WMI/ACPI when fallback did not apply, and logs the actual adaptive fallback cooldown duration after worker timeout.
- [x] RGB effect capability filtering covers additional real effect payloads before provider fanout.
  Root cause: the graceful-degradation filter recognized basic `effect:*` names, but payload forms such as `breathing:#RRGGBB`, `pulse:#RRGGBB:1000`, `wave`, and `off` could still be sent to providers that did not advertise support.
  Fix: [src/OmenCoreApp/Services/Rgb/RgbManager.cs](../src/OmenCoreApp/Services/Rgb/RgbManager.cs) now resolves those payload forms to `Breathing`, `Wave`, and `Off` capability classes before fanout, keeping static-only providers out of unsupported dynamic/off requests.
- [x] Hotkey cycle state now resolves canonical slots in the required order: Auto -> Gaming -> Extreme -> Custom -> Quiet.
  Root cause: Gaming and Quiet were not first-class built-in presets, which let alias resolution and quick-access selection drift into the wrong slot.
  Fix: built-in Gaming and Quiet presets now exist in [src/OmenCoreApp/ViewModels/FanControlViewModel.cs](../src/OmenCoreApp/ViewModels/FanControlViewModel.cs), Gaming resolves to the Gaming runtime label in [src/OmenCoreApp/Services/FanService.cs](../src/OmenCoreApp/Services/FanService.cs), and hotkey alias routing now keeps Gaming separate from Extreme in [src/OmenCoreApp/ViewModels/FanControlViewModel.cs](../src/OmenCoreApp/ViewModels/FanControlViewModel.cs).
- [x] Quiet mode no longer manufactures a fake Manual preset.
  Root cause: Quiet requests created a non-built-in manual preset, which polluted runtime slot detection.
  Fix: Quiet now selects the built-in Quiet preset directly.
- [x] Extreme mode now bypasses the normal smoothing path on apply.
  Root cause: the highest thermal-performance mode still entered the normal transition path, which delayed fan ramp-up during escalation.
  Fix: Extreme preset applies now request immediate fan application in [src/OmenCoreApp/ViewModels/FanControlViewModel.cs](../src/OmenCoreApp/ViewModels/FanControlViewModel.cs).
- [x] Battery OSD no longer snaps to 100% during cooldown windows.
  Root cause: the battery provider returned a hardcoded 100 while throttled.
  Fix: [src/OmenCoreApp/Hardware/WmiBiosMonitor.cs](../src/OmenCoreApp/Hardware/WmiBiosMonitor.cs) now caches the last valid charge reading and returns that cached value during cooldown or battery-disable states.
- [x] Thermal authority hardening added for low-temp/high-load CPU telemetry drift (Issue #129).
  Root cause: CPU thermal authority could remain on low-confidence WMI/ACPI values during active load, masking package-sensor divergence and making source transitions hard to audit.
  Fix: [src/OmenCoreApp/Hardware/WmiBiosMonitor.cs](../src/OmenCoreApp/Hardware/WmiBiosMonitor.cs) now enforces mismatch-confirmed LibreHardwareMonitor fallback under suspicious low-temp/high-load conditions, tracks explicit CPU thermal authority source/reason, logs authority transitions, and exposes current authority in monitoring source text for diagnostics export.
- [x] Victus e0xxx capability/identity fallback reduced (Issue #128).
  Root cause: ProductId 88EC fell through to broad Victus family defaults, producing low-confidence identity output and ambiguous keyboard capability messaging.
  Fix: explicit ProductId 88EC entries were added to [src/OmenCoreApp/Hardware/ModelCapabilityDatabase.cs](../src/OmenCoreApp/Hardware/ModelCapabilityDatabase.cs) and [src/OmenCoreApp/Services/KeyboardLighting/KeyboardModelDatabase.cs](../src/OmenCoreApp/Services/KeyboardLighting/KeyboardModelDatabase.cs) with conservative, non-overstated defaults pending field verification.
- [x] RGB dynamic-effect unsupported paths now degrade gracefully (Issue #130).
  Root cause: effect fanout attempted unsupported provider endpoints without explicit pre-filtering, creating noisy failures on platforms that support static RGB but not dynamic effects.
  Fix: [src/OmenCoreApp/Services/Rgb/RgbManager.cs](../src/OmenCoreApp/Services/Rgb/RgbManager.cs) now resolves known effect types, skips unsupported providers with explicit logs, and exits cleanly when no provider supports the requested effect.
- [x] Focused-window monitoring overhead is lower.
  Root cause: active cadence was too aggressive for the amount of UI-thread work it triggered.
  Fix: [src/OmenCoreApp/Services/HardwareMonitoringService.cs](../src/OmenCoreApp/Services/HardwareMonitoringService.cs) now uses a 2s active cadence instead of 1s.
- [x] Runtime telemetry no longer fans out through the shared mode/projection event pipeline.
  Root cause: `RuntimeStateEngine.StateChanged` was still carrying monitoring samples, so sample noise triggered the same subscriber path used for fan/performance/curve projection.
  Fix: [src/OmenCoreApp/Services/RuntimeStateEngine.cs](../src/OmenCoreApp/Services/RuntimeStateEngine.cs) no longer stores or publishes monitoring samples, and [src/OmenCoreApp/App.xaml.cs](../src/OmenCoreApp/App.xaml.cs) now sends tray telemetry directly from `MainViewModel.LatestMonitoringSample`.
- [x] Main summary bindings no longer redraw on unchanged rendered text.
  Root cause: tiny telemetry noise still raised `PropertyChanged` for CPU/GPU/memory/storage/clock summaries even when the user-visible strings were identical.
  Fix: [src/OmenCoreApp/ViewModels/MainViewModel.cs](../src/OmenCoreApp/ViewModels/MainViewModel.cs) now compares old/new rendered summaries before notifying summary bindings.
- [x] Hidden General surfaces now skip telemetry projection entirely.
  Root cause: `MainViewModel` still pushed every accepted sample into `GeneralViewModel` even when the General tab was not visible.
  Fix: [src/OmenCoreApp/ViewModels/GeneralViewModel.cs](../src/OmenCoreApp/ViewModels/GeneralViewModel.cs) now exposes a visibility gate, and [src/OmenCoreApp/Views/GeneralView.xaml.cs](../src/OmenCoreApp/Views/GeneralView.xaml.cs) toggles telemetry projection based on actual view visibility.
- [x] Hidden and minimized dashboard surfaces now become dormant.
  Root cause: dashboard redraw suppression existed in the control, but [src/OmenCoreApp/ViewModels/DashboardViewModel.cs](../src/OmenCoreApp/ViewModels/DashboardViewModel.cs) still accepted hidden samples and mutated chart/history state.
  Fix: [src/OmenCoreApp/ViewModels/DashboardViewModel.cs](../src/OmenCoreApp/ViewModels/DashboardViewModel.cs) now keeps only the latest queued hidden sample, and [src/OmenCoreApp/Controls/HardwareMonitoringDashboard.xaml.cs](../src/OmenCoreApp/Controls/HardwareMonitoringDashboard.xaml.cs) toggles dashboard projection from actual visibility/minimized state.
- [x] Tray and popup refreshes now skip redundant rendered state.
  Root cause: fixed tray/popup timers still reassigned identical tooltip, menu, icon, and popup text state even when the user-visible output had not changed.
  Fix: [src/OmenCoreApp/Utils/TrayIconService.cs](../src/OmenCoreApp/Utils/TrayIconService.cs) now caches last rendered tray state, and [src/OmenCoreApp/Views/QuickPopupWindow.xaml.cs](../src/OmenCoreApp/Views/QuickPopupWindow.xaml.cs) now caches popup telemetry render state to suppress no-op redraws.
- [x] Linux config ownership is fully unified under TOML.
  Root cause: `battery.profile` still persisted through a separate JSON helper, leaving split config authority in the Linux CLI.
  Fix: [src/OmenCore.Linux/Config/OmenCoreConfig.cs](../src/OmenCore.Linux/Config/OmenCoreConfig.cs) now owns battery profile persistence and the dead JSON `ConfigManager` path was removed from [src/OmenCore.Linux/Program.cs](../src/OmenCore.Linux/Program.cs).

## Changed
- [x] v3.6.2 version metadata updated in [VERSION.txt](../VERSION.txt).
- [x] Regression coverage added for canonical fan-mode identity, hotkey slot resolution, curve safety floor, and battery cooldown behavior.
- [x] Diagnostics now export runtime UI amplification ratios from [src/OmenCoreApp/Services/RuntimeUiPerformanceCounters.cs](../src/OmenCoreApp/Services/RuntimeUiPerformanceCounters.cs) through [src/OmenCoreApp/Services/Diagnostics/DiagnosticExportService.cs](../src/OmenCoreApp/Services/Diagnostics/DiagnosticExportService.cs), including projected-sample, dispatcher, and per-surface acceptance ratios.
- [x] RC field-validation diagnostics expanded with dormancy, hidden-surface suppression, tray/popup render-cache hit rates, and latest-sample replacement counters in [src/OmenCoreApp/Services/RuntimeUiPerformanceCounters.cs](../src/OmenCoreApp/Services/RuntimeUiPerformanceCounters.cs).
- [x] Diagnostics export now includes a bounded snapshot mode (`runtime-performance-bounded.txt`) to capture short-window scenario evidence (focused idle, tray idle, dashboard active, popup active, OSD active) without continuous logging in [src/OmenCoreApp/Services/Diagnostics/DiagnosticExportService.cs](../src/OmenCoreApp/Services/Diagnostics/DiagnosticExportService.cs).
- [x] Regression coverage now includes unchanged-summary suppression in [src/OmenCoreApp.Tests/ViewModels/MainViewModelTests.cs](../src/OmenCoreApp.Tests/ViewModels/MainViewModelTests.cs) and derived counter ratios in [src/OmenCoreApp.Tests/Services/RuntimeUiPerformanceCountersTests.cs](../src/OmenCoreApp.Tests/Services/RuntimeUiPerformanceCountersTests.cs).
- [x] Hidden-surface suppression coverage now exists in [src/OmenCoreApp.Tests/ViewModels/GeneralViewModelTests.cs](../src/OmenCoreApp.Tests/ViewModels/GeneralViewModelTests.cs) and [src/OmenCoreApp.Tests/ViewModels/DashboardViewModelTests.cs](../src/OmenCoreApp.Tests/ViewModels/DashboardViewModelTests.cs).
- [x] Release-gate coverage now locks in tray/popup render-state dedupe hooks in [src/OmenCoreApp.Tests/ReleaseGateCodeHygieneTests.cs](../src/OmenCoreApp.Tests/ReleaseGateCodeHygieneTests.cs).
- [x] Bounded diagnostics now include triage-friendly classifications (amplification class, acceptance class, cache-hit class, CPU window class) and runtime-state summary fields (cadence reason, low-overhead mode, fan control state) in [src/OmenCoreApp/Services/Diagnostics/DiagnosticExportService.cs](../src/OmenCoreApp/Services/Diagnostics/DiagnosticExportService.cs).
- [x] Tray and quick popup now normalize incoming fan/performance mode labels before UI updates and skip redundant no-op state pushes in [src/OmenCoreApp/Utils/TrayIconService.cs](../src/OmenCoreApp/Utils/TrayIconService.cs) and [src/OmenCoreApp/Views/QuickPopupWindow.xaml.cs](../src/OmenCoreApp/Views/QuickPopupWindow.xaml.cs).
- [x] RGB regression coverage now verifies payload-form effect filtering for breathing payloads and off requests in [src/OmenCoreApp.Tests/Services/RgbManagerTests.cs](../src/OmenCoreApp.Tests/Services/RgbManagerTests.cs).

## Validation
- [x] Syntax checks passed on the touched runtime files.
- [x] Post-RC1 Windows app build passed after CPU authority and RGB fanout hardening: `dotnet build src/OmenCoreApp/OmenCoreApp.csproj -c Debug --no-restore`.
- [x] Post-RC1 focused runtime tests passed after CPU authority and RGB fanout hardening: **21 passed, 0 failed**.
- [x] Focused regression tests added for fan-mode identity, hotkey cycle resolution, curve ordering, and battery cache behavior.
- [x] `dotnet build src/OmenCoreApp/OmenCoreApp.csproj -c Debug --no-restore` passed after the runtime telemetry fanout removal and summary delta-gating edits.
- [x] `dotnet build src/OmenCore.Linux/OmenCore.Linux.csproj -c Debug --no-restore` passed after removing the final JSON config path.
- [x] Focused test execution passed for [src/OmenCoreApp.Tests/ViewModels/MainViewModelTests.cs](../src/OmenCoreApp.Tests/ViewModels/MainViewModelTests.cs) and [src/OmenCoreApp.Tests/Services/RuntimeUiPerformanceCountersTests.cs](../src/OmenCoreApp.Tests/Services/RuntimeUiPerformanceCountersTests.cs).
- [x] Post-hardening targeted regression run passed: **17 passed, 0 failed** (dashboard/general suppression + counters + release-gate tests).
- [x] Post-hardening Linux build validation passed (`OmenCore.Linux.csproj`, Debug, no-restore).
- [ ] Full Windows suite rerun should be executed in the final RC operator environment before release tag.

## Known Limitations (RC)
- The thermal-limit saturation + low-temperature power-lock anomaly remains a field intake item until reproduced or disproven by Scenario A evidence.
- Windows scaling and DPI behavior (100/125/150/175 + multi-monitor) still require explicit manual sign-off coverage.
- Larger fan-control architecture changes remain deferred to v3.7.0 to keep v3.6.2 low risk.

## Notes
- This release intentionally avoids broad UI refactors.
- Remaining follow-up work should stay centered on hidden/minimized projection suppression, remaining tray/popup refresh cost, and focused-window render load reduction.

## Follow-up Field Evidence
- [x] New field report confirms severe UI frame collapse persists in some real-world sessions ("UI runs at 0.3fps").
- [x] This is tracked as a top-tier architecture/performance investigation, not a cosmetic issue.
- [x] Root-cause audit and decoupling plan documented in [docs/3.7.0-UI-PERFORMANCE-AUDIT.md](3.7.0-UI-PERFORMANCE-AUDIT.md).

## Additional Field Intake (Thermal/Power Anomaly)
- [x] New community reports indicate a rare state where CPU package power appears capped (around 20-30W) while thermal-limit indicators report 100% at only 40-50C.
- [x] Affected users described behavior during/after custom fan preset use in older builds (notably 3.2.5 and 3.4.0), including occasional settings resets and intermittent fan-control instability.
- [x] Reports are currently treated as field evidence, not yet reproduced as a confirmed 3.6.2 regression.
- [x] Validation impact for v3.6.2 RC: prioritize thermal-limit sanity checks (reported thermal-limit reason vs measured temperature/power) and custom-preset persistence/stability scenarios in [docs/3.6.2-RC-VALIDATION.md](3.6.2-RC-VALIDATION.md).
- [x] Triage impact: if thermal-limit reason saturates at low temperature, capture bounded snapshot + HWiNFO evidence and tag the scenario in [docs/3.6.2-PERFORMANCE-TRIAGE.md](3.6.2-PERFORMANCE-TRIAGE.md).
- [x] One-pass execution runbook added for operators: [docs/3.6.2-SCENARIO-A-OPERATOR-RUNBOOK.md](3.6.2-SCENARIO-A-OPERATOR-RUNBOOK.md).

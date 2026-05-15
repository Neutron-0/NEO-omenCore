# OmenCore v3.6.1 Changelog (Stabilization)

## Architecture Remediation Status
- v3.6.1 is a focused stabilization release for the v3.6.0 regression baseline.
- Symptom-level stabilization has delivered targeted reliability gains (mode sync hardening, tray/OSD consistency, EC write serialization slices, lifecycle tests, WMI fan CPU reduction).
- Remaining architecture consolidation is intentionally deferred to post-3.6.1 work unless new field evidence identifies a hard release blocker.
- New architecture baseline document added: [docs/3.6.1-RUNTIME-ARCHITECTURE-MAP.md](3.6.1-RUNTIME-ARCHITECTURE-MAP.md), including:
	- runtime ownership map
	- polling ownership map
	- EC ownership map
	- timer ownership map
	- dispatcher load map
	- architecture reduction phases (runtime state engine, polling coordinator, EC operation coordinator, MainViewModel reduction)

## Fixed
- [x] Fan/performance synchronization desync for linked mode behavior.
	Root cause: fan and performance transitions had multiple independent UI-side writers with no guarded bidirectional sync path.
	Fix: introduced guarded bidirectional mapping in [src/OmenCoreApp/ViewModels/MainViewModel.cs](../src/OmenCoreApp/ViewModels/MainViewModel.cs) so linked mode updates are processed through service events without recursive ping-pong.
	Source: Field evidence pack — cohort A (ProductId 8BCD, OMEN 16-xd0xxx; logs OmenCore_20260511_175355.log and OmenCore_20260511_183927.log) and cohort B (ProductId 88F7; message.txt). Sidebar tiles showing mismatched Performance/Fan states and fan card selection highlight inconsistencies. See [docs/3.6.1-FIELD-EVIDENCE.md §A](3.6.1-FIELD-EVIDENCE.md).
- [x] Hotkey fan cycle mismatch.
	Root cause: fan hotkey cycle used a three-mode set that did not match product requirements.
	Fix: updated cycle to Auto -> Gaming -> Extreme -> Custom -> Quiet in [src/OmenCoreApp/ViewModels/MainViewModel.cs](../src/OmenCoreApp/ViewModels/MainViewModel.cs).
	Source: Field evidence pack — cohort A (ProductId 8BCD); architecture audit of hotkey action routing. See [docs/3.6.1-FIELD-EVIDENCE.md §A](3.6.1-FIELD-EVIDENCE.md).
- [x] Missing Max Fan hotkey.
	Root cause: no dedicated hotkey action/event path existed for max cooling.
	Fix: added Ctrl+Shift+M action/event wiring in [src/OmenCoreApp/Services/HotkeyService.cs](../src/OmenCoreApp/Services/HotkeyService.cs) and handler in [src/OmenCoreApp/ViewModels/MainViewModel.cs](../src/OmenCoreApp/ViewModels/MainViewModel.cs).
	Source: Architecture audit; product requirement gap identified during v3.6.1 remediation review. See [docs/3.6.1-REMEDIATION-TODO.md §Hotkeys](3.6.1-REMEDIATION-TODO.md).
- [x] OSD fan mode stale/incorrect updates during some mode transitions.
	Root cause: OSD was updated for performance mode events but not consistently updated from fan preset applied events.
	Fix: fan applied event now updates OSD fan mode in [src/OmenCoreApp/ViewModels/MainViewModel.cs](../src/OmenCoreApp/ViewModels/MainViewModel.cs).
	Source: Field evidence pack — cohort A/B overlay screenshots showing stale mode information lagging behind fan card changes; RTSS compatibility mode active in message.txt. See [docs/3.6.1-FIELD-EVIDENCE.md §K](3.6.1-FIELD-EVIDENCE.md).
- [x] Fan runtime state could leak ghost `Manual` / `Custom (Applied)` identities into hotkeys, tray, and OSD, while OSD mode rows could duplicate the same performance label twice.
	Root cause: [src/OmenCoreApp/ViewModels/FanControlViewModel.cs](../src/OmenCoreApp/ViewModels/FanControlViewModel.cs) manufactured UI-only preset names for custom/manual flows, and [src/OmenCoreApp/ViewModels/MainViewModel.cs](../src/OmenCoreApp/ViewModels/MainViewModel.cs) plus [src/OmenCoreApp/Views/OsdOverlayWindow.xaml.cs](../src/OmenCoreApp/Views/OsdOverlayWindow.xaml.cs) still consumed raw preset/event labels instead of the canonical runtime fan mode.
	Fix: manual/custom curves now publish canonical runtime mode `Custom` from [src/OmenCoreApp/Services/FanService.cs](../src/OmenCoreApp/Services/FanService.cs), the fan page no longer exposes a ghost built-in `Manual` preset or `Custom (Applied)` name in [src/OmenCoreApp/ViewModels/FanControlViewModel.cs](../src/OmenCoreApp/ViewModels/FanControlViewModel.cs), MainViewModel now prefers the service-owned current mode over raw preset names in [src/OmenCoreApp/ViewModels/MainViewModel.cs](../src/OmenCoreApp/ViewModels/MainViewModel.cs), and duplicate OSD current-mode rows are suppressed in [src/OmenCoreApp/Views/OsdOverlayWindow.xaml.cs](../src/OmenCoreApp/Views/OsdOverlayWindow.xaml.cs).
	Source: 2026-05-13 RC field evidence and final release-hardening pass targeting fan preset correctness, hotkey state coherence, runtime synchronization, and OSD/UI consistency.
- [x] Undervolt capability was shown as actionable when runtime MSR/SMU backends were not ready.
	Root cause: capability gating relied on coarse availability/warning parsing and driver presence without strict runtime readiness state.
	Fix: added explicit runtime readiness metadata in [src/OmenCoreApp/Models/UndervoltStatus.cs](../src/OmenCoreApp/Models/UndervoltStatus.cs), provider probe hard-gates in [src/OmenCoreApp/Hardware/CpuUndervoltProvider.cs](../src/OmenCoreApp/Hardware/CpuUndervoltProvider.cs) and [src/OmenCoreApp/Hardware/AmdUndervoltProvider.cs](../src/OmenCoreApp/Hardware/AmdUndervoltProvider.cs), preflight enforcement in [src/OmenCoreApp/Services/UndervoltService.cs](../src/OmenCoreApp/Services/UndervoltService.cs), and UI command gating updates in [src/OmenCoreApp/ViewModels/SystemControlViewModel.cs](../src/OmenCoreApp/ViewModels/SystemControlViewModel.cs) and [src/OmenCoreApp/ViewModels/MainViewModel.cs](../src/OmenCoreApp/ViewModels/MainViewModel.cs).
	Source: Field evidence pack — cohort A (ProductId 8BCD, OMEN 16-xd0xxx): PawnIO MSR write failed HRESULT 0x80070002; AMD all-core CO SMU did not respond. PawnIO driver detected but MSR unavailable in same session. Reported via support thread logs OmenCore_20260511_175355.log. See [docs/3.6.1-FIELD-EVIDENCE.md §J](3.6.1-FIELD-EVIDENCE.md).
- [x] Fan smoothing startup timing accepted transient RPM changes too early in monitor-loop warmup.
	Root cause: [src/OmenCoreApp/Services/FanService.cs](../src/OmenCoreApp/Services/FanService.cs) seeded an initial read in `Start()` and then immediately entered monitor-loop processing without waiting one poll interval, compressing confirmation windows.
	Fix: added an initial poll-aligned delay at monitor-loop startup so smoothing confirmation behavior is deterministic and consistent with configured poll cadence.
	Source: Code audit during v3.6.1 architecture remediation; Max fan reliability reports in field evidence cohort B. See [docs/3.6.1-FIELD-EVIDENCE.md §B](3.6.1-FIELD-EVIDENCE.md).
- [x] Repeated Max-cooling toggles could generate avoidable hardware-write churn while still lacking delayed confirmation signal.
	Root cause: [src/OmenCoreApp/Services/FanService.cs](../src/OmenCoreApp/Services/FanService.cs) always executed Max apply operations even when already in Max mode, and verification feedback was immediate-only.
	Fix: added idempotent Max short-circuiting for duplicate direct Max requests, preserved forced apply for preset-driven Max transitions, and introduced deferred post-transition Max verification logging.
	Test coverage: Added 3 new unit tests in [src/OmenCoreApp.Tests/Services/FanPresetVerificationTests.cs](../src/OmenCoreApp.Tests/Services/FanPresetVerificationTests.cs) for idempotent behavior validation.
	Source: Code audit (OmenMon-Reborn parity hardening); field evidence cohort A/B Max fan reliability reports (WMI V1 backend, Max level 55 scalar path). See [docs/3.6.1-FIELD-EVIDENCE.md §B](3.6.1-FIELD-EVIDENCE.md).
- [x] Thermal protection safety-critical behavior lacked systematic unit test coverage.
	Root cause: thermal emergency/warning thresholds, debounce timers, rate-limiting, and hysteresis logic were complex but had only partial indirect testing.
	Fix: added comprehensive [src/OmenCoreApp.Tests/Services/ThermalProtectionTests.cs](../src/OmenCoreApp.Tests/Services/ThermalProtectionTests.cs) with 7 focused unit tests covering emergency/warning activation, debounce timers, EC write rate-limiting, invalid temperature handling, and hysteresis-based release behavior.
	Test coverage: 7/7 new thermal protection tests passing; included in full fan test validation (38+ related tests passing with zero regressions).
	Source: Code audit; proactive safety coverage gap identified during remediation review. No specific user report; driven by risk assessment of EC write paths.
- [x] Hotkey pending-update lifecycle could preserve stale key bindings before window initialization.
	Root cause: [src/OmenCoreApp/Services/HotkeyService.cs](../src/OmenCoreApp/Services/HotkeyService.cs) `UpdateHotkey` only replaced active registrations and did not replace queued pending bindings for the same action.
	Fix: `UpdateHotkey` now removes pending entries for the target action before re-registering, and global unregister/dispose now clears pending queues to avoid stale deferred registrations.
	Test coverage: added regression tests in [src/OmenCoreApp.Tests/Services/HotkeyServiceTests.cs](../src/OmenCoreApp.Tests/Services/HotkeyServiceTests.cs) for pending replacement and pending-queue reset behavior.
	Source: Code audit; hotkey flakiness pattern noted in v3.6.0 changelog note ("Hotkey registration now de-duplicates queued actions") and field evidence cohort A OmenCore startup logs showing registration timing issues.
- [x] OSD hotkey retry lifecycle could create overlapping timers and silently hide unregister cleanup failures.
	Root cause: retry timer creation in [src/OmenCoreApp/Services/OsdService.cs](../src/OmenCoreApp/Services/OsdService.cs) did not dispose an existing timer first, and cleanup used a silent broad catch.
	Fix: retry initialization now replaces any existing timer before starting a new retry cycle, and hotkey cleanup now logs warning details on exception.
	Source: Code audit; OSD stale/frozen state reports in field evidence cohort A/B (focus/minimize transitions). See [docs/3.6.1-FIELD-EVIDENCE.md §K](3.6.1-FIELD-EVIDENCE.md).
- [x] Linux systems without hp-wmi/acpi thermal profile sysfs interfaces could not apply performance mode changes.
	Root cause: [src/OmenCore.Avalonia/Services/LinuxHardwareService.cs](../src/OmenCore.Avalonia/Services/LinuxHardwareService.cs) only attempted sysfs thermal profile writes and had no secondary control backend.
	Fix: added `powerprofilesctl` fallback for get/set performance mode when direct thermal sysfs interfaces are unavailable or unwritable.
	Source: User report via Discord — Linux board class 8E35 ("Unable to set performance modes, I get an error saying performance mode unavailable"). See [docs/3.6.1-FIELD-EVIDENCE.md](3.6.1-FIELD-EVIDENCE.md).
- [x] Linux GitHub quick action could emit repeated `xdg-open` stderr lines in terminal and fail silently.
	Root cause: URL launch path relied on shell opening with inconsistent Linux behavior and non-validated fallback invocation.
	Fix: hardened Linux URL launcher logic in [src/OmenCore.Avalonia/ViewModels/MainWindowViewModel.cs](../src/OmenCore.Avalonia/ViewModels/MainWindowViewModel.cs) and [src/OmenCore.Avalonia/ViewModels/SettingsViewModel.cs](../src/OmenCore.Avalonia/ViewModels/SettingsViewModel.cs) to use quiet non-shell launchers with exit validation and fallback sequencing.
	Source: User report via Discord — Linux board class 8E35 ("click on github menu results in 4 lines of error output").
- [x] Linux Fan Control navigation visibility could disappear on non-manual fan-control systems.
	Root cause: sidebar visibility was tied only to manual fan-control capability flag, even when telemetry/profile-only paths were available.
	Fix: Linux navigation now keeps Fan Control visible in [src/OmenCore.Avalonia/ViewModels/MainWindowViewModel.cs](../src/OmenCore.Avalonia/ViewModels/MainWindowViewModel.cs), while capability warning text continues to describe control limitations.
	Source: User report via Discord — Linux board class 8E35 ("Unable to set fan controls… menu is absent most of the time").
- [x] Linux hp-wmi profile detection failed on systems exposing hyphenated sysfs names (for example `thermal-profile`) instead of underscore names.
	Root cause: profile path detection in Linux UI/CLI/controller paths used underscore-only checks (`thermal_profile`, `platform_profile`, `*_choices`) and missed hyphen variants.
	Fix: added hyphen-path fallbacks across Linux profile detection/read/write and diagnostics in [src/OmenCore.Avalonia/Services/LinuxHardwareService.cs](../src/OmenCore.Avalonia/Services/LinuxHardwareService.cs), [src/OmenCore.Linux/Hardware/LinuxEcController.cs](../src/OmenCore.Linux/Hardware/LinuxEcController.cs), [src/OmenCore.Linux/Commands/DiagnoseCommand.cs](../src/OmenCore.Linux/Commands/DiagnoseCommand.cs), and [src/OmenCore.Linux/Commands/StatusCommand.cs](../src/OmenCore.Linux/Commands/StatusCommand.cs).
	Source: Discord field report (board-specific hp-wmi sysfs naming with hyphenated profile files and missing real-time profile detection).
- [x] Linux capability classification could incorrectly report `full-control` when only `hp-wmi/hwmon/pwm*_enable` policy toggles were present, even without writable fan target/output interfaces.
	Root cause: [src/OmenCore.Linux/Hardware/LinuxCapabilityClassifier.cs](../src/OmenCore.Linux/Hardware/LinuxCapabilityClassifier.cs) treated `hasHwmonFanAccess` as manual fan control, which promoted capability class to `full-control` for boards lacking actual per-fan write paths.
	Fix: narrowed manual-fan classification to EC/fan-output/fan-target interfaces only; hwmon `pwm_enable` now contributes to profile/coarse control classification unless writable fan target/output paths exist.
	Source: GitHub issue [#127](https://github.com/theantipopau/omencore/issues/127) (OMEN 16-ap0xxx, board 8D26, Linux).
- [x] Linux troubleshooting lacked explicit guidance for multiplex fallback and board-dependent dkms automation experiments when hp-wmi exists but no profile controls are exposed.
	Root cause: install and diagnose guidance covered hp-wmi/acpi basics but did not explicitly call out `hp_wmi.force_multiplex=1` or the user-confirmed “often automates setup” AUR `hp-omen-gaming-wmi-dkms` path with proper caveats.
	Fix: added guidance to [INSTALL.md](../INSTALL.md), [docs/LINUX_INSTALL_GUIDE.md](LINUX_INSTALL_GUIDE.md), and diagnose recommendations in [src/OmenCore.Linux/Commands/DiagnoseCommand.cs](../src/OmenCore.Linux/Commands/DiagnoseCommand.cs).
	Source: Discord follow-up (Eric [GOG] + Loco Motivo) about AUR dkms attempts and mixed results.
- [x] Runtime polling coordinator could drop the first window-hidden cadence transition after startup.
	Root cause: [src/OmenCoreApp/Services/RuntimePollingCoordinator.cs](../src/OmenCoreApp/Services/RuntimePollingCoordinator.cs) initialized its local `UiWindowActive` cache to `false` while [src/OmenCoreApp/Services/HardwareMonitoringService.cs](../src/OmenCoreApp/Services/HardwareMonitoringService.cs) defaulted to `true`, causing the first `SetUiWindowActive(false)` call to be deduped away.
	Fix: initialize coordinator `UiWindowActive` cache to `true` and keep cadence writes centralized through coordinator-owned transitions.
	Source: New coordinator unit tests in [src/OmenCoreApp.Tests/Services/RuntimePollingCoordinatorTests.cs](../src/OmenCoreApp.Tests/Services/RuntimePollingCoordinatorTests.cs).
- [x] EC-heavy fan-cleaning write paths could execute without centralized runtime coordination.
	Root cause: [src/OmenCoreApp/Services/FanCleaningService.cs](../src/OmenCoreApp/Services/FanCleaningService.cs) executed direct EC register write sequences independently, without a shared runtime coordinator section for cross-service ordering.
	Fix: introduced [src/OmenCoreApp/Services/RuntimeEcOperationCoordinator.cs](../src/OmenCoreApp/Services/RuntimeEcOperationCoordinator.cs), then routed fan-cleaning EC write sequences (`EnableMaxFanViaEc`, `RestoreViaEc`) through coordinator-owned serialized execution.
	Source: architecture remediation pass for EC contention reduction.
- [x] Performance mode EC power-limit writes had no cross-service serialization gate, allowing race conditions with fan-cleaning EC sequences during simultaneous mode changes.
	Root cause: [src/OmenCoreApp/Services/PerformanceModeService.cs](../src/OmenCoreApp/Services/PerformanceModeService.cs) called `_powerLimitController.ApplyPerformanceLimits()` without going through the shared EC coordinator, so concurrent fan-cleaning and mode-change requests could interleave EC register writes.
	Fix: wrapped `ApplyPerformanceLimits` call in `_ecOperationCoordinator.Execute("PerformanceModeService", "ApplyPerformanceLimits", ...)` inside the existing `_applyLock` section; coordinator injected via optional constructor parameter (self-creating fallback); shared coordinator instance passed from [src/OmenCoreApp/ViewModels/MainViewModel.cs](../src/OmenCoreApp/ViewModels/MainViewModel.cs).
	Source: EC coordinator second migration slice.
- [x] Fan service controller writes were only serialized per-service, but still lacked shared cross-service EC ordering with performance-mode and fan-cleaning operations.
	Root cause: [src/OmenCoreApp/Services/FanService.cs](../src/OmenCoreApp/Services/FanService.cs) used a local `_fanWriteLock` for intra-service ordering but invoked controller writes without the shared runtime EC coordinator gate.
	Fix: injected [src/OmenCoreApp/Services/RuntimeEcOperationCoordinator.cs](../src/OmenCoreApp/Services/RuntimeEcOperationCoordinator.cs) into [src/OmenCoreApp/Services/FanService.cs](../src/OmenCoreApp/Services/FanService.cs) and wrapped serialized write helpers in coordinator sections; passed shared instance from [src/OmenCoreApp/ViewModels/MainViewModel.cs](../src/OmenCoreApp/ViewModels/MainViewModel.cs).
	Source: EC coordinator third migration slice.
- [x] Keyboard EC RGB writes could still bypass shared cross-service EC ordering during lighting updates.
	Root cause: [src/OmenCoreApp/Services/KeyboardLightingService.cs](../src/OmenCoreApp/Services/KeyboardLightingService.cs) performed direct EC RGB register writes in `SetZoneColorViaEc` without the shared runtime EC coordinator gate.
	Fix: injected [src/OmenCoreApp/Services/RuntimeEcOperationCoordinator.cs](../src/OmenCoreApp/Services/RuntimeEcOperationCoordinator.cs) into [src/OmenCoreApp/Services/KeyboardLightingService.cs](../src/OmenCoreApp/Services/KeyboardLightingService.cs), wrapped per-zone RGB register writes in coordinator execution, and passed shared coordinator from [src/OmenCoreApp/ViewModels/MainViewModel.cs](../src/OmenCoreApp/ViewModels/MainViewModel.cs).
	Source: EC coordinator fourth migration slice.
- [x] V2 EC-direct keyboard backend and power-limit verification could still bypass shared EC ordering.
	Root cause: [src/OmenCoreApp/Services/KeyboardLighting/KeyboardLightingServiceV2.cs](../src/OmenCoreApp/Services/KeyboardLighting/KeyboardLightingServiceV2.cs), [src/OmenCoreApp/Services/KeyboardLighting/EcDirectBackend.cs](../src/OmenCoreApp/Services/KeyboardLighting/EcDirectBackend.cs), and [src/OmenCoreApp/Services/PowerVerificationService.cs](../src/OmenCoreApp/Services/PowerVerificationService.cs) retained direct EC read/write paths after the first coordinator migrations.
	Fix: passed the shared [src/OmenCoreApp/Services/RuntimeEcOperationCoordinator.cs](../src/OmenCoreApp/Services/RuntimeEcOperationCoordinator.cs) into the V2 keyboard EC backend and power verification service, wrapping EC-direct color, brightness, backlight, effect, readback, power apply, and power readback operations in coordinator-owned sections.
	Test coverage: added coordinator wait coverage in [src/OmenCoreApp.Tests/Services/EcDirectBackendCoordinatorTests.cs](../src/OmenCoreApp.Tests/Services/EcDirectBackendCoordinatorTests.cs) and expanded [src/OmenCoreApp.Tests/Services/PowerLimitControllerTests.cs](../src/OmenCoreApp.Tests/Services/PowerLimitControllerTests.cs).
	Source: GPT-5.5 architecture review EC bypass finding.
- [x] Dashboard and Settings low-overhead toggles could bypass centralized polling coordination.
	Root cause: [src/OmenCoreApp/ViewModels/DashboardViewModel.cs](../src/OmenCoreApp/ViewModels/DashboardViewModel.cs) and [src/OmenCoreApp/ViewModels/SettingsViewModel.cs](../src/OmenCoreApp/ViewModels/SettingsViewModel.cs) still called `HardwareMonitoringService.SetLowOverheadMode` directly.
	Fix: injected [src/OmenCoreApp/Services/RuntimePollingCoordinator.cs](../src/OmenCoreApp/Services/RuntimePollingCoordinator.cs) into both surfaces from [src/OmenCoreApp/ViewModels/MainViewModel.cs](../src/OmenCoreApp/ViewModels/MainViewModel.cs), preserving direct service fallback only for standalone construction/tests.
	Source: GPT-5.5 architecture review polling bypass finding.
- [x] Aggregate test/runtime file-system paths could fail when temp config or LocalAppData directories disappeared during a session.
	Root cause: [src/OmenCoreApp/Services/ConfigurationService.cs](../src/OmenCoreApp/Services/ConfigurationService.cs) created the config directory only at construction time, and [src/OmenCoreApp/Services/BloatwareManager/BloatwareManagerService.cs](../src/OmenCoreApp/Services/BloatwareManager/BloatwareManagerService.cs) had brittle dry-run report path assumptions plus non-defensive admin detection.
	Fix: config saves now recreate the config directory before writing; bloatware dry-run export now respects explicit `LOCALAPPDATA`, falls back to temp storage if the known folder is unavailable, uses unique report filenames, and fails closed if admin detection is unavailable.
	Test coverage: added [src/OmenCoreApp.Tests/Services/ConfigurationServiceTests.cs](../src/OmenCoreApp.Tests/Services/ConfigurationServiceTests.cs) and revalidated the full app test project.
	Source: full-suite validation after GPT-5.5 architecture reduction pass.

## Changed
- [x] Mode-change orchestration now favors service-event synchronization over optimistic UI-only state writes in linked fan/performance flows.
- [x] Hotkey performance/boost/quiet handlers now apply service mode changes without forcing local mode fields, preventing temporary UI drift under delayed hardware confirmation in [src/OmenCoreApp/ViewModels/MainViewModel.cs](../src/OmenCoreApp/ViewModels/MainViewModel.cs).
- [x] Performance hotkey cycling now normalizes alias states (for example Silent/Turbo) into canonical Balanced/Performance/Quiet before selecting the next mode, preventing mis-ordered cycles in [src/OmenCoreApp/ViewModels/MainViewModel.cs](../src/OmenCoreApp/ViewModels/MainViewModel.cs).
- [x] Tray, automation, and game-profile built-in mode handling now share canonical alias resolution and fan default-curve construction via [src/OmenCoreApp/Models/PerformanceModeNameResolver.cs](../src/OmenCoreApp/Models/PerformanceModeNameResolver.cs) and [src/OmenCoreApp/Models/FanModeNameResolver.cs](../src/OmenCoreApp/Models/FanModeNameResolver.cs), reducing drift between Silent/Quiet, Turbo/Performance, and built-in preset defaults.

## Removed
- [x] Removed MainViewModel-local tray command queue state and worker lifecycle fields from [src/OmenCoreApp/ViewModels/MainViewModel.cs](../src/OmenCoreApp/ViewModels/MainViewModel.cs) (`_pendingTrayAction`, `_pendingTrayActionName`, `_trayActionWorkerRunning`, `_trayWorkerCts`, and queue-loop method), replacing the legacy in-viewmodel orchestration path with a dedicated runtime service.
- [x] Removed direct per-hotkey dispatcher scheduling ownership from [src/OmenCoreApp/ViewModels/MainViewModel.cs](../src/OmenCoreApp/ViewModels/MainViewModel.cs) handlers by routing hotkey UI actions through [src/OmenCoreApp/Services/RuntimeHotkeyCoordinator.cs](../src/OmenCoreApp/Services/RuntimeHotkeyCoordinator.cs).
- [x] Removed obsolete direct monitoring visibility cadence writes from [src/OmenCoreApp/ViewModels/MainViewModel.cs](../src/OmenCoreApp/ViewModels/MainViewModel.cs) `UpdateMonitoringVisibilityCadence`; this path now requires [src/OmenCoreApp/Services/RuntimePollingCoordinator.cs](../src/OmenCoreApp/Services/RuntimePollingCoordinator.cs) ownership.

## Refactored
- [x] Refactored hotkey and service-event synchronization pipeline with explicit loop guard helpers and canonical fan/performance mapping helpers in [src/OmenCoreApp/ViewModels/MainViewModel.cs](../src/OmenCoreApp/ViewModels/MainViewModel.cs).
- [x] Removed duplicated built-in fan curve and performance alias normalization logic from [src/OmenCoreApp/ViewModels/MainViewModel.cs](../src/OmenCoreApp/ViewModels/MainViewModel.cs), [src/OmenCoreApp/Services/PowerAutomationService.cs](../src/OmenCoreApp/Services/PowerAutomationService.cs), and [src/OmenCoreApp/Utils/TrayIconService.cs](../src/OmenCoreApp/Utils/TrayIconService.cs) in favor of shared resolvers.
- [x] Extracted tray quick-action latest-wins dispatch orchestration into [src/OmenCoreApp/Services/RuntimeCommandDispatcher.cs](../src/OmenCoreApp/Services/RuntimeCommandDispatcher.cs) and rewired [src/OmenCoreApp/ViewModels/MainViewModel.cs](../src/OmenCoreApp/ViewModels/MainViewModel.cs) to use this service as an intent dispatcher instead of owning queue scheduling internals.
- [x] Expanded runtime-dispatcher race coverage in [src/OmenCoreApp.Tests/Services/RuntimeCommandDispatcherTests.cs](../src/OmenCoreApp.Tests/Services/RuntimeCommandDispatcherTests.cs) with a concurrent-producer latest-wins scenario, validating deterministic post-burst winner selection under overlapping enqueue pressure.
- [x] Added sustained dispatcher congestion/drift coverage in [src/OmenCoreApp.Tests/Services/RuntimeCommandDispatcherTests.cs](../src/OmenCoreApp.Tests/Services/RuntimeCommandDispatcherTests.cs) with a high-pressure 500-intent burst scenario that asserts bounded latest-wins execution behavior (running command + deterministic final intent only).
- [x] Added cross-surface runtime intent-race integration coverage in [src/OmenCoreApp.Tests/Services/RuntimeIntentDispatchIntegrationTests.cs](../src/OmenCoreApp.Tests/Services/RuntimeIntentDispatchIntegrationTests.cs), validating convergence when tray-dispatcher, hotkey-coordinator, and automation apply overlapping performance-mode intents.
- [x] Added MainViewModel UI-surface runtime intent overlap coverage in [src/OmenCoreApp.Tests/ViewModels/MainViewModelTests.cs](../src/OmenCoreApp.Tests/ViewModels/MainViewModelTests.cs) with `RuntimeIntentOverlap_TrayHotkeyAutomation_ConvergesToFinalTrayMode`, validating tray + hotkey + automation convergence to an explicit final tray intent without forcing SystemControl lazy-load side effects, and hardening deterministic assertion via overlap-wave settle barrier before final-intent apply.
- [x] Added repeated-burst long-runtime drift coverage in [src/OmenCoreApp.Tests/Services/RuntimeCommandDispatcherTests.cs](../src/OmenCoreApp.Tests/Services/RuntimeCommandDispatcherTests.cs), validating latest-wins convergence and bounded execution under 20 sequential contention rounds.
- [x] Extracted hotkey action orchestration into [src/OmenCoreApp/Services/RuntimeHotkeyCoordinator.cs](../src/OmenCoreApp/Services/RuntimeHotkeyCoordinator.cs), so [src/OmenCoreApp/ViewModels/MainViewModel.cs](../src/OmenCoreApp/ViewModels/MainViewModel.cs) now dispatches intent while coordinator-owned latest-wins scheduling handles overlapping hotkey triggers.
- [x] Introduced a first centralized polling coordinator slice in [src/OmenCoreApp/Services/RuntimePollingCoordinator.cs](../src/OmenCoreApp/Services/RuntimePollingCoordinator.cs) and routed MainViewModel monitoring cadence mode writes (low-overhead and OSD overlay realtime) through coordinator ownership.
- [x] Extended polling-coordinator ownership for runtime window/tray cadence updates by routing App lifecycle visibility/state transitions in [src/OmenCoreApp/App.xaml.cs](../src/OmenCoreApp/App.xaml.cs) through [src/OmenCoreApp/ViewModels/MainViewModel.cs](../src/OmenCoreApp/ViewModels/MainViewModel.cs) (`UpdateMonitoringVisibilityCadence`) instead of direct `HardwareMonitoringService` mode writes.
- [x] Added first centralized EC-operation coordinator slice in [src/OmenCoreApp/Services/RuntimeEcOperationCoordinator.cs](../src/OmenCoreApp/Services/RuntimeEcOperationCoordinator.cs) and migrated [src/OmenCoreApp/Services/FanCleaningService.cs](../src/OmenCoreApp/Services/FanCleaningService.cs) EC write-heavy operations to coordinator-owned execution sections.
- [x] Introduced first architecture-reduction slice: centralized fan/performance tray projection state in [src/OmenCoreApp/Services/RuntimeStateEngine.cs](../src/OmenCoreApp/Services/RuntimeStateEngine.cs) and migrated tray fan/performance/curve display updates in [src/OmenCoreApp/App.xaml.cs](../src/OmenCoreApp/App.xaml.cs) to consume this engine as a read-only subscriber path.
- [x] Extended RuntimeStateEngine projection subscribers to OSD mode display in [src/OmenCoreApp/App.xaml.cs](../src/OmenCoreApp/App.xaml.cs), and removed duplicate direct OSD fan/performance mode writes from [src/OmenCoreApp/ViewModels/MainViewModel.cs](../src/OmenCoreApp/ViewModels/MainViewModel.cs) event handlers.
- [x] Extended RuntimeStateEngine projection to include linked-mode state in [src/OmenCoreApp/Services/RuntimeStateEngine.cs](../src/OmenCoreApp/Services/RuntimeStateEngine.cs) and migrated tray linked-indicator updates in [src/OmenCoreApp/App.xaml.cs](../src/OmenCoreApp/App.xaml.cs) to runtime-state subscription flow (removing mixed direct tray linked writes).
- [x] Extended RuntimeStateEngine projection to include latest telemetry sample state in [src/OmenCoreApp/Services/RuntimeStateEngine.cs](../src/OmenCoreApp/Services/RuntimeStateEngine.cs), and migrated tray monitoring sample updates in [src/OmenCoreApp/App.xaml.cs](../src/OmenCoreApp/App.xaml.cs) to runtime-state subscription flow (reducing direct tray state push paths).

## Performance
- [x] Dashboard low-overhead mode now skips hidden thermal-history, sparkline, and fan-curve maintenance in [src/OmenCoreApp/ViewModels/DashboardViewModel.cs](../src/OmenCoreApp/ViewModels/DashboardViewModel.cs), reducing UI-thread work when graphs are disabled.
- [x] Reduced high CPU during stable WMI fan curve/hold sessions in [src/OmenCoreApp/Hardware/WmiFanController.cs](../src/OmenCoreApp/Hardware/WmiFanController.cs).
	Root cause: skipped countdown keepalive ticks still called `ExtendFanCountdown()`, and that helper reissued fan-level WMI writes. Combined with a 0.8s timer and 2.5s manual reapply threshold, low-duty custom curves could keep writing the same fan level every few seconds for thousands of writes.
	Fix: bounded the countdown timer cadence, made skipped manual/preset keepalive ticks true no-ops, throttled low-duty manual curve hold reapply to 15s, retained a 5s high-duty safety cadence, throttled preset-mode reapply to 30s, added a 15s throttle around remaining countdown-extension WMI writes, and stopped normal manual curve writes from performing an extra pre-write RPM snapshot purely for command-history metadata.
	Source: 2026-05-13 high-CPU field report and `message(1).txt` showing repeated same-target WMI fan writes and `[FanKeepalive]` write counts above 4800. See [docs/3.6.1-FIELD-EVIDENCE.md Â§O](3.6.1-FIELD-EVIDENCE.md).
- [x] Reduced repeated CPU temperature fallback retry pressure in [src/OmenCoreApp/Hardware/WmiBiosMonitor.cs](../src/OmenCoreApp/Hardware/WmiBiosMonitor.cs).
	Root cause: when LibreHardwareMonitor fallback temperature reads timed out, the monitor retried on a fixed 30s cadence. In sessions with HWiNFO/other sensor contention or a stuck fallback provider, this could create repeated timeout work.
	Fix: repeated fallback timeouts now use capped backoff (30s -> 60s -> 120s -> 240s -> 300s), resetting after a valid fallback read.
	Test coverage: added [src/OmenCoreApp.Tests/Hardware/WmiBiosMonitorFallbackTests.cs](../src/OmenCoreApp.Tests/Hardware/WmiBiosMonitorFallbackTests.cs).
	Source: 2026-05-13 high-CPU field log showing recurring `[WmiBiosMonitor] CPU temp fallback timed out after 500ms` entries. See [docs/3.6.1-FIELD-EVIDENCE.md Â§O](3.6.1-FIELD-EVIDENCE.md).
- [x] Bounded automatic post-apply fan curve verification in [src/OmenCoreApp/ViewModels/FanControlViewModel.cs](../src/OmenCoreApp/ViewModels/FanControlViewModel.cs).
	Root cause: applying a saved/custom curve could run a diagnostic fan verification sequence across both fans, then reapply the curve. On slow or unresponsive firmware this could last tens of seconds and amplify WMI/CPU load immediately after a preset apply.
	Fix: automatic verification now checks only one fan, uses a 15s cancellation timeout, and skips repeated same-target verification for 10 minutes while preserving explicit diagnostic/calibration paths.
	Test coverage: added cooldown regression coverage in [src/OmenCoreApp.Tests/ViewModels/FanControlViewModelTests.cs](../src/OmenCoreApp.Tests/ViewModels/FanControlViewModelTests.cs) and expanded WMI keepalive/countdown-extension behavior coverage in [src/OmenCoreApp.Tests/Hardware/WmiV2VerificationTests.cs](../src/OmenCoreApp.Tests/Hardware/WmiV2VerificationTests.cs).
	Source: 2026-05-13 high-CPU field report and curve verification log sequence in `message(1).txt`. See [docs/3.6.1-FIELD-EVIDENCE.md Â§O](3.6.1-FIELD-EVIDENCE.md).
- [x] Added freeze-recovery restart throttling in [src/OmenCoreApp/Services/HardwareMonitoringService.cs](../src/OmenCoreApp/Services/HardwareMonitoringService.cs).
	Root cause: sustained flat CPU/GPU telemetry on unsupported or degraded sensor paths could repeatedly satisfy freeze-restart criteria, producing bridge restart churn.
	Fix: bridge restart after freeze detection now uses a cooldown/backoff gate and a bounded restart budget window, with suppression logging while preserving normal recovery behavior.
	Test coverage: added [src/OmenCoreApp.Tests/Services/HotkeyAndMonitoringTests.cs](../src/OmenCoreApp.Tests/Services/HotkeyAndMonitoringTests.cs) (`CheckAndRecoverFrozenTemps_RateLimitsBridgeRestart_WhenTempsStayFrozen`).
	Source: 2026-05-13 field logs showing repeated "Both CPU and GPU temps frozen" restart cycles.

- [x] General tab telemetry no longer owns an independent 1s polling timer.
	Root cause: [src/OmenCoreApp/ViewModels/GeneralViewModel.cs](../src/OmenCoreApp/ViewModels/GeneralViewModel.cs) retained its own `DispatcherTimer` that polled fan/temperature/telemetry projection state even though the runtime already emits normalized monitoring samples.
	Fix: removed the GeneralViewModel timer and updated `UpdateFromMonitoringSample` to carry fan RPM/percent, temperatures, load, power, and memory projection from the centralized monitoring stream.
	Test coverage: added [src/OmenCoreApp.Tests/ViewModels/GeneralViewModelTests.cs](../src/OmenCoreApp.Tests/ViewModels/GeneralViewModelTests.cs) and a release-gate assertion preventing the timer from returning.
	Source: final release-hardening timer/polling audit and "10hz feel" responsiveness risk review.
- [x] System control EC GPU boost fallback no longer bypasses shared EC serialization.
	Root cause: [src/OmenCoreApp/ViewModels/SystemControlViewModel.cs](../src/OmenCoreApp/ViewModels/SystemControlViewModel.cs) retained direct EC read/write/readback sequences for model-specific GPU boost fallback paths.
	Fix: passed the shared RuntimeEcOperationCoordinator from MainViewModel into SystemControlViewModel and routed detection, apply, and startup reapply EC register operations through coordinator-owned sections.
	Source: final EC bypass audit for partial-migration risk.
- [x] Diagnostic support-bundle EC register reads now share the runtime EC coordination gate.
	Root cause: [src/OmenCoreApp/Services/Diagnostics/DiagnosticExportService.cs](../src/OmenCoreApp/Services/Diagnostics/DiagnosticExportService.cs) captured EC diagnostic reads directly during export, which could overlap normal fan/performance/keyboard EC operations under load.
	Fix: injected RuntimeEcOperationCoordinator into DiagnosticExportService and wrapped diagnostic register reads through a single helper; MainViewModel passes the shared coordinator for settings/model-report exports.
	Test coverage: added release-gate coverage preventing raw diagnostic EC read loops from returning.
	Source: final release-hardening diagnostics/EC contention audit.
- [x] Removed obsolete MainViewModel custom fan-curve apply bypass.
	Root cause: MainViewModel still exposed an unbound `ApplyFanCurveCommand` that called FanService directly, outside FanControlViewModel's validation and bounded verification path.
	Fix: removed the obsolete command and unused local apply helpers so custom curve application remains owned by FanControlViewModel.
	Test coverage: added release-gate coverage preventing the bypass from being reintroduced.
	Source: final MainViewModel reduction pass.

## Compatibility
- [x] Model identity fallback messaging now differentiates HP/non-HP and OMEN/Victus hint states for low-confidence fallback paths in [src/OmenCoreApp/Services/Diagnostics/ModelIdentityResolutionSummary.cs](../src/OmenCoreApp/Services/Diagnostics/ModelIdentityResolutionSummary.cs).
- [x] Improved low-confidence OMEN/Victus fallback wording to explicitly state when branding hints exist but exact model mapping is missing, reducing contradictory "unknown vs non-gaming" interpretation in [src/OmenCoreApp/Services/Diagnostics/ModelIdentityResolutionSummary.cs](../src/OmenCoreApp/Services/Diagnostics/ModelIdentityResolutionSummary.cs).
- [x] Linux performance profile control now degrades to `powerprofilesctl` backend when hp-wmi/acpi profile sysfs nodes are absent, improving support for boards that expose generic power profile controls but not vendor thermal profile files.

## Diagnostics
- [x] Added [docs/3.6.1-GPT55-ARCHITECTURE-REVIEW.md](3.6.1-GPT55-ARCHITECTURE-REVIEW.md), documenting current hybrid architecture health, runtime ownership findings, polling/timer findings, dispatcher findings, EC coordination findings, release blockers, and recommended pre/post-release tasks.
- [x] Added high-CPU fan-curve field evidence section in [docs/3.6.1-FIELD-EVIDENCE.md](3.6.1-FIELD-EVIDENCE.md) tying the 2026-05-13 Task Manager screenshot and `message(1).txt` logs to WMI keepalive write cadence and post-apply verification load.
- [x] Revalidated focused high-CPU mitigation subset after WMI keepalive, manual-write snapshot, curve-verification, CPU fallback, and release-hygiene changes: `WmiV2VerificationTests` + `WmiBiosMonitorFallbackTests` + `FanControlViewModelTests` + `ReleaseGateCodeHygieneTests` passing (59/59).
- [x] Revalidated full [src/OmenCoreApp.Tests/OmenCoreApp.Tests.csproj](../src/OmenCoreApp.Tests/OmenCoreApp.Tests.csproj) after the high-CPU/fallback mitigation slice: 619/619 passing in 2m42s.
- [x] Revalidated full [src/OmenCoreApp.Tests/OmenCoreApp.Tests.csproj](../src/OmenCoreApp.Tests/OmenCoreApp.Tests.csproj) after the final release-hardening consolidation slice: 623/623 passing in 1m45s.
- [x] Revalidated release-candidate build and focused validation matrix after version/package metadata alignment:
	- `dotnet build OmenCore.sln -c Release --no-restore`: PASS, 0 warnings, 0 errors.
	- Full Windows test suite: PASS, 623/623 in 3m13s.
	- Release hygiene gates: PASS, 7/7.
	- Fan/performance/OSD/tray focused suite: PASS, 115/115.
	- EC/diagnostics/WMI coordination focused suite: PASS, 61/61.
- [x] Revalidated focused runtime intent/concurrency suites after MainViewModel overlap settle-barrier hardening: `MainViewModelTests` (12/12), `RuntimeIntentDispatchIntegrationTests` (1/1), `RuntimeCommandDispatcherTests` (6/6), and `RuntimeHotkeyCoordinatorTests` (2/2) all passing.
- [x] Added synchronization trace logging for linked fan->performance and performance->fan transitions in [src/OmenCoreApp/ViewModels/MainViewModel.cs](../src/OmenCoreApp/ViewModels/MainViewModel.cs).
- [x] Added structured field evidence dossier for patch planning and regression targeting: [docs/3.6.1-FIELD-EVIDENCE.md](3.6.1-FIELD-EVIDENCE.md).
- [x] Added runtime capability truth tests for undervolt visibility gating in [src/OmenCoreApp.Tests/Hardware/DeviceCapabilitiesTests.cs](../src/OmenCoreApp.Tests/Hardware/DeviceCapabilitiesTests.cs), confirming driver-present/runtime-blocked scenarios remain non-actionable in UI.
- [x] Added shared fan/performance link-mapping coverage in [src/OmenCoreApp.Tests/Models/FanPerformanceLinkMapperTests.cs](../src/OmenCoreApp.Tests/Models/FanPerformanceLinkMapperTests.cs) and expanded [src/OmenCoreApp.Tests/Hardware/DeviceCapabilitiesTests.cs](../src/OmenCoreApp.Tests/Hardware/DeviceCapabilitiesTests.cs) to cover AMD undervolt gating plus RGB/GPU-power visibility rules on unsupported or unknown-model paths.
- [x] Added OSD lifecycle regression tests in [src/OmenCoreApp.Tests/Services/OsdServiceLifecycleTests.cs](../src/OmenCoreApp.Tests/Services/OsdServiceLifecycleTests.cs) covering cached mode-state persistence, visibility transition deduplication, and retry-timer replacement/disposal semantics.
- [x] Expanded OSD lifecycle regression coverage in [src/OmenCoreApp.Tests/Services/OsdServiceLifecycleTests.cs](../src/OmenCoreApp.Tests/Services/OsdServiceLifecycleTests.cs) for shutdown-time visibility transitions (visible -> hidden emit-once behavior and hidden-state no-duplicate behavior).
- [x] Added canonical custom-mode regression coverage in [src/OmenCoreApp.Tests/Services/FanPresetVerificationTests.cs](../src/OmenCoreApp.Tests/Services/FanPresetVerificationTests.cs) and [src/OmenCoreApp.Tests/ViewModels/FanControlViewModelTests.cs](../src/OmenCoreApp.Tests/ViewModels/FanControlViewModelTests.cs), confirming manual/custom curves publish runtime `Custom`, unsaved custom curves do not synthesize fake preset names, and the fan page no longer exposes a ghost `Manual` preset.
- [x] Performed focused post-fix cross-check on Linux/Avalonia 8E35 patch files with clean editor diagnostics in [src/OmenCore.Avalonia/Services/LinuxHardwareService.cs](../src/OmenCore.Avalonia/Services/LinuxHardwareService.cs), [src/OmenCore.Avalonia/ViewModels/MainWindowViewModel.cs](../src/OmenCore.Avalonia/ViewModels/MainWindowViewModel.cs), [src/OmenCore.Avalonia/ViewModels/SettingsViewModel.cs](../src/OmenCore.Avalonia/ViewModels/SettingsViewModel.cs), and [src/OmenCore.Avalonia/ViewModels/SystemControlViewModel.cs](../src/OmenCore.Avalonia/ViewModels/SystemControlViewModel.cs).
- [x] Added tray header normalization coverage in [src/OmenCoreApp.Tests/Utils/TrayFanModeHeaderTests.cs](../src/OmenCoreApp.Tests/Utils/TrayFanModeHeaderTests.cs) and normalized whitespace/empty-state handling in [src/OmenCoreApp/Utils/TrayIconService.cs](../src/OmenCoreApp/Utils/TrayIconService.cs) so tray fan status no longer shows false stale-request annotations after convergence.
	Source: OSD/tray trust follow-up from field evidence pack §K and fundamentals-first Discord guidance to keep fan/performance state trustworthy.
- [x] Added tray performance/health header coverage in [src/OmenCoreApp.Tests/Utils/TrayStatusHeaderTests.cs](../src/OmenCoreApp.Tests/Utils/TrayStatusHeaderTests.cs) and extracted pure formatting helpers in [src/OmenCoreApp/Utils/TrayIconService.cs](../src/OmenCoreApp/Utils/TrayIconService.cs) to keep status labels deterministic under whitespace/empty edge cases.
- [x] Added behavior-level tray performance sync coverage in [src/OmenCoreApp.Tests/Utils/TrayPerformanceModeBehaviorTests.cs](../src/OmenCoreApp.Tests/Utils/TrayPerformanceModeBehaviorTests.cs), validating canonical alias normalization, submenu checkmark convergence, and canonical event payloads for tray-initiated performance mode changes.
	Source: OSD/tray consistency hardening follow-up after service-event-driven checkmark/header drift fixes.
- [x] Revalidated focused OSD/tray suite after lifecycle + tray behavior coverage additions: `TrayPerformanceModeBehaviorTests` + `TrayFanModeHeaderTests` + `TrayStatusHeaderTests` + `OsdServiceLifecycleTests` passing (26/26).
	Source: OSD/tray consistency stabilization validation for v3.6.1.
- [x] Added architecture-level runtime ownership audit in [docs/3.6.1-RUNTIME-ARCHITECTURE-MAP.md](3.6.1-RUNTIME-ARCHITECTURE-MAP.md), documenting competing state authorities, fragmented polling/timer ownership, EC ownership boundaries, dispatcher load concentration, and reduction-first remediation phases.
- [x] Added dispatcher pressure counters in [src/OmenCoreApp/ViewModels/MainViewModel.cs](../src/OmenCoreApp/ViewModels/MainViewModel.cs) for BeginInvoke/Invoke scheduling rates with periodic `[DispatcherMetrics]` logs, and instrumented hotkey and monitoring-update dispatch paths through those counters.
- [x] Added explicit `NonParallel` test collection definition in [src/OmenCoreApp.Tests/TestCollections.cs](../src/OmenCoreApp.Tests/TestCollections.cs) to match existing Services timer-registry test annotations and reduce nondeterministic collection behavior during aggregate test discovery.
- [x] Added explicit `STA Isolation` test collection in [src/OmenCoreApp.Tests/TestCollections.cs](../src/OmenCoreApp.Tests/TestCollections.cs) and moved [src/OmenCoreApp.Tests/Services/ModelReportServiceTests.cs](../src/OmenCoreApp.Tests/Services/ModelReportServiceTests.cs) into this boundary with bounded STA clipboard-thread join handling to reduce hang risk from clipboard/STA test interactions.
- [x] Revalidated infrastructure-sensitive test subset after STA isolation update: `ModelReportServiceTests` + `BackgroundTimerRegistryTests` passing (8/8).
- [x] Revalidated full Services-filter execution profile: discovery completes quickly (`--list-tests`) and full filter execution now completes successfully (`269/269` in ~180s), indicating long-runtime concentration rather than persistent discovery deadlock.
- [x] Added polling cadence coordinator coverage in [src/OmenCoreApp.Tests/Services/RuntimePollingCoordinatorTests.cs](../src/OmenCoreApp.Tests/Services/RuntimePollingCoordinatorTests.cs), including static-tray policy transitions and duplicate low-overhead dedupe behavior (2/2 passing).
- [x] Added EC operation coordinator coverage in [src/OmenCoreApp.Tests/Services/RuntimeEcOperationCoordinatorTests.cs](../src/OmenCoreApp.Tests/Services/RuntimeEcOperationCoordinatorTests.cs) and revalidated coordinator-focused suite (`RuntimeEcOperationCoordinatorTests` 5 tests + `RuntimePollingCoordinatorTests` 2 tests: 7/7 passing). New tests: `Execute_SerializesConcurrentCallersFromDifferentOwners` (concurrent gate serialization under 4-thread contention), `Execute_VoidOverload_RunsActionExactlyOnce`.

## Safety Improvements
- [x] Added profile synchronization guard gate to prevent recursive fan/performance ping-pong in linked mode.
- [x] Added undervolt runtime preflight checks so blocked MSR/SMU paths fail early with explicit reasons instead of presenting fake-success-capable controls.
- [x] Capability probing now validates undervolt runtime readiness during startup and gates undervolt UI visibility on successful runtime probe (not only driver presence), preventing false actionable undervolt controls when MSR/SMU access is blocked in [src/OmenCoreApp/Hardware/CapabilityDetectionService.cs](../src/OmenCoreApp/Hardware/CapabilityDetectionService.cs), [src/OmenCoreApp/Hardware/DeviceCapabilities.cs](../src/OmenCoreApp/Hardware/DeviceCapabilities.cs), and [src/OmenCoreApp/ViewModels/MainViewModel.cs](../src/OmenCoreApp/ViewModels/MainViewModel.cs).
- [x] Hardened max-fan hotkey handling to use FanService as the apply authority and to block false success when fan writes are unavailable in [src/OmenCoreApp/ViewModels/MainViewModel.cs](../src/OmenCoreApp/ViewModels/MainViewModel.cs).
- [x] Added bounded Max-mode persistence recovery in [src/OmenCoreApp/Services/FanService.cs](../src/OmenCoreApp/Services/FanService.cs): if deferred Max verification fails while Max is still requested, FanService performs one forced recovery apply with cooldown protection to avoid repeated EC write churn.
- [x] Serialized [src/OmenCoreApp/Services/PerformanceModeService.cs](../src/OmenCoreApp/Services/PerformanceModeService.cs) apply operations to reduce overlapping EC/power/fan-policy writes when hotkeys, power automation, and linked sync request transitions close together.
- [x] Power automation now skips fan/performance reapply operations when the requested AC/battery target is already active, reducing unnecessary hardware writes in [src/OmenCoreApp/Services/PowerAutomationService.cs](../src/OmenCoreApp/Services/PowerAutomationService.cs).
- [x] Serialized FanService controller write paths in [src/OmenCoreApp/Services/FanService.cs](../src/OmenCoreApp/Services/FanService.cs) behind both a local fan-write gate and the shared runtime EC coordinator gate, reducing interleaved EC/WMI write overlap when monitor-loop, tray actions, hotkeys, automation, fan cleaning, and performance-mode transitions request writes near-simultaneously.
	Source: EC contention follow-on from v3.6.1 remediation checklist (write-cadence overlap audit).

## UI Improvements
- [x] OSD fan mode now follows authoritative fan preset applied events, improving OSD/sidebar/tray coherence during linked transitions.
- [x] OSD mode labels now rehydrate cached current/fan/performance mode values on overlay re-show, preventing stale labels after minimize/focus transitions in [src/OmenCoreApp/Services/OsdService.cs](../src/OmenCoreApp/Services/OsdService.cs).
- [x] Reduced runtime tray/OSD mode-update churn in [src/OmenCoreApp/App.xaml.cs](../src/OmenCoreApp/App.xaml.cs) by deduping RuntimeStateEngine subscriber updates (fan/performance/curve/linked), so telemetry-only ticks do not repeatedly push unchanged mode state.
	Source: user-reported slow UI behavior clip during v3.6.1 remediation pass; correlated with high-frequency telemetry update path review.
- [x] Updated unsupported-system banner wording to avoid false "Non-HP" implication for HP fallback/unverified model cases in [src/OmenCoreApp/Views/MainWindow.xaml](../src/OmenCoreApp/Views/MainWindow.xaml).
	Source: Field evidence pack — cohort B (ProductId 88F7): model identity screenshot showed "Unknown OMEN (DEFAULT)" with Non-HP Gaming System warning while logs confirmed HP=True. See [docs/3.6.1-FIELD-EVIDENCE.md §I](3.6.1-FIELD-EVIDENCE.md).
- [x] Improved high-DPI/smaller-window layout resilience by reducing rigid min-width constraints, replacing fixed footer row sizing with auto-bounded behavior, and changing fan preset cards to a 2x3 layout to prevent overlap/clipping in [src/OmenCoreApp/Views/MainWindow.xaml](../src/OmenCoreApp/Views/MainWindow.xaml) and [src/OmenCoreApp/Views/FanControlView.xaml](../src/OmenCoreApp/Views/FanControlView.xaml).
	Source: Field evidence pack — UI layout screenshots showing overlapping/truncated elements, clipped independent CPU/GPU toggle area, and header warning/tab strip overlap at various window sizes. See [docs/3.6.1-FIELD-EVIDENCE.md §E](3.6.1-FIELD-EVIDENCE.md).
- [x] Reworked dense fan/settings horizontal control rows into wrap/stack layouts so preset actions, advanced fan settings, and model-identity badges remain readable under narrower widths and higher DPI in [src/OmenCoreApp/Views/FanControlView.xaml](../src/OmenCoreApp/Views/FanControlView.xaml) and [src/OmenCoreApp/Views/SettingsView.xaml](../src/OmenCoreApp/Views/SettingsView.xaml).
	Source: Field evidence pack — settings and fan panel screenshots showing overlapping elements; DPI scaling clipping reports. See [docs/3.6.1-FIELD-EVIDENCE.md §E](3.6.1-FIELD-EVIDENCE.md).
- [x] Further reduced fan page clipping risk on smaller widths/high DPI by relaxing rigid curve/telemetry split constraints and making profile-header helper text width flexible in [src/OmenCoreApp/Views/FanControlView.xaml](../src/OmenCoreApp/Views/FanControlView.xaml).
- [x] Dashboard graph-heavy telemetry updates are now suppressed when low-overhead mode hides those visuals, reducing perceived lag while preserving headline live telemetry in [src/OmenCoreApp/ViewModels/DashboardViewModel.cs](../src/OmenCoreApp/ViewModels/DashboardViewModel.cs).
    Source: Discord feedback from OsamaBiden (OMEN 16-xd0010ax, BEAM) requesting fundamentals-first responsiveness work because the app can feel like "10hz" even when the machine itself is otherwise fluid.
- [x] Tray fan-status header now trims pending-request labels and falls back cleanly when mode text is blank in [src/OmenCoreApp/Utils/TrayIconService.cs](../src/OmenCoreApp/Utils/TrayIconService.cs), reducing false "requested" drift in the tray UI.
	Source: OSD/tray consistency hardening under the v3.6.1 fundamentals-focused stabilization pass.
- [x] Quick popup now updates performance mode live when tray performance state changes in [src/OmenCoreApp/Utils/TrayIconService.cs](../src/OmenCoreApp/Utils/TrayIconService.cs), matching existing live fan-mode sync behavior and reducing stale popup state during mode transitions.
	Source: Fundamentals-first consistency pass for tray/popup fan-performance controls.
- [x] Runtime tray performance updates now also refresh performance submenu checkmarks in [src/OmenCoreApp/Utils/TrayIconService.cs](../src/OmenCoreApp/Utils/TrayIconService.cs), preventing stale highlighted mode state after service-driven mode changes.
	Source: OSD/tray consistency hardening for service-event-driven performance updates.
- [x] Unified tray performance parent-header formatting and canonical mode dispatch across set/update paths in [src/OmenCoreApp/Utils/TrayIconService.cs](../src/OmenCoreApp/Utils/TrayIconService.cs), reducing header drift and alias-state mismatches.
	Source: Fundamentals-first tray coherence pass driven by recent Discord responsiveness/consistency feedback.
- [x] Linux sidebar quick actions now provide explicit refresh feedback timestamps and quieter browser-launch behavior to avoid no-op feel and terminal error spam in [src/OmenCore.Avalonia/ViewModels/MainWindowViewModel.cs](../src/OmenCore.Avalonia/ViewModels/MainWindowViewModel.cs).
	Source: User report via Discord — Linux board class 8E35 ("Refresh Sensors doesn't do anything" and terminal error output on GitHub menu action).

## Hardware Support
- [ ] Hardware support changes pending validation.

## Known Limitations
- Legacy and multi-generation OMEN compatibility still requires deeper follow-on audit beyond current stabilization slice.
- The runtime remains a stabilized hybrid rather than a fully consolidated architecture; remaining ownership/timer simplification is tracked as post-3.6.1 work.
- Full-suite test runs remain long, but the current stabilization snapshot completed full Windows test validation successfully (623/623 passing).

# OmenCore v3.8.0 - Field Fixes, UX Polish, And Enhancements

**Release Date:** TBD
**Release Status:** Release candidate - local launch sweep passed; field validation pending
**Type:** Minor release
**Base Version:** v3.7.1

---

## Summary

v3.8.0 is a broader field-driven release, not just a bug-fix pass. It covers model-specific fixes, fan-control functionality improvements, GUI and visual polish, accessibility fixes, diagnostics clarity, and runtime optimization work.

The first fixes target OMEN Max `8D41` Max fan hold stability, Victus `8BD4` WMI V1 auto-handoff behavior, a dashboard battery-health false warning, and warning badge contrast. New tracked field items include GitHub #136 for HP OMEN 16-ap0xxx performance-option persistence, GitHub #137 for Linux OMEN 16-xd0xxx `8BCD` ACPI WMAA control failures, GitHub #138 for Victus 15 `8DCD` Performance mode remaining EC-limited, GitHub #139 for Victus 15-fb1xxx `8C30` Performance/Balanced/Quiet mode no-op behavior, GitHub #140 for Victus 16-e0194nw `88EE` exact identity, and OMEN 15-ek0xxx `878C` Quick Profiles leaving fans low at thermal-throttle temperatures.

Community feedback also asks that v3.8.0 stay RC/pre-release until the core control surface is validated: fan control, fan curves, performance modes, profile cycling, and basic hotkeys.

Post-3.8.0 reports GitHub #141-#143, saved Custom fan-curve selection, GPU OC startup persistence, and background resource usage are not claimed as fixes in this changelog. They are triaged for the v3.8.1 patch in [3.8.1-BUG-REPORTS.md](3.8.1-BUG-REPORTS.md), with a fresh-PC continuation guide in [3.8.1-MIGRATION-HANDOFF.md](3.8.1-MIGRATION-HANDOFF.md).

---

## Fixed So Far

### `8D41` OMEN Max 16 Max Fan Hold Stability

- Added a model capability override so `8D41` reasserts Max mode on the first low Max telemetry sample instead of waiting for two low samples.
- This keeps the MAX-series safety rule intact: no legacy EC fan writes, WMI-only fan ownership.
- Added regression coverage for the one-sample Max reassertion path.

### `8BD4` Victus 16 WMI V1 Auto Handoff

- Reverted `8BD4` to conservative WMI V1 auto handoff by disabling `SetFanLevel(0,0)` manual-zero floor clear.
- Kept exact ProductId identity and WMI profile routing, but stopped the v3.7.1 zero-floor path that correlates with 0/200 RPM and non-reactive fan reports.
- Updated regression coverage so `8BD4` does not accidentally opt back into V1 manual-zero floor clear.

### Battery Health False Warning

- Stopped deriving battery health from current battery charge percentage.
- Relabelled the historical battery chart to Battery Charge until real battery wear data is available.
- Added regression coverage so 68% charge while plugged in does not trigger a Battery Health warning.

### Dashboard Badge Contrast

- Darkened the medium-load badge color so white text is no longer placed on bright yellow.

### Direct Fan Level UI

- Added a first-class Direct fan card for models that support manual fan level control.
- Direct mode exposes a compact fixed-level slider with requested percent, estimated RPM, and explicit Apply action.
- Profile-only and curve-disabled systems keep the Direct control hidden/disabled through existing model capability gates.

### Premium Visual And Chart Pass

- Added a lightweight OmenCore thermal-control SVG mark for release/docs artwork.
- Refined monitoring chart colors away from harsh yellow/cyan defaults.
- Reused frozen chart brushes/dash patterns to reduce per-refresh allocations.
- Cleaned chart empty-state and stats text so it renders without broken glyphs.
- Fixed dashboard unload/reload state-change subscription tracking.

### Dashboard Temperature Sanity

- Fan-curve visualization no longer averages inactive, stale, invalid, or unavailable temperature readings.
- A lone `100C` reading paired with a much lower valid sensor is treated as a likely broken-BIOS sentinel for dashboard visualization.
- General CPU/GPU power cards now show `--W` with a tooltip when power telemetry is unavailable, invalid, inactive, unknown, or returning zero, instead of presenting a misleading `0W` reading.

### `8D87` OMEN Max Fan Hold Follow-up

- Added the first-low-sample Max reassertion override to `8D87` after field chat reported fans work initially but become less obedient later.
- Legacy EC fan writes remain disabled for MAX-series safety.

### RGB Reliability First Pass

- Enabled the basic WMI ColorTable RGB path for Victus 16-s0xxx / `8BD4` instead of forcing the model to backlight-only.
- Added a `7Z5Z2EA` Victus 16-S0035NT keyboard mapping for support-product/SKU based detection.
- WMI ColorTable writes are now kept when firmware accepts the command but readback is stale or mismatched, instead of restoring possibly stale previous colors; these are reported as accepted/unverified rather than verified RGB success.
- Restore-defaults now sets brightness before color data so firmware is armed before the final color write.
- Added a Lighting-page RGB observed-surface probe: users can send a safe bright static color, record whether keyboard zones, per-key keys, light bar, single backlight, nothing, or an unknown surface changed, and include that evidence in `rgb-control-path.txt`, readiness diagnostics, and in-app support bundles.

**Validation still needed:** `8BD4`/`7Z5Z2EA` zone color control has not been confirmed on real hardware. If keyboard zones still do not light up, capture a startup log showing the `[WmiBiosBackend]` lines and the GetColorTable readback result.

### OMEN MAX 16 Per-Key RGB Backend (First Implementation)

- Implemented `HidPerKeyBackend` — the first concrete USB HID per-key backend for OMEN MAX 16 laptops (`8D41` / `8D87`).
- Scans for HP Inc. keyboard controllers (VID `0x03F0`) and matches against a known OMEN per-key PID list derived from OpenRGB's HP OMEN keyboard controller database and OmenHubLighter decompilation of `HP.Omen.Core.Common.dll`.
- When a recognized device is found: enters static per-key mode, writes zone-mapped colors in 20-key segments, and commits via the documented HP per-key packet protocol.
- When a device is found but its PID is not in the known list: logs the PID at `Info` level so field reports can confirm the correct mapping for OMEN MAX (2025) hardware.
- Falls back transparently to WMI `ColorTable2020` (light-bar zone control) when the HID probe fails or no recognized keyboard is found.

**Current limitation / validation needed:** USB PIDs `0x054E` (ah0xxx / `8D41`) and `0x054F` (ak0xxx / `8D87`) are inferred from adjacent generations and have not been confirmed on real hardware. If per-key colors still do not apply on an OMEN MAX 16, share the startup log showing the `[HidPerKey]` scan results — particularly any `PID 0x????` line — so the correct PID can be added to the list.

### GitHub #136 - Performance Mode Persistence Fix (Root Cause)

- Identified and fixed the root cause of performance mode not persisting across restarts.
- The `SelectedPerformanceMode` property setter unconditionally called `SavePerformanceModeToConfig` on every assignment — including during constructor initialization when the saved mode could not be resolved and the UI fell back to Balanced. This silently overwrote the user's saved choice with `Balanced` every time the app started, before any user action.
- Fixed by assigning the initial value directly to the backing field (`_selectedPerformanceMode`) during construction and firing `OnPropertyChanged` notifications manually, bypassing the auto-save. Config persistence is now triggered only by explicit user selection via `ApplyPerformanceModeCommand` / `SelectPerformanceModeCommand`, not by object initialization.

### Retry Jitter Threading Fix

- `ReapplySettingWithRetryAsync` used `new Random()` inside the retry loop. Constructing `Random` without a seed uses `Environment.TickCount`, which can be identical for rapid successive calls on the same tick, producing the same jitter value and defeating the purpose of randomized back-off.
- Replaced with `Random.Shared.Next(0, 500)`, which uses the thread-safe shared instance introduced in .NET 6.

### GPU OC Profile Save For Power-Limit-Only Models

- `SaveGpuOcProfileCommand` required `GpuOcAvailable` as a precondition, which prevented profile saves on models that expose only the `GpuPowerLimitAvailable` (power-limit-only) path.
- Both code paths can store named profiles. The `CanExecute` guard is now `(GpuOcAvailable || GpuPowerLimitAvailable)`.

### GPU Power Boost Readback: Extended vs Maximum Now Distinguishable

- `VerifyGpuPowerReadback` used `GetGpuPower()`, which returns `ppab` as a bool. Both Extended and Maximum produce `ppab = true`, so hardware stuck at Maximum while Extended was requested passed the verification check silently.
- Switched to `GetGpuPowerDetailed()` (already used in `DetectGpuPowerBoost`), which returns `ppabLevel` as an integer. The check now uses `ppabLevel >= expectedLevel` (`Maximum` → `>=1`, `Extended3` → `>=2`, `Extended4` → `>=3`), making mismatched Extended-vs-Maximum hardware states detectable.

### Per-Core Voltage Offset: Core Count Ceiling Raised To 24

- `InitializePerCoreOffsets` had `const int maxCores = 16`, which clipped the UI for Raptor Lake HX (24-core) and newer Intel desktop-class-in-laptop silicon.
- Raised to `const int maxCores = 24` to cover all current shipping configurations.

### CPU Power Limit Reset Now Uses Startup-Read Values

- `ResetCpuPowerLimits` hardcoded `PL1 = 45W, PL2 = 65W` regardless of CPU or model, which is incorrect for many H-series processors and meaningless for AMD.
- Added `_initialPl1Watts` / `_initialPl2Watts` fields that capture the hardware-reported values during the first successful `InitializeCpuPowerLimits` call.
- Reset now restores those startup-baseline values. The hardcoded constants remain as a last-resort fallback only when the initial read returned zero (e.g. firmware locked on first boot).

### WmiFanController Startup Capability Log

- Added a capability-summary `Info` log line at the end of the `WmiFanController` constructor that records `MaxModeDropChecksBeforeReapply`, `AllowV1AutoModeFloorClear`, and `StrictFanModeReadback` for the current session.
- This makes field validation significantly easier when confirming that model-database capability overrides (e.g. `8D41` one-sample reassertion, `8BD4` floor-clear disable) are being applied correctly, without having to dig through the model init path.

### Windows Start With Windows Reliability

- Fixed scheduled-task XML generation for the Settings > Start with Windows path: `<Command>` now stores the raw executable path instead of a quoted path that can create an enabled task which fails to launch.
- Added a scheduled-task `<WorkingDirectory>` pointing at the app folder.
- Startup detection now validates the scheduled-task XML and rejects disabled or malformed quoted-command tasks instead of showing the toggle as enabled for a broken task.
- Task-creation failure handling now uses a proper Windows administrator-role check.

### Fan Diagnostics Guidance

- Replaced the stale "switch to OGH proxy backend" fan-diagnostics tip with current actionable guidance: Restore OEM Auto, try Direct/Max control where available, then export diagnostics with ProductId/backend/requested percent/RPM/level readback if fans remain unresponsive.
- Added regression coverage to keep the unavailable OGH proxy backend-switch wording from returning.

### GitHub #138 - Victus 15 `8DCD` Performance Mode Routing

- Added an exact conservative model profile for Victus 15 ProductId `8DCD`.
- The profile keeps WMI fan/profile control, disables unverified direct EC writes and independent curves, and enables the WMI thermal-policy fallback for Performance mode.
- No PL1/PL2 watt override is included yet because the issue does not include diagnostics proving the correct CPU power envelope.

### GitHub #139 - Victus 15-fb1xxx `8C30` Performance Mode Routing

- Tightened the exact `8C30` profile to expose `Quiet`, `Balanced`, and `Performance` as the relevant OEM mode names.
- Kept direct EC writes disabled and hid CPU power-limit controls because issue #139 has no safe PL1/PL2 readback evidence yet.
- Kept WMI thermal-policy fallback enabled and added an `8C30 / Victus 15-fb1xxx` validation card plus RC matrix row so field reports can capture Quiet/Balanced/Performance wattage and fan/RPM response before any model-specific watt override is added.

### OMEN 15-ek0xxx `878C` Quick Profile Cooling Routing

- Added an exact conservative model profile for OMEN Laptop 15-ek0xxx ProductId `878C` after a Discord report showed Performance/Balanced/Quiet/Gaming/Extreme/Auto profile clicks leaving both fans around `1900 RPM` at `99C`, while Custom Max could wake the coolers.
- The profile keeps WMI fan/profile control, disables unverified direct EC writes and independent curves, sets the legacy WMI V1 max level to `55`, and enables the WMI thermal-policy fallback so Quick Profiles still send the OEM performance policy when EC power limits are unavailable.
- Added a matching conservative keyboard identity profile for `878C` so model summaries no longer fall back to an unknown keyboard profile.
- Added regression coverage for exact capability identity, keyboard identity, and Performance-mode WMI fallback routing without direct EC power-limit writes.

### OMEN 15-dh0xxx `8600` Legacy Identity And Telemetry Routing

- Added an exact conservative model profile for OMEN by HP Laptop 15-dh0xxx ProductId `8600` after a Discord report showed unknown legacy fallback, weak fan-mode response except Max, missing PawnIO, CPU temperature stuck around `28C`, CPU power `0W`, and fan RPM `0`.
- The profile keeps WMI fan/profile control, disables unverified direct EC writes and independent curves, treats RPM readback as untrusted, hides direct CPU power-limit controls, and enables WMI thermal-policy fallback for Quick Profiles.
- Added a matching conservative backlight-only keyboard identity profile for `8600` so model summaries no longer report an unknown keyboard profile.
- Added `8600 / OMEN 15-dh0xxx` validation-card and RC matrix coverage focused on PawnIO install/reboot telemetry recovery, Windows fan-mode response, and Linux cross-check evidence.

### GitHub #140 - Victus 16-e0194nw `88EE` Exact Identity

- Added an exact conservative model profile for HP Victus 16-e0194nw ProductId `88EE` after the issue reported the board being inferred as sibling `88EC` from the broad `Victus by HP Laptop 16-e0xxx` WMI name.
- Added a matching conservative backlight-only keyboard identity profile for `88EE` so model summaries no longer report keyboard source as a model-name series match.
- Kept feature flags evidence-gated: WMI fan/profile routing remains available, while direct EC writes, independent curves, RGB, GPU boost, and undervolt stay disabled until field diagnostics prove those paths.
- Added regression coverage for exact capability identity, keyboard identity, and identity-summary source text.

### PawnIO Installer Argument Fix

- Fixed the bundled Windows installer invocation for `PawnIO_setup.exe`. v3.7.1 launched the setup with `-silent` only, which could surface a modal error asking users to specify either `-install` or `-uninstall`.
- The Inno Setup script now passes `-install -silent` when the optional PawnIO task is selected, while still skipping the sub-installer when PawnIO is already present.
- Updated release hygiene coverage so future installer changes must keep the required PawnIO install verb.

### GitHub #137 - Linux `8BCD` Degraded-Control Detection (Partial)

- Board `8BCD` no longer reports headline `full-control` purely because WMI/sysfs paths are present.
- Linux diagnostics now flag ACPI WMAA/WHCM abort evidence as a degraded WMI-control state when kernel logs contain matching failures.
- `fan --speed` messaging no longer describes the fallback as OMEN Max-specific on non-Max boards, and adds `8BCD`-specific guidance when RPM does not change.
- Linux battery status now discovers the active `/sys/class/power_supply` battery path instead of assuming `BAT0`, and falls back between `energy_*` and `charge_*` counters.

**Validation still needed:** this does not make broken HP WMI/ACPI calls effective. It prevents overclaiming support, keeps still-working telemetry/EC paths visible, and asks for the right kernel/sysfs evidence before re-enabling WMI-backed fan/RGB/battery claims.

### Release-Gate Follow-up Hardening

- Fixed the Direct fan control layout so the Direct fixed-level panel no longer shares the same grid row as the custom-curve settings row.
- Replaced newly introduced bare `catch {}` blocks in keyboard-lighting paths with explicit exception handling/logging so the release hygiene gate stays clean.
- Fixed a mojibake border string in the Linux diagnose Kernel Hints renderer.
- Hardened the new HID per-key RGB backend so single-zone writes preserve the last requested zone colors instead of turning untouched zones black on write-only hardware.
- Replaced Linux battery/sysfs bare catches with explicit recoverable exception filters.
- Replaced mojibake-prone Unicode window-control glyphs with ASCII-stable chrome text and changed the title-bar drag race handler from a bare catch to an explicit logged `InvalidOperationException` path.

### Core Controls And UI Responsiveness Sweep

- Added `3.8.0-CORE-CONTROLS-NEXT-STEPS.md` to keep release work focused on fans, RGB, overclocking, undervolting, readback, diagnostics, and model truthfulness before lower-priority feature work.
- Added `core-control-readiness.txt` to diagnostic exports so field reports have one place for fan backend/write availability, RGB backend/surface state, tuning startup/readback state, monitoring source/health/cadence, recent performance apply traces, and suggested validation actions.
- Added `omenmon-reborn-parity.txt` to diagnostic exports as a license-safe OmenMon-Reborn emulation/parity layer: it maps OmenMon-style expectations (probe report, lightweight mode, dynamic model truthfulness, read-only unknown-model fallback, auto-calibration evidence, EC contention hardening, fan/RGB/OMEN-key workflows) onto OmenCore's current safe-control surface and next validation evidence.
- Added `field-validation-script.txt` to diagnostic exports with exact field-tester steps for fan Max/Direct/curve/Restore Auto, RGB surface checks, performance/tuning readback, profile cycling, hotkeys, startup restore, and evidence attachments.
- Added `priority-model-validation-cards.txt` to diagnostic exports with targeted validation cards for `8D41`, `8D87`, `8BD4`, `8C30`, `8DCD`, `878C`, Linux `8BCD`, OMEN 17 db-1000, and Victus 15/16 field cohorts.
- Added `rc-validation-matrix.txt` to diagnostic exports so RC bundles show each priority cohort's local fix status, field-pending status, degraded/experimental notes, and promotion evidence before v3.8.0 is treated as stable.
- Added the same lightweight Core Control Readiness section to the in-app diagnostic export control for quick support bundles.
- Added hotkey and OMEN-key readiness to core-control diagnostics, including registered/pending hotkeys, physical OMEN-key hook/WMI watcher state, strict-mode and firmware Fn+P flags, and last never-intercept suppression evidence for Victus 15 and profile-cycling validation.
- Expanded fan command history, core-control readiness, and the in-app diagnostic export with per-command ProductId/model context, write/curve/manual/desktop-safety gates, latest fan telemetry snapshot, and raw primary RPM/duty markers so field reports can show what OmenCore requested and what readback was visible at that moment.
- Added a safe tuning rollback bundle and Tuning page action that normalizes persisted startup state first, then best-effort restores fan OEM auto, Balanced performance, Minimum GPU power, startup CPU PL1/PL2, zero TCC, cleared undervolt offsets, reset GPU OC, and AMD power defaults where supported.
- Split startup restore into category opt-ins for fans, performance/GPU power, RGB, and tuning under the existing broad safety gate. Legacy configs with broad restore enabled keep legacy behavior until category toggles are saved; fresh/disabled configs remain off.
- Added persisted RGB observed-surface capture to diagnostics so accepted/unverified backend writes can be separated from what physically changed on the device.
- Replaced high-visibility emoji/checkmark glyphs in Fan Control and Performance Mode cards with existing vector icons so core controls render consistently across Windows font/locale setups.
- Replaced Game Library search/scanning glyphs with vector icons and re-enabled logical scrolling/virtualization hints for smoother large-library browsing.

### P2 Overlay And RGB Scene Enhancements

- Improved the OSD FPS row so RTSS average FPS is used when instant FPS is zero, compact avg/1%/process detail is shown when available, FPS quality is color-coded, and unavailable states remain explicit instead of implying GPU activity is FPS.
- Renamed the settings toggle from `FPS / GPU Activity` to `FPS (RTSS)` to match the real data source.
- Added Heat Wave and Calm Pulse built-in RGB scenes.
- Routed `RgbSceneEffect.Wave` through capability-filtered `effect:wave` provider calls instead of collapsing Wave scenes into Spectrum, and protected the audio-reactive built-in scene from removal.

### P2 Game/Profile Automation Enhancements

- Game profile JSON now uses the configured OmenCore config folder, which keeps tests, portable configs, and support bundles aligned instead of hardcoding `%APPDATA%\OmenCore`.
- Game profile process monitoring now honors the `GameProfilesEnabled` feature toggle before starting the polling timer.
- Exact executable-path game profiles now outrank generic executable-name profiles, even when the generic profile has a higher priority value.
- Already-active game profiles no longer reapply and increment launch count repeatedly when the same tracked process is detected again.
- Game profiles now include `RestoreDefaultsOnExit`; the profile editor exposes it, and the exit event carries the exited profile so launchers or chained automation can opt out of automatic Balanced/default restore.

---

## Newly Tracked GitHub Issues

### GitHub #136 - HP OMEN 16-ap0xxx Energy Option Resets To Balanced

- Source: [GitHub #136](https://github.com/theantipopau/omencore/issues/136)
- Reporter: SoyJogargon
- Status: **Fixed in this release** — root cause identified and resolved (see "GitHub #136 - Performance Mode Persistence Fix" in Fixed So Far above).
- Impact: selecting Quiet, Performance, Max, or another energy option in OmenCore 3.7.1 did not survive app close/reopen; the UI returned to Balanced on next launch.
- Root cause: the `SelectedPerformanceMode` property setter called `SavePerformanceModeToConfig` unconditionally, including during startup initialization, overwriting the saved mode name with `Balanced` whenever the saved entry could not be resolved.

### GitHub #137 - Linux OMEN 16-xd0xxx `8BCD` ACPI WMAA Abort Breaks Hardware Controls

- Source: [GitHub #137](https://github.com/theantipopau/omencore/issues/137)
- Reporter: adarsh-67r
- Status: partial degraded-control handling implemented; field validation pending.
- Impact: CachyOS on Board `8BCD` / BIOS F.31 reports ACPI WMAA/WHCM aborts that break WMI-backed fan control, keyboard RGB, and battery status, while temperature monitoring and EC performance switching still partly work.
- Release note: Linux now avoids classifying `8BCD` as full-control from path presence alone, reports WMAA/WHCM aborts as degraded WMI control, and uses standard power-supply battery fallback data where available. Effective write/readback checks are still needed before fan/RGB support can be called verified.

### GitHub #138 - Victus 15 `8DCD` Performance Mode Still EC-Limited

- Source: [GitHub #138](https://github.com/theantipopau/omencore/issues/138)
- Reporter: cksenpai
- Status: first conservative model-routing fix implemented; field validation pending.
- Impact: Performance mode reportedly leaves the CPU EC-limited around `40W` instead of allowing aggressive turbo behavior.
- Release note: `8DCD` now uses a conservative Victus 15 profile with WMI thermal-policy fallback; diagnostics are still needed before adding model-specific watt overrides.

### GitHub #139 - Victus 15-fb1xxx `8C30` Performance Modes Do Nothing

- Source: [GitHub #139](https://github.com/theantipopau/omencore/issues/139)
- Reporter: NotDarkn
- Status: first conservative model-routing/diagnostics fix implemented; field validation pending.
- Impact: Performance, Balanced, and Quiet reportedly produce identical FPS/wattage behavior.
- Release note: `8C30` now exposes the expected Quiet/Balanced/Performance mode names, hides unverified CPU power-limit controls, keeps WMI thermal-policy fallback enabled, and has targeted validation-card evidence requirements before wattage overrides are added.

---

## Release Gate Notes

- v3.8.0 should remain RC/pre-release until fan control, fan curves, performance modes, profile cycling, and basic hotkeys have model-family validation.
- Core fan/performance/hotkey regressions take priority over extra feature work for this cycle.
- Release notes should identify verified, partially supported, and still-experimental model families so users do not assume universal support.

---

## Planned Improvement Areas

- GitHub #137: Linux `8BCD` effective write/readback validation before calling manual fan/RGB control confirmed.
- GitHub #138: Victus 15 `8DCD` Performance-mode validation with before/after PL1/PL2 and CPU package-power readback.
- GitHub #139: Victus 15-fb1xxx `8C30` Quiet/Balanced/Performance validation with before/after CPU package power, WMI thermal-policy result, and fan RPM/level response.
- OMEN 15-ek0xxx `878C`: Quick Profile validation with before/after fan RPM, WMI policy result, Performance apply trace, and PL1/PL2/GPU readback.
- OMEN 15-dh0xxx `8600`: PawnIO install/reboot telemetry recovery plus Quiet/Balanced/Performance/Auto/Max response on Windows and Linux cross-checks if available.
- Victus 16-e0194nw `88EE`: Exact ProductId identity confirmation plus fan/RGB/readback diagnostics before promoting capabilities beyond conservative WMI/backlight routing.
- Lightweight "just control the fans" mode to reduce background memory footprint.
- Clearer stale temperature guidance when PawnIO/fallback telemetry is unavailable.
- Victus 15 Omen key interception validation.
- FPS overlay and exclusive fullscreen OSD validation.
- RGB surface/backend diagnostics for Victus and OMEN models where light bar, keyboard, and backlight support differ.
- Dashboard and fan-control visual pass for readability, contrast, and state clarity.
- Runtime optimization pass for polling, chart history, optional integrations, and tray-only behavior.
- Diagnostics improvements for model capability, fan ownership, RGB backend, OSD support, and missing-driver states.

---

## Tracked Reports

See [3.8.0-BUG-REPORTS.md](3.8.0-BUG-REPORTS.md).

---

## Validation

- `dotnet build OmenCore.sln -c Debug`: passed, 0 warnings, 0 errors.
- `dotnet build OmenCore.sln -c Debug --no-restore`: passed after follow-up fixes, 0 warnings, 0 errors.
- `dotnet test src\OmenCoreApp.Tests\OmenCoreApp.Tests.csproj -c Debug --no-build`: passed, 796/796.
- Focused fan/model tests passed: `ModelCapabilityDatabaseTests`, `WmiV2VerificationTests`, `FanSmoothingTests` - 99/99.
- Focused monitoring tests passed: `HotkeyAndMonitoringTests` - 36/36.
- Focused dashboard/model tests passed: `DashboardViewModelTests`, `ModelCapabilityDatabaseTests` - 42/42.
- Focused fan-control view-model tests passed: `FanControlViewModelTests` - 19/19.
- Combined focused regression suite passed with `--no-build` after the Direct fan UI, chart visual pass, and Discord field follow-ups: 205/205.
- Focused release-follow-up suite passed: `SettingsViewModelTests`, `FanVerificationServiceTests`, `ModelCapabilityDatabaseTests` - 62/62.
- `dotnet build OmenCore.sln -c Debug --no-restore`: passed after Start with Windows, fan diagnostics wording, and `8DCD` model routing fixes, 0 warnings, 0 errors.
- `dotnet build OmenCore.sln -c Debug --no-restore`: passed after Linux `8BCD` degraded-control notes, battery/sysfs catch filtering, and HID per-key state hardening, 0 warnings, 0 errors.
- Focused v3.8.0 regression suite passed after the latest hardening pass: `KeyboardModelDatabaseTests`, `WmiBiosBackendTests`, `SettingsViewModelTests`, `ModelCapabilityDatabaseTests`, `MainViewModelTests`, `FanVerificationServiceTests`, `HotkeyAndMonitoringTests`, `FanControlViewModelTests`, `DashboardViewModelTests`, `WmiV2VerificationTests` - 218/218.
- `dotnet build OmenCore.sln -c Debug --no-restore`: passed after the core-controls next-steps doc and UI icon/scrolling sweep, 0 warnings, 0 errors.
- Focused XAML/view-model suite passed: `ResourceDictionaryTests`, `MainViewModelTests`, `FanControlViewModelTests` - 47/47.
- `dotnet build OmenCore.sln -c Debug --no-restore`: passed after the core-control readiness diagnostic export, 0 warnings, 0 errors.
- New core-control readiness snapshot checks passed: `CoreControlReadinessFile_IsIncludedInExport`, `CoreControlReadinessFile_ContainsCoreControlSections_WhenServicesUnavailable`, `BuildCoreControlReadinessReport_ContainsStartupRestoreAndValidationGuidance` - 3/3.
- Focused UI/core-control regression checks passed after the readiness export: `ResourceDictionaryTests`, `MainViewModelTests`, `FanControlViewModelTests` - 47/47.
- `dotnet build OmenCore.sln -c Debug --no-restore`: passed after hotkey/OMEN-key readiness diagnostics, 0 warnings, 0 errors.
- Focused hotkey/readiness tests passed after hotkey/OMEN-key diagnostics: `HotkeyServiceTests`, `OmenKeyServiceTests`, `HotkeyAndMonitoringTests`, and core-control readiness filters - 50/50.
- Full diagnostic snapshot tests passed after readiness diagnostics: `DiagnosticExportSnapshotTests` - 19/19.
- `dotnet build OmenCore.sln -c Debug --no-restore`: passed after OmenMon-Reborn parity diagnostics, 0 warnings, 0 errors.
- Full diagnostic snapshot tests passed after OmenMon-Reborn parity diagnostics: `DiagnosticExportSnapshotTests` - 21/21.
- `dotnet build OmenCore.sln -c Debug --no-restore`: passed after field-validation script diagnostics, 0 warnings, 0 errors.
- Full diagnostic snapshot tests passed after field-validation script diagnostics: `DiagnosticExportSnapshotTests` - 23/23.
- `dotnet build OmenCore.sln -c Debug --no-restore`: passed after priority model validation-card diagnostics, 0 warnings, 0 errors.
- Full diagnostic snapshot tests passed after priority model validation-card diagnostics: `DiagnosticExportSnapshotTests` - 25/25.
- Full diagnostic snapshot tests passed after RC validation matrix diagnostics: `DiagnosticExportSnapshotTests` - 28/28.
- Focused fan preset verification passed after RC validation matrix diagnostics: `FanPresetVerificationTests` - 22/22.
- `dotnet build OmenCore.sln -c Debug --no-restore`: passed after RC validation matrix diagnostics, 0 warnings, 0 errors.
- Focused rollback model tests passed after safe tuning rollback bundle: `TuningRollbackCoordinatorTests` - 3/3.
- Focused Tuning view resource checks passed after safe tuning rollback bundle: `ResourceDictionaryTests` - 3/3.
- Full diagnostic snapshot tests passed after safe tuning rollback bundle: `DiagnosticExportSnapshotTests` - 28/28.
- `dotnet build OmenCore.sln -c Debug --no-restore`: passed after safe tuning rollback bundle, 0 warnings, 0 errors.
- Focused startup restore split tests passed: `StartupRestorePolicyTests`, `StartupRestoreCategoryToggles_PersistToConfig`, `StartupFanRestore_RequiresGlobalAndFanCategoryOptIn` - 5/5.
- Focused Tuning/Settings resource checks passed after startup restore split: `ResourceDictionaryTests` - 3/3.
- Full diagnostic snapshot tests passed after startup restore split: `DiagnosticExportSnapshotTests` - 28/28.
- `dotnet build OmenCore.sln -c Debug --no-restore`: passed after startup restore split, 0 warnings, 0 errors.
- Focused P2 overlay/RGB tests passed after FPS formatter and Wave scene routing: `OsdFpsDisplayFormatterTests`, `RgbSceneServiceTests` - 9/9.
- `dotnet build OmenCore.sln -c Debug --no-restore`: passed after FPS OSD and RGB scene P2 enhancements, 0 warnings, 0 errors.
- Focused game-profile automation tests passed after exact-path matching, feature-gated monitoring, duplicate suppression, and exit restore policy: `GameProfileMatchingTests`, `GameProfileServiceTests` - 8/8.
- `dotnet build OmenCore.sln -c Debug --no-restore`: passed after game-profile automation and README updates, 0 warnings, 0 errors.
- Focused release-gate window chrome test passed after ASCII-stable window controls and title-bar drag logging: `RuntimePresentation_MainWindowChromeUsesStableAsciiControls` - 1/1.
- Focused `8600` identity/diagnostics tests passed after OMEN 15-dh0xxx conservative routing: model capability, model identity summary, keyboard identity, priority validation cards, and RC matrix filters - 20/20.
- `dotnet build OmenCore.sln -c Debug --no-restore`: passed after `8600` OMEN 15-dh0xxx routing and docs updates, 0 warnings, 0 errors.
- Focused GitHub #140 `88EE` identity tests passed: capability lookup, keyboard lookup, identity summary, and newly-added model-entry coverage - 19/19.
- `dotnet build OmenCore.sln -c Debug --no-restore`: passed after GitHub #140 `88EE` routing and docs updates, 0 warnings, 0 errors.
- Focused release-gate regression suite passed after launch sweep: `ModelCapabilityDatabaseTests`, `KeyboardModelDatabaseTests`, `ModelIdentityResolutionSummaryTests`, `DiagnosticExportSnapshotTests`, `ReleaseGateCodeHygieneTests`, `FanPresetVerificationTests`, and `FanVerificationServiceTests` - 153/153.
- Full `OmenCoreApp.Tests` project passed after launch sweep - 851/851.
- `dotnet test OmenCore.sln --no-restore`: passed.
- Version metadata aligned to `3.8.0` across `VERSION.txt`, active project files, installer fallback, Linux/Avalonia version display fallback, README, INSTALL, and launch-readiness diagnostics.
- `dotnet build OmenCore.sln -c Debug --no-restore`: passed after the `3.8.0` version bump, 0 warnings, 0 errors.
- `dotnet build OmenCore.sln -c Release --no-restore`: passed after the `3.8.0` version bump, 0 warnings, 0 errors.
- Full `OmenCoreApp.Tests` project passed in Debug after the `3.8.0` version bump - 851/851.
- Full `OmenCoreApp.Tests` project passed in Release after the `3.8.0` version bump - 851/851.
- `dotnet test OmenCore.sln --no-restore`: passed after the `3.8.0` version bump - 851/851.
- Windows artifacts built successfully with `build-installer.ps1`: `OmenCoreSetup-3.8.0.exe` and `OmenCore-3.8.0-win-x64.zip`.
- Linux artifact built successfully with `build-linux-package.ps1 -SkipBinaryVersionCheck`: `OmenCore-3.8.0-linux-x64.zip`, `.sha256`, `version.json`, and `linux-version-verification-3.8.0-linux-x64.json`. Binary execution smoke was skipped because this run was on Windows, not Linux/WSL.
- Artifact SHA256:
  - `79C2B6F6EE14A1FF7231DEC8B13EA18FB96695B1DF1DDE9267B2C68FC1400BF0  OmenCoreSetup-3.8.0.exe`
  - `CA0C0B11532C72532A6D39D18F68913C16971B121F59AA57313D9DD41C8AFCF4  OmenCore-3.8.0-win-x64.zip`
  - `46549D55368015FACB6B9CDE0B09FF6EAD8ACC71766998F74FCD438E857D6AB6  OmenCore-3.8.0-linux-x64.zip`
- `git diff --check`: passed; only repository line-ending normalization warnings were reported.
- Pending field validation on `8D41`, `8BD4`, `8DCD`, `8BCD` Linux degraded-control reporting, `88EE` exact-board fan/RGB/readback evidence, Windows autostart on affected installs, and unverified RGB surface routing.

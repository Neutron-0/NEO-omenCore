# OmenCore v3.6.1 Release Candidate

v3.6.1 is a focused stabilization release for the v3.6.0 regression baseline. It does not add major new features and it does not claim full runtime architecture consolidation.

## Focus Areas
- Fan/performance state convergence across UI, tray, hotkeys, OSD, dashboard, and linked mode.
- Reduced WMI fan-control CPU/write pressure during custom fan curve and hold sessions.
- Safer EC operation ordering across fan, performance, keyboard, power verification, diagnostics, and GPU boost fallback paths.
- Clearer unsupported/unverified capability handling for undervolt, unknown models, Linux fan/profile support, and fallback paths.
- UI/tray/OSD freshness improvements for minimized, tray-only, and restored sessions.

## Additional 3.6.1 Hardening
- Custom and manual fan curves now publish canonical runtime mode `Custom` instead of leaking ghost UI-only labels like `Manual` or `Custom (Applied)` into hotkeys, tray state, and OSD.
- The advanced fan page no longer exposes a synthetic built-in `Manual` preset that could drift from the actual runtime owner.
- MainViewModel fan synchronization now prefers the authoritative FanService runtime mode over raw preset/event names, improving hotkey and tray convergence after preset applies.
- The OSD current-mode row is suppressed when it would duplicate the same performance label already shown in the performance-mode row.

## Validation
- Release build: `dotnet build OmenCore.sln -c Release --no-restore` passed with 0 warnings and 0 errors.
- Full Windows test suite: 623/623 passing.
- Release hygiene tests: 7/7 passing.
- Fan/performance/OSD/tray focused suite: 115/115 passing.
- EC/diagnostics/WMI focused suite: 61/61 passing.
- Post-hardening focused fan/runtime/OSD regression slice: 59/59 passing.

## Expected Artifacts
- `OmenCoreSetup-3.6.1.exe`
- `OmenCore-3.6.1-win-x64.zip`
- `OmenCore-3.6.1-linux-x64.zip`
- `SHA256SUMS-3.6.1.txt`

## SHA256
```text
45BA43784A70231EAA1DC925AE3F23FE83E4EAE1CBE22AD0DA91E5ADD13A3192  OmenCoreSetup-3.6.1.exe
23EC24EE4E308C601085F26FD46F00D7ABA0F13B49D1EF3683162DA912DB0F6F  OmenCore-3.6.1-win-x64.zip
82491B5742C0D5E523717D29304D2F12F99708C7870BA01D931AE5F4C9FDE8BE  OmenCore-3.6.1-linux-x64.zip
```

## Tester Notes
Please report model, ProductId, BIOS version, Windows/Linux version, install type, and whether OMEN Gaming Hub is installed/running.

High-priority validation areas:
- Fan mode and performance mode agreement across main UI, tray, and OSD.
- Max Fan apply/exit behavior.
- Custom fan curve CPU usage over a 10-minute hold.
- Minimize-to-tray and restore state freshness.
- Suspend/resume recovery where testers are comfortable doing so.
- Linux profile/fan capability truth on hp-wmi/sysfs boards.

Full changelog: [CHANGELOG-3.6.1.md](CHANGELOG-3.6.1.md)
RC checklist: [3.6.1-RC-CHECKLIST.md](3.6.1-RC-CHECKLIST.md)

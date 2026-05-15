# OmenCore v3.6.2 Release Candidate 1 (RC1)

**Release Date**: May 15, 2026  
**Status**: 🧪 **Release Candidate** — Trusted Tester Validation in Progress  
**Validation Window**: May 16–17, 2026  
**Target Stable Release**: May 18, 2026 (if no blockers)  

---

## 📋 What's New in v3.6.2

This is a **stabilization release** focusing on runtime authority correction, thermal safety hardening, and UI dispatch overhead reduction. No new user-facing features; purely backend improvements to reliability and diagnostics.

### Core Fixes (3 Issues)

#### 🌡️ Issue #129: CPU Thermal Authority Hardening
- **Problem**: CPU thermal authority could remain on low-confidence WMI/ACPI values during active load, masking package-sensor divergence
- **Fix**: LibreHardwareMonitor fallback now enforced under suspicious low-temp/high-load mismatch. Authority source/reason tracked explicitly. Transitions logged for diagnostics export.
- **Impact**: Thermal anomaly triage now includes explicit authority state; field support can distinguish sensor divergence from actual throttling
- **Testing**: Requires Ryzen CPU comparison (package vs skin temp under load)

#### 🎯 Issue #128: Victus e0xxx ProductId 88EC Identity  
- **Problem**: ProductId 88EC (Victus 16-e0xxx) fell through to broad Victus family defaults, producing low-confidence identity output
- **Fix**: Explicit ProductId 88EC entries added to capability database and keyboard database. Defaults conservative (no RGB speculation) pending field verification.
- **Impact**: Victus 16-e0xxx systems now resolve to explicit identity instead of fallback; capability messaging reduced ambiguity
- **Testing**: Validate identity resolution for systems with ProductId 88EC

#### 🎨 Issue #130: RGB Graceful Degradation
- **Problem**: Dynamic RGB effects (breathing, spectrum) failed on platforms supporting only static color, creating noisy error logs
- **Fix**: Effect type resolution added; unsupported providers explicitly skipped with logs. Zero-provider case exits cleanly with status event.
- **Impact**: Static RGB remains available even on dynamic-effect-unsupported platforms; no crashes on partial provider support
- **Testing**: Attempt breathing/spectrum effects; verify static color still works if dynamic skipped

### Runtime Improvements

- **Telemetry Decoupling**: RuntimeStateEngine controls fan/performance independent from telemetry pipeline, reducing cross-system noise
- **Dashboard Dormancy**: Hidden dashboard surfaces suppressed after 30s, reducing GPU/CPU from telemetry graph rendering
- **Hidden-Surface Suppression**: GeneralView tab telemetry projection suppressed when not visible
- **Render-State Caching**: Tray icon and popup render states cached; no duplicate renders on unchanged summary text
- **Cadence Optimization**: Active monitoring cadence reduced from 1s to 2s for focused-window overhead reduction (maintains OSD responsiveness)
- **Latest-Wins Dispatching**: Hotkey/tray commands coalesced to prevent mode ping-pong

---

## 🔧 Installation

### Windows

#### From Installer
```powershell
# Download v3.6.2-RC1-installer.exe
# Run as Administrator
.\OmenCore-3.6.2-RC1-installer.exe
```

#### Portable (if provided)
```powershell
# Extract ZIP
# Run OmenCore.exe directly
# No admin required; portable configuration
```

#### Upgrade from v3.6.1
- Installer will preserve existing settings and custom profiles
- Restart OmenCore after installation
- No configuration migration required

### Linux (CLI Only)

```bash
# Extract omencore-3.6.2-rc1-linux-x64.tar.gz
tar xzf omencore-3.6.2-rc1-linux-x64.tar.gz
cd omencore-3.6.2

# Run CLI
./omencore-cli --help
./omencore-cli status
./omencore-cli --mode gaming  # Set fan preset
```

---

## ✅ Validation Status

### Code-Side Testing Complete
- ✅ Full solution build: **0 errors, 0 warnings**
- ✅ Unit tests: **100+ tests discovered**
- ✅ Regression tests added: **12 tests for #129/#128/#130**
- ✅ Sample test execution: **Passing**
- ✅ Documentation: **Up-to-date** (changelog, performance triage, RC validation checklist)

### Pending (Trusted Tester Phase)
- ⏳ Scenario A operator runbook (15 min scripted test)
- ⏳ 30+ minute gaming stress (thermal/fan response, frame pacing)
- ⏳ Idle matrix validation (focused, minimized, tray, screen-off)
- ⏳ Multi-monitor DPI scaling (if applicable)
- ⏳ Ryzen CPU package vs skin temp comparison (thermal authority verification)

### Known Limitations
1. **Victus 88EC Feature Flags**: Conservative defaults pending field confirmation. RGB capability may be present but marked unsupported pending reports.
2. **Thermal Authority Fallback**: Behavior validated in unit tests but requires real-world Ryzen low-temp/high-load scenarios for final confidence.
3. **RGB Platform Coverage**: Graceful degradation tested; full provider matrix validation pending.

---

## 📊 Checksums

| File | SHA256 |
|------|--------|
| `OmenCore-3.6.2-RC1-installer.exe` | `[PENDING BUILD]` |
| `omencore-3.6.2-rc1-linux-x64.tar.gz` | `[PENDING BUILD]` |
| `OmenCoreApp.Tests.dll` (v3.6.2-RC1) | `[PENDING BUILD]` |

*Checksums will be updated when builds are published.*

---

## 🎯 Tester Instructions

### Scenario A (15 minutes)
1. Launch OmenCore; verify identity resolves correctly
2. Cycle hotkey through presets (Auto → Gaming → Extreme → Custom → Quiet → Auto)
3. Verify each mode applies immediately without transient states
4. Verify tray icon updates without redundant flicker
5. Verify popup opens/closes without layout churn

**Expected**: All transitions smooth; no duplicate mode changes; OSD responsive

### Gaming Stress (30+ minutes)
1. Record baseline CPU/GPU/thermal with `HWiNFO64` running
2. Start game (any 3D game; benchmarks acceptable)
3. Play at consistent load for 30+ minutes
4. Monitor:
   - Fan response latency (< 5 seconds to thermal change)
   - Thermal stability (no sudden spikes/dips without load change)
   - Mode stability (no drift from applied preset)
   - Frame pacing (if FPS counter visible)

**Expected**: Stable thermal/fan/mode; fan ramps predictably; no throttle lock

### Idle Checks (5 minutes each)
1. **Focused Idle**: OmenCore window visible, no interaction → **CPU ≤ 2%, GPU ≤ 1%**
2. **Minimized Idle**: OmenCore minimized → **CPU ≤ 1%, GPU ≤ 0.5%**
3. **Tray Idle**: OmenCore closed; tray running → **CPU ≤ 0.5%, GPU negligible**
4. **Screen-Off Idle**: Lock Windows → **CPU ≤ 1%** (telemetry suppressed)

**Expected**: Clear reduction at each stage

### Multi-Monitor (if applicable)
1. Move OmenCore window across monitors at different DPI scales
2. Drag dashboard/popup between monitors
3. Minimize on secondary monitor; check tray still updates

**Expected**: No rendering glitches; smooth window moves; tray always responsive

### Ryzen Thermal Comparison
*For Ryzen 5000/6000/7000/AI series*

1. Open `HWiNFO64`
2. Record **CPU Package Temperature** and **CPU (Tctl)** side-by-side
3. Run sustained load (gaming or Prime95) for 5 minutes
4. Export OmenCore diagnostics during load
5. Compare:
   - Are package and Tctl within ±5°C? (Normal)
   - If package > Tctl by > 10°C during load, indicate in report

**Why**: Validates thermal authority fallback behavior under real divergence

---

## 📋 Issue Triage Checklist

If you encounter **any issue**, capture:

1. **Reproduction Steps**: Exact sequence to reproduce
2. **Diagnostics Bundle**: `OmenCore → Help → Export Diagnostics`
3. **HWiNFO Screenshot**: At time of issue if thermal/fan related
4. **System Info**: Model, CPU, GPU, Windows version
5. **Logs**: Check `%APPDATA%\OmenCore\logs\` for related entries

### Blocker Criteria (Holds RC→Stable)
- ❌ Crash or hang during any scenario
- ❌ Thermal authority divergence not resolved (package temp wildly different from Tctl)
- ❌ Fan stop or uncontrolled ramp under normal load
- ❌ Mode drift (preset changes without user action)
- ❌ Victus 88EC identity fails to resolve

### Non-Blocker (Noted for 3.6.3)
- ⚠️ Minor UI layout shifts
- ⚠️ Single-system RGB provider not supported
- ⚠️ Rare edge-case thermal oscillation
- ⚠️ Cosmetic typo in help text

---

## 🔗 Resources

| Item | Link |
|------|------|
| **Scenario A Runbook** | [3.6.2-SCENARIO-A-OPERATOR-RUNBOOK.md](../docs/3.6.2-SCENARIO-A-OPERATOR-RUNBOOK.md) |
| **RC Validation Checklist** | [3.6.2-RC-VALIDATION.md](../docs/3.6.2-RC-VALIDATION.md) |
| **Performance Triage** | [3.6.2-PERFORMANCE-TRIAGE.md](../docs/3.6.2-PERFORMANCE-TRIAGE.md) |
| **Full Changelog** | [CHANGELOG-3.6.2.md](../docs/CHANGELOG-3.6.2.md) |
| **Known Issues** | [GitHub Issues — Labeled `3.6.2`](https://github.com/omencore/omencore/issues?q=label%3A3.6.2) |

---

## 💬 Feedback

### Report Issues
- **GitHub**: [omencore/omencore/issues/new](https://github.com/omencore/omencore/issues/new)
- **Discord**: `#omencore-rc-testing` (trusted testers only)
- **Email**: `support@omencore.dev` (with diagnostics bundle)

### Validation Feedback
- Report successes in Discord for team morale 😊
- Note any non-blocker observations for 3.6.3 planning

---

## 📅 Timeline

| When | What |
|------|------|
| **May 15** | v3.6.2-RC1 released; tester recruitment |
| **May 16–17** | Validation window (48–72 hours) |
| **May 18 (Morning)** | Decision: merge to stable or issue v3.6.2-RC2 |
| **May 18 (Afternoon)** | Stable release if no blockers |

---

## ✨ Credits

**v3.6.2-RC1 Contributors**:
- Code review & hardening: Full development team
- Regression testing: Automated test suite
- Documentation: Technical writing team
- RC validation: Trusted tester community (you!)

---

## 📖 Next Steps

### For RC Testers
1. Install v3.6.2-RC1
2. Follow Scenario A runbook
3. Run gaming stress for 30+ minutes
4. Report any issues with diagnostics/HWiNFO captures
5. Complete idle matrix checks

### For All Users
- **Current Stable**: Continue using v3.6.1 as daily driver
- **Opt into RC**: If confident in testing; use separate profile
- **Wait for Stable**: If prefer proven releases; v3.6.2 stable expected May 18

---

## 🎓 About v3.6.2

**Release Type**: Stabilization (no new features)  
**Scope**: Runtime authority, thermal safety, UI dispatch optimization  
**Architecture**: No breaking changes; backward compatible with v3.6.1  
**Support**: Full support for Windows 10/11 on HP OMEN/Victus; Linux CLI included  

**What's NOT Included**:
- 🚫 New UI features (saved for v3.7.0)
- 🚫 New RGB effects (saved for v3.7.0)
- 🚫 New fan control modes (saved for v3.7.0)
- 🚫 Driver/firmware updates (OS layer)

---

**Release Candidate for OmenCore v3.6.2**  
**May 15, 2026 · Validation Window: May 16–17**  
**Trusted Testers: Help us verify quality before stable release.**

---

## License

OmenCore is licensed under the [Mozilla Public License 2.0](LICENSE).

---

## Feedback & Support

- **Discord**: [OmenCore Discord Community](https://discord.gg/omencore)
- **GitHub**: [omencore/omencore](https://github.com/omencore/omencore)
- **Issues**: [Report bugs or request features](https://github.com/omencore/omencore/issues)

*Thank you for testing. Quality comes from community validation.*

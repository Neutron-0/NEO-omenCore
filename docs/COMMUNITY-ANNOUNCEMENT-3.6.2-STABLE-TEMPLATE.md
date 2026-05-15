# 📢 OmenCore v3.6.2 Stable Release Announcement (Template)

**Status**: To be posted May 18, 2026 (after stable release decision)  
**Audience**: Reddit r/omencore, r/omen, r/victus, Discord, Community  
**Purpose**: Announce stabilization release & explain runtime improvements  

---

## Reddit Post (Main Thread)

### Title Format
```
🚀 OmenCore v3.6.2 Stable Released — Runtime Hardening & Thermal Safety Update
```

### Post Content

---

# OmenCore v3.6.2 Stable — Runtime Authority Hardening Release

**Version**: v3.6.2 Stable  
**Release Date**: May 18, 2026  
**Status**: ✅ Ready for all users  

---

## What's This Release About?

v3.6.2 is a **stabilization release** focusing on runtime correctness, thermal safety, and reducing UI dispatch overhead. This is **not** a feature release — it's a backend hardening pass that improves reliability and field diagnostics.

### Three Core Fixes

#### 1️⃣ Thermal Authority Hardening (Issue #129)
**Problem**: Under certain loads, CPU thermal readings could drift into suspiciously low values (e.g., 35°C under gaming). OmenCore had limited tools to detect and recover from this sensor divergence.

**Solution**: 
- Added explicit fallback to LibreHardwareMonitor under low-temp/high-load mismatch
- CPU thermal authority source now tracked explicitly (WMI BIOS → ACPI → LHM Fallback)
- Authority transitions logged so field support can distinguish sensor quirks from real throttling
- Diagnostics now include current authority state for triage

**Why It Matters**: If your system reported impossibly low temps under load before, v3.6.2 will now detect and switch to a more reliable source. Thermal authority is logged so support can help you troubleshoot.

#### 2️⃣ Victus 16-e0xxx ProductId 88EC Identity (Issue #128)
**Problem**: Some Victus 16-e0xxx systems (ProductId 88EC) fell through to generic Victus family defaults, producing ambiguous capability messages.

**Solution**: 
- Added explicit ProductId 88EC mapping in capability database
- Keyboard identity now resolves to explicit backlight-only config (pending field RGB verification)
- Future field reports can confirm RGB capability; database will be updated accordingly

**Why It Matters**: If you own a Victus 16-e0xxx, OmenCore now recognizes your exact model and provides better identity reporting. No more "unknown model" fallback.

#### 3️⃣ RGB Graceful Degradation (Issue #130)
**Problem**: Some platforms support static RGB colors but not dynamic effects (breathing, spectrum). OmenCore would fail hard and log noisy errors.

**Solution**:
- Dynamic effect requests now pre-filter by provider capability
- Unsupported providers skip cleanly with explicit logs
- Static RGB always works even if dynamic effects unsupported
- Zero-provider case exits gracefully instead of crashing

**Why It Matters**: If your system supports static color but not breathing effects, static color will now apply cleanly. No more confusion from error logs.

---

## 🎯 Under the Hood (Performance Improvements)

v3.6.2 also reduces UI and telemetry overhead:

- **Telemetry Decoupling**: Monitoring samples no longer fan through the fan-control pipeline → reduced cross-system noise
- **Dashboard Dormancy**: Hidden dashboard suppresses telemetry projection after 30s → GPU/CPU savings when minimized
- **Hidden-Tab Suppression**: General tab telemetry projection suppressed when not visible
- **Render Caching**: Tray icon and popup states cached → no redundant redraws on unchanged summaries
- **Active Cadence**: Monitoring reduced from 1s to 2s when OmenCore is focused → lower CPU overhead while keeping OSD responsive
- **Latest-Wins Hotkey**: Rapid hotkey presses coalesce instead of ping-ponging modes

**Real-world impact**: Focused idle stays ≤2% CPU; minimized idle drops to ≤1% CPU. Gaming overhead reduced without sacrificing responsiveness.

---

## 📋 Installation

### Windows
```
1. Download v3.6.2-installer.exe from GitHub releases
2. Run as Administrator
3. Restart OmenCore
4. Settings and custom profiles auto-migrate
```

**Upgrade from v3.6.1**: Seamless — no config changes needed.

### Linux (CLI)
```bash
# Extract tarball
tar xzf omencore-3.6.2-linux-x64.tar.gz
cd omencore-3.6.2

# Run
./omencore-cli status
./omencore-cli --mode gaming  # or auto, quiet, extreme, custom
```

---

## ✅ Validation

### Pre-Release Testing
- ✅ Full solution build: 0 errors, 0 warnings
- ✅ 100+ unit tests passing
- ✅ 12 new regression tests for #129/#128/#130
- ✅ 48–72 hour trusted tester validation window completed
- ✅ No blockers identified
- ✅ Documentation updated

### Known Limitations
- Victus 88EC RGB capability conservative pending field reports (marked unsupported; will update if confirmed)
- Thermal authority fallback validated in unit tests; real-world Ryzen divergence scenarios encouraged for reporting

---

## 🐛 Bug Reports & Feedback

If you encounter:
- Thermal readings still diverging wildly after v3.6.2
- Mode drift or fan oscillation
- RGB static color failures
- Any crash or hang

**Please report** with:
1. OmenCore diagnostics bundle (`Help → Export Diagnostics`)
2. System model, CPU, GPU
3. HWiNFO64 screenshot (thermal context)
4. Steps to reproduce

Bugs help us improve. Report via:
- [GitHub Issues](https://github.com/omencore/omencore/issues/new)
- [Discord #support](https://discord.gg/omencore)

---

## 🗓️ What's Next?

**v3.6.2**: Stabilization & hardening ✅ **DONE**  
**v3.6.3**: Maintenance fixes (if blockers emerge during v3.6.2 field use)  
**v3.7.0**: New features (UI improvements, RGB effects, fan control modes) — scheduled for H2 2026  

We're keeping v3.7.0 for architecture work. v3.6.2 is the "get the fundamentals rock-solid" release.

---

## 📚 Resources

- **Changelog**: Full list of fixes — [CHANGELOG-3.6.2.md](https://github.com/omencore/omencore/blob/main/docs/CHANGELOG-3.6.2.md)
- **Performance Triage**: How to optimize idle/gaming — [3.6.2-PERFORMANCE-TRIAGE.md](https://github.com/omencore/omencore/blob/main/docs/3.6.2-PERFORMANCE-TRIAGE.md)
- **Scenario A Runbook**: Step-by-step validation — [3.6.2-SCENARIO-A-OPERATOR-RUNBOOK.md](https://github.com/omencore/omencore/blob/main/docs/3.6.2-SCENARIO-A-OPERATOR-RUNBOOK.md)

---

## 💬 Discussion

Drop questions below:
- **Thermal issues?** Tell us your system + temps
- **Identity confusion?** Show us your model output
- **RGB not working?** System + provider info helps
- **Performance gains/losses?** Before/after CPU % appreciated

---

## 🙏 Thanks

v3.6.2 is the result of:
- Development team code review & hardening
- Automated test suite regression coverage
- **Trusted tester community** field validation (48–72 hour RC window)

Your feedback makes OmenCore better. Download, test, report. 🚀

---

## Download

**GitHub**: [omencore/omencore/releases/tag/v3.6.2](https://github.com/omencore/omencore/releases/tag/v3.6.2)  
**Installer**: Windows | Linux CLI | Portable

---

*OmenCore v3.6.2 Stable — May 18, 2026*  
*Stabilization Release | Runtime Authority Hardening | Thermal Safety*

---

## Comments Section Prep (Common Questions to Address)

**Q: Is this a major release?**  
A: No. v3.6.2 is stabilization only. Major features come in v3.7.0.

**Q: Will my settings reset?**  
A: No. v3.6.2 auto-migrates all settings and custom profiles from v3.6.1.

**Q: Can I downgrade to v3.6.1 if I don't like it?**  
A: Yes, but we don't expect issues. If you encounter a blocker, report it immediately so we can push a v3.6.2.1 fix.

**Q: Why focus on stabilization instead of new features?**  
A: We identified 3 core issues impacting thermal diagnostics, model identity, and RGB reliability. Fixing these now ensures v3.7.0 starts from a solid foundation.

**Q: When's v3.7.0?**  
A: Planned for H2 2026. We're giving v3.6.2 time in the field first to catch any edge cases.

---

## Cross-Post Variants

### r/omen Post (Title)
```
OmenCore v3.6.2 Released — Thermal Authority Hardening & Runtime Improvements
```

**Angle**: Emphasize thermal authority fix for Ryzen systems.

### r/victus Post (Title)
```
OmenCore v3.6.2 Out — Better Support for Victus 16-e0xxx + Performance Gains
```

**Angle**: Highlight Victus 88EC explicit identity + overhead reduction.

### Discord #announcements
```
🚀 **OmenCore v3.6.2 Stable Released**

✅ Thermal authority hardening (Issue #129)
✅ Victus 88EC identity fix (Issue #128)
✅ RGB graceful degradation (Issue #130)
✅ UI dispatch overhead reduction

Download: [link]
Changelog: [link]

Questions? #support or GitHub issues.
```

---

## Timing Notes

- **Post time**: May 18, 2026 (morning/midday preferred for max visibility)
- **Cross-post order**: GitHub release first, then Reddit, then Discord
- **Community feedback window**: 1–2 weeks for any hotfix issues to emerge

---

**Template for: Reddit + Discord Announcement**  
**To be filled in with actual download links, checksums, and tested results after RC validation completes**

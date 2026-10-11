# OmenCore v4.5.0 Roadmap

**Status:** Planning (opened 2026-10-11, the day 4.4.5 shipped).
**Previous cycle:** `docs/ROADMAP_v4.4.5.md` / `docs/CHANGELOG_v4.4.5.md`.
**Running notes for this cycle:** `docs/CHANGELOG_v4.5.0.md`.

Working rules are unchanged: issues, exports, forks and PRs are field evidence; separate *confirmed* (a reporter saw it
on real hardware) from *implemented, pending confirmation*; fix root causes with a test; keep the suite green; reply on
GitHub with what is fixed, what is pending and what we need. Nothing is released, tagged or closed without the maintainer.

## 1. Where 4.4.5 left things

4.4.5 shipped a lot that has only run in software tests. The first job of this cycle is to turn those into confirmed or
corrected items from real reports, before adding more on top. What each one is waiting on:

| Item | Waiting on | Who |
|---|---|---|
| 8BD4 single-zone keyboard (#212) | the RGB payload probe result (which numbered step, if any, lit it) | VoRtam4 |
| Factory fan curve, 8D87 (#189) | a real run in Performance mode vs firmware Auto, and the IR step-down guess checked | mbilykov |
| 8E9F keyboard route (#224) | whether the Lighting page does anything on Darfon `0D62:30BF` | JeanHX5 |
| 8E9A board and keyboard route (#142) | Lighting page result and the keyboard's HID ids | Keith1341 |
| 8A13 board, MUX (#225) | enabling MUX in a build, then a reversible test | Dsedan15 |
| 8E35 latency and SMU table (#222) | confirmation of the faster response; a PM-table dump for the 8940HX | reporter |
| 8DD0 temperature source (#221) | an answer to what looks wrong (ACPI zone vs Tctl) | reporter |
| NVIDIA power unlock | the first run through OmenCore on a real laptop (start with a Victus) | timmyy123, bobshmo, testers |
| Updater relaunch | one real update from 4.4.5 to the next release | any |
| Auto-hidden taskbar fix | a machine with the taskbar set to auto-hide | any |

## 2. Already on main for the next release

- **Release hygiene (local commit `c46c483`, not pushed yet).** The Linux QA tag release step is removed, the release notes
  carry the changelog, and the CI actions are off Node 20. Verify on the first CI run after the push.
- **NVIDIA guard picks the HP mode by firmware policy.** 4.4.5 always sent the legacy Performance byte (`0x01`) before a
  CURRENT write on a Victus. V1 and newer firmware use `0x31`, as the rest of OmenCore already does; V0 keeps `0x01`.
  Found by comparing with bobshmo's standalone 1.2.0, which made the same change. On a V1+ board the old guard either
  failed closed or set a mode the firmware ignored, never wrote without preparation. Test: `NvidiaPowerServiceTests`.
  *Pending hardware confirmation* (the claim is bobshmo's; it matches our own code).

## 3. Candidate work, by theme

Ordered within each theme by value and by how little new hardware risk it carries. Items marked (data) need a field
export first; none of those are started without one.

### A. Fan control
- Widen the factory fan curve beyond 8D87 only with each board's own tables (data). Add the Unleashed table for 8D87.
- Settle the IR sensor step-down (the 4.4.5 curve uses midpoints between rising thresholds, a guess) from a real capture.
- Real RPM on the 2025/2026 MAX boards: HWiNFO shows no fan sensor on 8E9F, so look for an EC or WMI source only if a
  tester volunteers; otherwise keep the estimate and keep it labelled.
- 8E35 fan sticking at the high-RPM failsafe after a spike (#222): needs an export taken right after it happens (data).
- **Max auto-return, opt-in, off by default** (Ohman's rule). Return Max to Auto after CPU and chassis stay under 60 °C
  for two minutes, or after a 15, 30 or 60-minute timer. Release only through `HpWmiBios.ReleaseMaxAndHandBackToBios()`,
  from inside `FanService`, so `FirmwareMaxPairingGateTests` stays green. Tests for the cool-down and timer paths.
  *Implemented, pending hardware confirmation.*
- **Fan-stall guard (data-gated).** A fan reading 0 RPM while its duty is above zero for a set time forces Max and warns.
  Only on boards whose RPM is actually read: 8E9F has no fan sensor, so it stays off there. Needs an export from a board
  that has stalled, or a test rig.

### B. RGB
- 8BD4 (#212): build the fix from the probe result (data). If none of the five layouts lights it, try a different
  command, not more layouts.
- Validate the Darfon `0D62:30BF` route on 8E9F and 8E9A; if it works, mark the PID verified in `DojoKeyboardMcu`.
- Linux per-key RGB for the 2025 MAX boards (#179, #151, #87): port the Dojo backend to hidraw from the Windows code and
  mbilykov's write-up. Only worth doing with a named tester (mbilykov offered).
- Check the 8BB3 keyboard entry: the keyboard database calls `8BB3` the OMEN 30L desktop, while an older report calls it
  the OMEN Transcend 16 (data).

### C. NVIDIA and GPU
- Read-only **HP WMI scan** (thermal policy, Extreme/Unleashed support flags) as a diagnostics line. Lowest-risk idea
  from bobshmo's 1.2.0.
- **PCF Max** (HP-side power cap): evaluate after the scan, behind the same opt-in and safety checks as MAX/CURRENT.
  Only with an owner willing to test; HP caps are exactly where firmware surprises live.
- MUX test for 8A13 (#225): enable it in a build and post the exact steps; reversible from OmenCore or the BIOS.
- 8D87 EC GPU power unlock stays off until an owner can validate the EC writes.
- Extend the same read-only scan to HP's CPU+GPU power budget (`0x29`), GPU power (`0x22`) and graphics mode (`0x52`).
  Ohman documents these; OmenCore does not use them. Read only: no writes until a board's response has been captured.
- **Will not do:** VBIOS flashing, bundling `nvflash`, or voltage/clock tuning in OmenCore. A bad flash can brick a laptop,
  and NVFlashB's licence is unclear (its README says MIT, its LICENSE file says GPLv3). The panel keeps asking users to
  supply their own `nvflash64.exe` or a ROM file.

### D. AMD and power
- Widen the SMU PM-table layouts (Ryzen 7 260, Ryzen 9 8940HX) from exports (data).
- A board-level CPU power-limit story for the MAX 16 Intel boards (8E9A reports 100 W+ in Gaming Hub against about 55 W
  with OmenCore). No limits are written today; this needs data before any write.
- The requested extra undervolting features (MENTRAUZ): get the specifics first; the 8E9A log shows the PawnIO MSR write
  failing on that board, so "more features" starts with why the existing path fails there.
- **CPU throttle reason, Intel first** (Ohman 1.1). Read the thermal and power-limit throttle state through the existing
  PawnIO MSR path (IA32_THERM_STATUS, `0x19C`, and its PROCHOT and power-limit bits, to be checked against the Intel SDM
  before use) and log it in the diagnostics export. "Why is my CPU slow" reports then show which limit fired. Read only.
  AMD needs its own register research before anything is shown.

### E. Sensors and temperature authority
- Per-board CPU temperature source (#221): AMD boards where the ACPI zone lags (8DD0 showed 56 C against Tctl 71-93 C).
  Needs the reporter's HWiNFO comparison before changing the default for every AMD board (data).
- Record which boards' BIOS CPU index (`0x23` index 1) is not the CPU (8E9A reads a constant ~28 C; it fits the Ambient
  index from #189) and prefer the worker sensor there. The OMEN MAX model override already does.

### F. Resource use
- Run the new memory breakdown in the diagnostics export on a real HP machine and attack the biggest block (the 4.4.5
  export showed ~280 MB private and 2,572 handles in the main app, 37 MB in the worker).
- Pause optional loops (automation, update check) while hidden, or merge them into one timer.
- Keep GPU load polling cheap (done in 4.4.5); look for the next per-poll cost in the same export.

### G. Release and project hygiene
Status: the first three items below are done in the workflows (see `docs/CHANGELOG_v4.5.0.md`, "Release and CI"). The
tag path is unverified until the next release tag.
- **Linux QA workflow:** its tag job fails with "Resource not accessible by integration" on every tag because it lacks
  `contents: write`; the real release comes from Release Build, so it is only a red mark. Fix the permission or drop the job.
- **Release notes:** the Release Build workflow overwrites the notes with its template (downloads and SHA256 table). Have
  it include the changelog, keeping the SHA256 table the updater reads.
- **CI actions:** the log warns that Node 20 actions are forced to Node 24, and that `ubuntu-latest` moves to Ubuntu 26
  on 2026-10-19. Update the pinned action versions and check the Linux jobs on the new image.
- **Linux package:** the keyboard suspend hook is not in the zip; either include it or keep it documented as a repo download.
- **Code signing** for the installer (SmartScreen shows "unknown publisher"). A cost and trust decision for the maintainer.
- **Housekeeping:** update `CLAUDE.md` (it still names 4.4.1 as current); close the 4.4.1 close-on-ship issues that are
  still open (#205, #214, #215) once confirmed; decide on PR #147 (reviewed three times, no rework; invite a resubmission
  of the tray and uptime parts and close it).
- The log-hygiene gate (`ReleaseGateCodeHygieneTests`) keeps a line-number baseline for bare catches; keep new code on
  typed catches.

### H. Hotkeys and coexistence (new from the Ohman research)
- **Hotkey retry that ends in a message.** `HotkeyService` retries a failed key only at startup (`RetryPendingHotkeys`
  runs from `Initialize`) and then drops it with no final log. Retry for a bounded time, then log and surface "Ctrl+Shift+O
  is held by another app". Test with a fake register failure. This is the cause of the three silent failures on the 4.4.5
  test machine (not a new bug).
- **Ohman in conflict detection.** Add it to `ConflictDetectionService`, modelled on the OGH entry. The download is
  `Ohman.exe`, so confirm the process name on a running copy before adding it.
- **Linux: competing fan writers.** `omencore-cli diagnose` and the daemon's start-up log warn when a known fan service is
  active (victus-fan, victus-backend, omen-fan-control, omenctld), since they all write `pwm1_enable`. The writer lock
  only covers our own processes.
- **Kernel note for #219.** A third-party README says upstream commit `46be1453` ("platform/x86: hp-wmi: add manual fan
  control for Victus S models") adds manual fan control for Victus S. Check that commit on kernel.org before using it in
  the #219 reply.

### I. Stretch: not started (move to 4.6 if this cycle runs out)
- **Battery refresh-rate limit** (Ohman). Optional and off by default: drop to a lower rate on battery, restore on AC.
  `DisplayService` has refresh-rate control but no battery branch.
- **Benchmark and FPS overlay** (Ohman 1.3.0). FPS, 1 % and 0.1 % lows, frame time, and temperature, power, clocks and
  fans, with run history. Uses PresentMon 2.6.0. Before starting: check PresentMon's licence, set a resource budget (4.4.5
  cut overhead, so this must not undo it), and make sure the overlay never takes clicks (Ohman's 1.3.0 notes list a fix for exactly that).
- **"Firmware holds the fans" label** (Ohman 1.3.1). Say "firmware is holding fans on its own curve" instead of showing a
  curve as active. `FanService` already detects the stuck-high case; this changes the wording only.
- **Power gain, CPU+GPU budget `0x29`** (Ohman: 0 to +15 W on the Dynamic Boost budget). Not started. Only after the read-only
  scan has a captured response from a board we can test.

### J. Research log
- 2026-10-11. **Ohman** (`P4R1H/ohman`, [repo](https://github.com/P4R1H/ohman), [releases](https://github.com/P4R1H/ohman/releases)):
  releases 1.0.6 (15 Sep) to 1.3.1 (10 Oct). Taken from the README and releases page through a summarising fetch, not checked
  against the source. Read the 1.3.1 changelog directly before quoting it in public.
- **OmenCtl** ([yunusemreyl/Omen-Control-App](https://github.com/yunusemreyl/Omen-Control-App), GPL-3.0) and
  **victus-control** ([Batuhan4/victus-control](https://github.com/Batuhan4/victus-control), GPL-3.0), and
  **omen-fan-control** ([arfelious/omen-fan-control](https://github.com/arfelious/omen-fan-control), GPL-3.0): README-level
  feature and behaviour notes only.
- Licence rule for all of these: documentation sources only. No code, key maps or protocol tables are copied from them.

## 4. What counts as done for 4.5.0

- Every item from section 1 has either a reporter's confirmation, a corrective fix, or an honest "still unconfirmed" in the notes.
- New board entries each cite their export and add a test; unproven features stay off.
- Full suite green (0 build warnings), installer and both zips built by CI, and the release notes carry the changelog
  and the hash table.
- The Needs-field-confirmation list is rewritten for whatever 4.5.0 adds.
- Every new item in sections 3A, 3D and 3H has a test, or is labelled *implemented, pending confirmation* in the changelog.
- Anything in 3I that does not ship is listed as moved to 4.6, not dropped silently.

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
| 8BD4 single-zone keyboard (#212) | nothing: the probe result arrived 2026-10-12 and points at colour offset 1, see 3K.11. Next is a build to retest | VoRtam4 |
| 88D1 OMEN 15-en1xxx (#228, new) | an entry from the export (3K.12), then Guided Fan Verification with the RGB check | korokhidup |
| Factory fan curve, 8D87 (#189) | a real run in Performance mode vs firmware Auto, and the IR step-down guess checked | mbilykov |
| 8E9F keyboard route (#224) | **Answered 2026-10-11:** the light bar works, the keyboard RGB does not. Next step is a 30BF probe, see 3K.5 | JeanHX5 |
| 8E9A keyboard route (#142) | nothing for the keyboard: Keith1341 confirmed 2026-10-13 that lights work (3K.14). Needs a Guided Fan Verification and which effects work | Keith1341 |
| 88ED Victus 16-e0xxx (#229, new) | an answer from the maintainer: the entry exists, the notice is about verification (3K.13) | PRIMUS626 |
| 8A13 board, MUX (#225) | enabling MUX in a build, then a reversible test. Guided Fan Verification passed 6/6 (82/100) on 4.4.5, so the entry can be promoted, see 3K.6 | Dsedan15 |
| 8E35 latency and SMU table (#222) | confirmation of the faster response; a PM-table dump for the 8940HX. The three files posted on 2026-10-11 are `config.json` copies (two are byte-identical), not diagnostics exports, so the SMU table and the stuck-fan state are still missing, see 3K.4 | nyn1fe |
| 8DD0 temperature source (#221) | an answer to what looks wrong (ACPI zone vs Tctl) | reporter |
| NVIDIA power unlock | the first run through OmenCore on a real laptop (start with a Victus) | timmyy123, bobshmo, testers |
| Updater relaunch | one real update from 4.4.5 to the next release | any |
| Auto-hidden taskbar fix | a machine with the taskbar set to auto-hide | any |
| 8BC2 board entry (#227, new) | export received 2026-10-12; build the entry, then the reporter's Lighting and fan result, see 3K.2 | ZycoJiun |
| 8BD4 second owner (#226, new) | a log taken at the moment the fans "max at 70 and stick"; the calibration findings are already in, see 3K.1 | WilliamMillerDave |
| Transcend 14 random hibernate (Discord, new) | version, a diagnostics export, and the Windows power events around a hibernate, see 3K.8 | BluSleeperOP |

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
- 8BD4 (#212): the probe result is in (3K.11): step 2, colour directly after the count byte, is the one that lights it. Build the
  single-zone payload from that (WP-14), also for 8BC2, and retest on the 8BD4 owner before calling it confirmed.
- Darfon `0D62:30BF`: works on 8E9A (#142, reporter-confirmed 2026-10-13), does nothing on 8E9F (#224, light bar only). See 3K.14 for
  the difference. Mark it verified for 8E9A only, not as a PID.
- Linux per-key RGB for the 2025 MAX boards (#179, #151, #87): port the Dojo backend to hidraw from the Windows code and
  mbilykov's write-up. Only worth doing with a named tester (mbilykov offered).
- Check the 8BB3 keyboard entry: the keyboard database calls `8BB3` the OMEN 30L desktop, while an older report calls it
  the OMEN Transcend 16 (data).

### C. NVIDIA and GPU
- Read-only **HP WMI scan** (thermal policy, Extreme/Unleashed support flags) as a diagnostics line. Lowest-risk idea
  from bobshmo's 1.2.0.
- **PCF Max** (HP-side power cap): evaluate after the scan, behind the same opt-in and safety checks as MAX/CURRENT.
  Only with an owner willing to test; HP caps are exactly where firmware surprises live. Discord findings on this are in 3K.7.
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
  Needs the reporter's HWiNFO comparison before changing the default for every AMD board (data). A second board shows the same
  freeze flag (#228, 88D1: CPU "frozen" at 47 C for 22 readings, GPU at 45 C for 74 readings; see 3K.12).
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
- **OMEN key can open the OSD (user request, 2026-10-14, WP-21).** Settings already lets the OMEN key run an action
  (`OmenKeyActionOptions` in `SettingsViewModel.cs`: quick popup, window, performance, fan mode, max cooling, external app, nothing),
  but none of them is the OSD. The OSD is only reachable from its own hotkey (default Ctrl+Shift+F12). Add a "ToggleOsd" action.
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
- **TPP calibration scan (bobshmo's Prophecy Power Unlocker 1.2.2): not planned for 4.5.0.** It targets laptops that do not advertise
  default and maximum TPP bounds (the Barcik82 case in K.7). It tests every whole-watt target in a chosen interval (default 100 to
  180 W) by applying it to HP's PCF controller and checking the readback, then restores the original snapshot. That is a write loop on
  the firmware side, its own notes say no live calibration has been run, and it does not prove electrical or thermal safety. OmenCore
  keeps PCF and TPP read only until an owner has validated single writes (3C). Revisit only after that, behind the same opt-in as
  MAX and CURRENT, and never from the panel without a confirmation that names the interval.
- **Power gain, CPU+GPU budget `0x29`** (Ohman: 0 to +15 W on the Dynamic Boost budget). Not started. Only after the read-only
  scan has a captured response from a board we can test.

### K. Field evidence from the 4.4.5 reports (2026-10-11 and 12)
Everything below was read from the attachments (exports, logs, screenshots) unless it says otherwise. *Confirmed* means seen
in the evidence; every fix is still *to do* and needs a test.

**K.1 Calibration wizard on level-only boards, and "stuck at 2000 RPM" (#226, board 8BD4; also #227, 8BC2)**
- Evidence: on the 8BD4 the RPM source is **Estimated** (the log says "Fan RPM readback returned null"). Every RPM the wizard
  shows is the fan *level* read back times 100. The CPU fan reads back level 20 for every step from 20% to 100%, and the
  100% step "passes" at 2000 RPM. The GPU fan reads back 25 to 29 when asked for 33 and 44 (60% and 80%, both "fail") and
  only the 100% step, which uses Max, reaches 3090 (range 2800 to 5700). So "balanced fans stuck at 2000 RPM" is level 20
  (the V1 hint level) turned into a number, not a measured speed.
- Fix: (a) when the RPM source is Estimated, the wizard says it cannot measure RPM on this board and does not build a
  duty-to-RPM map (or labels the map "level only"); (b) a stable, repeated, clamped readback below the request is a firmware
  ceiling at any step, not only at 100% (`FanVerificationService` already does this at 100%: the 8BC2 log shows "settled at
  firmware ceiling level 44/55"); (c) show "estimated" in the results table, as the main screen already does.
- Answer for the reporter: the 2000 figure is an estimate. Say so in the #226 reply when this is fixed.

**K.2 8BC2 gets its own board entry (#227, Victus 16-r0xxx Intel)**
- Today it resolves by model name to the 8BBE entry ("Low" confidence in the export). Measured: i7-13700HX, RTX 4060, BIOS
  F.31, thermal policy V1, **2 fans**, max fan level 55, level readback works (20/20), RPM readback null, keyboard topology
  `OneZoneWithNumpad (0x04)`, ACPI zone `TZ00_0` frozen near 28 C while the CPU is at 82 to 96 C.
- Entry: `ProductId = "8BC2"`, `SupportsRpmReadback = false` (8BBE does not set it, so the card says "Supported"), curves and
  GPU boost off, `UserVerified = false`, Notes citing #227. Add a test.
- Keyboard: no database match, so the V2 engine finds nothing and falls back to the V1 path. That path still declares **4**
  zones in the colour table (`SetColorTable: ZoneCount=4` in the log) for a keyboard that reports one: the 4.4.1 zone-count fix
  only reaches `WmiBiosBackend`. The reporter's saved config also holds an observation "Keyboard zones changed" (WMI BIOS,
  `#00FF66`, 2026-08-10), so colour may already work here. Do not guess: add the keyboard entry and ask for a Lighting result.
- Cross-check: Ohman's 1.1.1 notes list 8BC2 as verified for its basic controls (documentation only, not our evidence).
- Confirmed safe behaviour: thermal protection fired at 92 C and 96 C ("forcing fans to 100%") and released at 70 to 76 C.

**K.3 The capability card contradicts the live probes (8BD4 and 8BC2 screenshots)**
- 8BD4 shows "Real fan RPM readback: Supported" and "4-zone keyboard RGB: Supported" while the log says RPM readback is null and
  the keyboard topology is one zone. 8BC2 shows "Real fan RPM readback: Supported" and "Power limit (PL1/PL2): Supported" while
  the log says RPM is null and the PL1/PL2 MSR read returned 0 W / 0 W; it also shows "Custom fan curves: Not supported" next
  to "Independent CPU/GPU fan curves: Supported".
- Fix: where a live probe disagrees with the database, the card shows the probe ("Estimated", "not readable on this board"),
  and the database default for `SupportsRpmReadback` stops being true for boards whose export shows null. Test per case.

**K.4 Watchdog failsafe can hold fans high for minutes (#222, board 8E35) (hypothesis from code, not yet confirmed)**
- The reporter's three "exports" are config files, so the stuck state itself is not captured. What the code does:
  `HardwareWatchdogService` sets fans to 90% when monitoring looks frozen and releases only after readings stay at or below
  `FailsafeSafeReleaseTempC` (65 C) for 15 s, re-asserting every 15 s. A CPU that settles at 66 to 75 C after a spike (the
  reporter switched Windows boost to "Efficient Enabled") never reaches that, and changing a preset clears the failsafe, which
  matches "it recovered when I tweaked the fan settings".
- Needed first: a real diagnostics export taken while stuck, and the `WATCHDOG` lines from the log. If they show the failsafe,
  release when readings are fresh and below the thermal-protection ramp (90 C) minus a margin for 15 s, since `FanService`'s own
  protection covers anything hotter. Also find why monitoring went stale for more than 90 s twice.
- Separate, same reporter: UI stutter (about 3 FPS) when scrolling or dragging curve points. Direct EC would not change that, it
  is UI-thread work. Ask for the `runtime-performance.txt` from a real export; profile the curve editor's drag path (see F).

**K.5 8E9F keyboard (#224)**
- Result: the light bar works, the keyboard RGB does not. The Dojo route to Darfon `0D62:30BF` is therefore not enough as it
  stands. Mark the route "tried, no effect" in the keyboard database notes, keep the light bar, and write a 30BF probe in the
  style of the 8BD4 RGB payload probe (one change per step, each logged) before touching the route again. Evidence needed from
  JeanHX5: which numbered step, if any, moves the keys.
- The same comment asks for fan curves in percent instead of RPM. OmenCore's curves are already percent based; they are off on
  this board because RPM cannot be verified. Candidate: allow level-based curves where level readback verifies, labelled
  "estimated". Needs an owner willing to run it.
- The screenshot shows the SMU line "this CPU's SMU power table layout has not been identified". Add the 8E9F's CPU to the PM-table
  list in 3D and ask for `tools/SmuProbe --pmtable` output, as the message itself says.

**K.6 Smaller items**
- 8A13 (#225): Guided Fan Verification passed 6/6 on 4.4.5 (82/100, RPM estimated) and the RGB pattern visibly changed the
  keyboard. Promote the fan and RGB flags in the entry, keep MUX off. The final "white" shows as pink, so ask for a photo; it may
  need a per-board colour balance.
- Guided Fan Verification on a hot machine (8BC2, CPU 96 C at under 30% load): the thermal emergency fired during the CPU 60% step
  and the step then "failed". Mark such a step "interrupted by thermal protection" and re-run it after a cool-down, and wait for
  temperatures below 85 C before starting.
- #226 also reports fans "max at 70 C and stuck". Nothing in the posted logs shows it (OmenCore's protection runs at 90 and 95 C),
  so it may be the BIOS curve. Unconfirmed: ask for a log taken at that moment.
- #227 shows ThrottleStop detected (the existing conflict check) and a failed TCC-offset write (PawnIO MSR read fail). Not new.

**K.7 NVIDIA power unlock: Discord findings (bobshmo, Barcik82; 2026-10-11 and 12)**
All of this is single-laptop evidence from bobshmo's own machine unless stated. None of it is confirmed through OmenCore.
- **Out-of-range values reset the plan.** In his 1.2.0, PCF Max is a Max/Default toggle locked to the detected GPU's range;
  setting a value below the stock default or above NVIDIA's stock max made the power plan fall back to Balanced with cTGP and
  PPAB off. Check that the panel's MAX and CURRENT inputs are clamped to the same range (stock default to NVIDIA stock max),
  refuse anything outside it before any write, and add a test.
- **TPP (Total Platform Power)** appears in his Victus tab: stock 125 W combined, max 180 W on his machine. His finding: the
  unlock does not hold unless TPP is above the requested power state; once TPP allows it, PCF can be raised and the unlock works.
  Add TPP, if it can be read, to the read-only HP WMI scan in 3C and to the panel's status text. It may be the same budget as
  Ohman's `0x29`; that is unverified. Read only until an owner has tested a write.
- **"If CURRENT won't stick, re-toggle the HP state and unlock."** Put that in the panel's help text. The Victus guard already
  sets the HP performance mode before a CURRENT write, so check whether a re-toggle adds anything.
- **Barcik82 (not a Victus, stock 115 W, the tool expected 140 W):** bobshmo's reading is that this laptop is locked like his was,
  and that PPAB must not be disabled. Barcik82's measurement shows why: with PPAB off the GPU fell from 115 W to 90 to 100 W and
  the CPU rose to 130 W boost and 90 W sustained, so the adapter is already the limit. The panel should say "do not disable PPAB"
  and show the stock max it read next to the value it expected, so a mismatch like 115 W against 140 W is explained, not just an error.
- **RTX 5050 is untested** (bobshmo). List it as untested in the GPU support text. After any NVIDIA driver release, the unlock
  needs a re-check ("assuming HP or NVIDIA doesn't catch up"), and the panel should say a driver update may undo it.
- Unchanged: his auto resolver ships NVFlashB. OmenCore still does not bundle it (licence unclear, see the *Will not do* list).

**K.8 OMEN Transcend 14: random hibernate (Discord, BluSleeperOP; no export yet)**
- The post says the app makes the laptop hibernate at random, that it runs much cooler than it used to, and that the laptop is
  "still undetected as an official HP model". The Windows database has no entries for the Transcend 14 boards (8C58 for 2024, 8E41
  for 2025); only the Linux code names them. So the detection complaint is expected, and the entries need an export.
- Code check: OmenCore never calls a hibernate or sleep API. It does switch power plans (`PowerPlanService`, and the optimiser's
  `powercfg /setactive`), pauses monitoring on suspend, and re-asserts fan state. Any link is therefore indirect and
  *unconfirmed*: a plan switch with different sleep or hibernate timers, a Modern Standby wake loop that ends in hibernate, or fan
  and thermal state on resume. Ohman's 1.2 notes mention a battery fan fix on the 2025 Transcend 14 (documentation only).
- Needed: OmenCore version, a diagnostics export (it carries `resume-recovery.txt` and the session sentinel), and the Windows
  evidence: Kernel-Power events 41 and 42 around the hibernate, `powercfg /sleepstudy`, and `powercfg /requests`.
- Plan: add the 8C58 and 8E41 entries only from an export. Add the last Kernel-Power sleep and hibernate transitions, with their
  reasons, to the diagnostics export, so the next report of this kind carries the cause.

**K.9 Guided Fan Verification and the idle fan floor, on more boards (found in the full GitHub audit, 2026-10-12)**
- **#207 (88F8), 4.4.1 retest: 5/6.** At 100% the CPU fan settles at level 43/55 (78%) after Max and the direct `SetFanLevel(55)`;
  the GPU fan on the same run settles at 46/55 and passes as `FirmwareCeiling`. `FanVerificationService.IsFirmwareCeilingPlateau`
  (line 885) needs 80% of the expected level, so the CPU fan fails by 2 points. Idea: judge a plateau against the best level
  any fan reached in the same run (43 is 93% of 46), and keep the existing rule that a board whose Max never rose past the 60% step
  still fails (88F8 had that before). Tests go in `FanVerificationCeilingTests`.
- **#218 (878A), 4.4.1 retest: 1/6.** The 30% and 60% steps end in about 50 ms as "RPM=0 ... FAIL" because the per-fan write is
  refused: `WMI fan control failed for fan 0, level 16` (and level 33, and fan 1). At 100% Max moves only the GPU fan (about 5200
  estimated); the CPU fan reaches level 22 to 23. Ohman's 1.1 notes say its 878A fix was EC fan control through PawnIO
  (documentation only), so an EC path for 878A and the sibling 878C is a candidate, with an owner test first. Keyboard: the
  reporter sees a single bright-red backlight, which matches the "Normal" topology and the 4.4.1 backlight-only gating.
- **#217 (88EE): 6/6 on 4.4.1 (68/100).** New question from the reporter: the fans never stop at idle (about 2000 RPM, original
  OGH stopped them) and no preset changes it. #226 shows the same 2000 figure and #143 (8DCD) reports a fan that "drops below 2000
  and stays". On V1 boards the RPM is the level times 100, so 2000 is level 20. OmenCore sends `SetFanLevel(20, 20)` as a "V1
  krpm hint" when handing back from Performance or Max (`WmiFanController` around lines 526 to 538, 1164 to 1168 and 1224;
  `HpWmiBios` line 1237). Hypothesis, *unconfirmed*: on boards whose firmware Auto would stop the fans, that hint may hold them
  at level 20. Needs an owner who can hear or measure the fans after Restore Auto and after quitting OmenCore.
- **New report 2026-10-15 (no issue number yet, board not given):** a user says the fans sit at about 2300 RPM at all times, even in
  Eco or Quiet, and that OMEN Gaming Hub drops the fans to 0 when heat and load are low. That is the fourth report of the same
  symptom (#217, #226, #143). 2300 is level 23 on a V1 board (level x 100), close to the level 20 hint above but not equal, so the
  hint cannot be the whole story and may not be the cause at all. Unknowns to ask for: board ID, the Windows power plan, whether
  the fans stop when OmenCore is closed (Restore Auto, then quit), and whether they stop with OmenCore never started since boot.
  If they stop with OmenCore closed, we are holding a floor; if not, the BIOS Auto curve on that board never stops them.
- **#211 (8BBE), stuck fans: two exports from 2026-10-04 (4.4.1).** Telemetry shows about 5800 and 6100 estimated RPM at CPU 27.9 C
  and 3% load while the reporter switched Balanced and Quiet; the log shows the keepalive re-asserting Cool every 30 s. The export
  records no Max flag, fan mode or level readback ("LastMaxModeExternalResetDetails: No external Max-mode reset detected"), so the
  cause is still open. Needs the export additions in WP-04.

**K.10 Reporter threads waiting on something from us (maintainer-owned unless noted)**
- #215 (88F7): RazrV33 agreed on 2026-10-03 to test undervolt; the maintainer offered a flagged test build (Test Apply auto-reverts
  after 30 s). Not delivered yet.
- #181 and #123 (8D41, RTX 5090/5080): waiting on the OGH A/B run (Run A without Gaming Hub, Run B after it set a profile, exports
  named A and B). #123's Linux part is kernel-side (`platform_profile_victus_s_set_ec()`), so the reply points upstream.
- #219 (Linux 8D2F): Nova-8056 asked on 2026-10-05, unanswered: how to write and where to send a kernel board-ID patch, whether
  the out-of-tree drivers they found (`saikiranworks/hp-omen-dkms`, `yunusemreyl/hp-wmi-extended`) are safe, and what to check.
  There is no `platform_profile`, no `hp_wmi` lines in `dmesg`, and a CLI screenshot. See WP-09.
- #179: mbilykov will test a Linux per-key RGB build when one exists.
- Waiting on reporters' exports: #201 (8EDC needs its own entry), #143 (fan drop on 8DCD), #133 (UI performance, wants
  `runtime-performance.txt` from 4.4.x), #158 (8A44 verification pass), #145 (8D40 Battery Care and whether the profile survives a
  relaunch), #103 (Ryzen AI 9 HX 375 Curve Optimizer, export after Apply), #195 (8E35 rerun expected to read 6/6, then
  `UserVerified`), #221 (the reporter has not said what looks wrong).

**K.11 8BD4 RGB payload probe result (#212, VoRtam4, 4.4.5, 2026-10-12)**
- Evidence (observed by the reporter, three runs, same each time; log extract `OmenCore_8BD4_v4.4.5_RGB_payload_probe_relevant_logs.txt`).
  Firmware topology `OneZoneWithNumpad (0x04)`, backend WMI BIOS ColorTable, BIOS F.29. Per probe step:

  | Step | Layout sent (pure red) | Keyboard | Readback at offset 25 |
  |---|---|---|---|
  | 1 | count 1, colour at 25 (what 4.4.1 sends) | did not light | `#000000` |
  | 2 | count 1, colour at offset 1 | **turned red** | `#FF0000` |
  | 3 | count 0, colour at 25 | stayed red | `#FF0000` |
  | 4 | count 1, colour at 1 and 25 | stayed red | `#FF0000` |
  | 5 | count 1, colour at 25 plus a 0xFF fourth byte | turned off | `#000000` |

- Reading it (inference, not yet confirmed by a fix): steps 1 and 5 carry black at offset 1 and the keyboard is dark; steps 2 and 4
  carry red at offset 1 and it is red. Step 3 has black at offset 1 but stayed red, which fits the firmware ignoring a count of 0
  and keeping the previous state. So on this board the single colour is read from offset 1, and the firmware reports it back at
  offset 25. The reporter also could not say whether steps 3 and 4 re-lit it or only kept step 2's red; the readbacks are the
  better evidence than the eyes here.
- Same keyboard topology as 8BC2 (#227). Both should get the one-zone payload (WP-14, WP-07). The four-zone payload is unchanged.
- Not known: whether brightness and effects on this board use other commands, and whether numpad keys are part of the single zone.
  Ask the reporter, after the build, to set a colour on the Lighting page, change brightness, and say what moves.
- Reply to draft (not posted): thank them; say step 2 is the layout; say a build follows; say it is not fixed until they confirm.

**K.12 New board 88D1, OMEN 15-en1xxx (#228, korokhidup, 4.4.5, 2026-10-12)**
- Evidence (export read): BIOS F.30, Ryzen 5 5600H (CezanneBarcelo, PawnIO active), RTX 3060 Laptop, SKU 3D3E7PA#UUF. There is no
  `88D1` entry. Both the capability and keyboard lookups fall back to the model-name pattern `15-en` and land on the 8787 entry
  (`OMEN 15-en0038ur`, #120), confidence Low. Firmware: thermal policy V1, software fan control supported, two fans, max level 55,
  fan level readback works (42 and 40 at start-up), RPM is **Estimated**. Keyboard topology `FourZoneWithoutNumpad (0x02)`; the
  WMI ColorTable path initialised and wrote without error, but no one has seen the keys change.
- Adapter read: shipping 200 W, connected rating reads 0 W, firmware verdict "meets requirement". Treat the 0 W as unknown, not
  as a weak adapter (the low-wattage logic already declines to act on it).
- The log shows sensor-freeze warnings on a laptop that was in use: CPU frozen at 47 C for 22 readings with load swing 52%, GPU
  frozen at 45 C for 74 readings (recovered after 126 s) and again for 180 s. The GPU one is plausibly the dGPU sleeping, which
  would be a false positive. Not investigated. Same family as #221 (3E) and the sensor-identity work in WP-19.
- What to do: add an `88D1` entry (WP-07): V1, `FanZoneCount = 2`, max level 55, `SupportsRpmReadback = false` until a physical
  source appears, curves on, `RequiredCpuVendor = AMD`, keyboard `FourZoneTkl`-style entry matching what the 8787 entry uses,
  `UserVerified = false`, Notes cite #228. Do not enable undervolt or MUX from this export. Then ask for Guided Fan Verification
  with the RGB check on that build.
- Cheap hardening this exposes: the "Matched by model name" path logs at INFO. Make the notice in the app say the profile was
  borrowed from another board (it already says "Inferred"); no behaviour change.
- The RGB control path snapshot printed "Keyboard lighting service unavailable" although the log shows the V2 engine active. The
  snapshot runs before the Lighting page starts the service (the file says providers start lazily). Reword it to say "not started
  yet" (WP-04 or WP-05, text only).

**K.13 Board 88ED, Victus 16-e0xxx, "model request" although an entry exists (#229, PRIMUS626, 4.4.5, 2026-10-13)**
- Evidence (export read; the issue text is a screenshot and a zip only): BIOS F.27, Ryzen 7 5800H (CezanneBarcelo), RTX 3050 Ti. `88ED`
  already resolves by exact ProductId (Medium confidence, from #209), so what the reporter sees is the "not yet verified" notice,
  not a missing board. Firmware: thermal policy **V0**, software fan control **false**, 2 fans, max level 55; fan RPM readback and
  CPU temperature readback both return null, and all four `0x23` temperature indices read 0. RPM is Estimated. Keyboard topology
  `Normal (0x00)`: backlight only, colour control not offered, which is correct for the entry (it borrows 88EC's keyboard row through
  a model-name match, Low confidence).
- CPU temperature came from the LibreHardwareMonitor fallback because the WMI/ACPI value was rejected (49.1 C against 51.0 C). The log
  has about 15 "CPU temperature appears frozen" warnings over a day, held for 22 to 107 readings at 43 to 61 C with 1 to 24% load.
  Same shape as #221 and #228. Third board with this pattern; treat it as one problem (sensor identity, WP-19), not per-board.
- What to do: reply that the entry exists and what the notice means; add an exact `KeyboardModelDatabase` row for `88ED`
  (backlight only, `UserVerified = false`) so it stops matching by name (WP-07); ask for Guided Fan Verification, noting that V0
  firmware without software fan control may only offer the firmware modes, so the expected result is limited. Check what the Fan
  page shows on a V0, no-software-control board (WP-05), because the reporter's first impression is that the app "does not support" it.
- Extra: a "Quiet" preset was applied at 22:22 and "Fan max mode: disabled" at 09:52 the next day. No fan fault is reported.

**K.14 8E9A keyboard works through the Darfon 30BF route; 8E9F does not (#142, Keith1341, 2026-10-13)**
- Evidence (reporter's words, with HID ids): "I can confirm that I am able to control the lights through Omen Core." Hardware ids
  include `VID_0D62&PID_30BF` with `MI_01` and `MI_02&Col05`, plus a Microsoft `VID_045E` keyboard collection.
- Compare 8E9F (#224): same `0D62:30BF`, but its list shows `MI_00`, `MI_02&Col03` and `MI_03`, and the keyboard did not respond
  while the light bar did. Same VID/PID, different board, different result, so the route works on at least one 30BF device and the
  8E9F failure is not "30BF speaks another dialect". Hypotheses (unconfirmed): the 8E9F keyboard sits on a different interface
  (`MI_00` or `MI_03` against `MI_01`), or the route needs a different init for that board. Check which interface
  `DojoKeyboardMcu` opens and write the 30BF probe (WP-14) to try each interface, one change per step, logged.
- Replied 2026-10-13 (thanks; asked which effects work, brightness, per-key). Keith1341 answered the same evening with a new export
  (`omencore-diagnostics-2026-10-13-182151.zip`), read 2026-10-14:
  - Works: solid colour (`[DojoPerKey] Uniform fill via mi_03: accepted`), brightness 100 by re-scaling the displayed picture, and the
    on-board device effects Confetti and Raindrop (`installed and confirmed by readback`). MCU is on `mi_03`, LampArray on `mi_04`
    (120 lamps). So 30BF colour and device effects are now reporter-observed on 8E9A.
  - Scene Quick Select Wave/Rainbow do nothing on the keyboard: `RgbSceneService` sends `effect:wave` / spectrum to the third-party
    providers only (the log shows Logitech G713 failing and "no available providers support this effect"), and Heat Wave arrives as a
    uniform fill. The HP keyboard's own effects (`DojoKeyboardMcu.Effect.Wave`/`Rainbow`, offered on the Device Lighting page) are not
    reachable from scenes. Work: route scene effects to the V2 keyboard engine's effect path when it is active (WP-14 neighbour).
  - Per-key colours: no per-key write appears in the log, so this was not exercised; the layout is still `UNVERIFIED` (12 of 120 lamps
    have no HID usage). Ask what the per-key editor showed and whether Apply was pressed.
  - "Lights Off" scene: MCU refused brightness 0x0C with no host-painted picture, then fell back to WMI BIOS ColorTable (which
    succeeded). A running device effect cannot be dimmed on this hardware, by the log's own words.
  - Quick Profile back to Balanced at startup: by design today. The log says "Restored last performance mode: Performance" then
    "Startup performance restore is disabled - skipping automatic reapply" (`ShouldRunStartupHardwareRestore(Performance)`, off by
    default as a safety choice). The mode is remembered but not re-sent to firmware, and the UI then shows Balanced. Candidate fix: make
    the label say "saved: Performance, not applied" (the #199 `MarkSavedModeNotAppliedAtStartup` work) and tell the user where the
    switch is. Check the settings label is findable.
  - Third fan: Keith1341 says HP added it for the PCH. Still no control path or RPM for it; record as reporter statement.
- Replied 2026-10-14 (effects, scenes gap, startup label, per-key question, verification request).
- Keith1341 third post (2026-10-14 01:18 UTC): Guided Fan Verification 6/6 passed, 81/100, RPM source Estimated (all `[evidence: Level]`),
  RGB pattern shown, physical confirmation by the reporter not stated. Found the startup-restore switch. New:
  - Per-key paint does nothing: Paint selected, pick colour, click a key, Apply, nothing visible; box-drag selection also fails. The
    export read on 2026-10-14 pre-dates the attempt (no `[KeyboardMap] Apply:` line, which `KeyboardMapViewModel.ApplyAsync` always logs),
    so the cause is unknown. Ask for the log after one Apply and a screenshot of the editor. Suspects, unconfirmed: layout is
    `UNVERIFIED` (12 of 120 lamps without a HID usage), selection not reaching `PaintSelection`, or LampArray reports accepted with no
    visible change. Box-drag is a separate editor bug to reproduce.
  - Tray GPU Power submenu has only Minimum, Medium and Maximum (`TrayIconService.cs` lines 425 to 429, labels at 808 to 812), while
    the Custom profile offers Extended (`SystemControlViewModel.GpuPowerBoostLevels`). The tray's `_currentGpuPowerLevel` defaults to
    "Medium", and `SetGpuPowerLevel("Extended")` matches no item, so a reporter on Extended sees Medium. Code-confirmed gap; fix
    in WP-14 neighbour: add an Extended item shown only when the board supports it, and tick the right item. Test the label/tick logic.


**K.15 New board 8F21, HyperX OMEN 16-ap1xxx (#230, Projectorigins, 4.4.5, 2026-10-14)**
- Evidence (export read; the issue text is a one-line request plus a zip): BIOS F.04, Ryzen 7 8745HX, RTX 5060 Laptop, Radeon 610M, SKU
  D8RB8UA#ABA. No database entry, so it fell to the Unknown OMEN16 family default at Low confidence. Firmware: thermal policy V1,
  2 fans, max level 55, fan level readback works (27 and 30 at start-up), RPM readback null (estimated, source Est). Keyboard topology
  `FourZoneWithNumpad (0x01)`; the V2 engine's WMI ColorTable write was read back as verified. PawnIO's MSR module is Intel only, so CPU
  power reads 0 W and throttling telemetry falls back to LibreHardwareMonitor (known, logged).
- Done (`34951e2`): capability entry and keyboard row, both `UserVerified = false`, curves, GPU boost, MUX and undervolt left off.
- Reply posted 2026-10-17: thank them, say the entry exists in the 4.5.0 work, ask for Guided Fan Verification and what
  changes on the keyboard when a colour is set, and be plain that nothing about visible colour is confirmed yet.

**K.16 Transcend 14 (8E41) export with a guided run (#157, jmsguevara, 4.4.5, 2026-10-16)**
- Evidence: BIOS F.05, SKU BV8C6EA#ABD, thermal policy V1, **2 fans from the firmware** (the database said 1, and the runtime already uses
  the firmware's answer), fan level readback works, RPM readback null. Keyboard topology `FourZoneWithoutNumpad (0x02)` and colour writes
  verified through WMI ColorTable, while the database marks the board per-key (`HasPerKeyRgb`, keyboard type PerKeyRgb). Guided run: 6/6,
  88/100, every step on level evidence only (RPM is the level times 100).
- The reporter wrote that the app only offers "Run Verification Sequence", not "Guided Fan Verification". The button text was the
  mismatch; it now reads "Run Guided Fan Verification" (`34951e2`).
- Done: `FanZoneCount = 2` and `SupportsRpmReadback = false` for 8E41, with Notes. The per-key flag is deliberately left alone: one
  export is not enough to overrule an earlier Discord report, and the live probe already decides the backend. Ask: does a colour set
  on the Lighting page change the whole keyboard, zones, or individual keys?
- Sensor freeze: the log shows CPU and GPU "frozen" at the **same** value (56 C and 56 C, 60 C and 60 C, 59 C and 59 C) across many
  windows, with load swings of 15 to 60 percent. Two sensors holding an identical value is new; it points at one source feeding both
  readings. Fourth board with the freeze pattern; goes to WP-19 (sensor identity), not a per-board fix.
- Reply posted 2026-10-17.

**K.17 Fans pinned near level 20 after a hand-back: the cause, from the #227 log (2026-10-14 comment, 8BC2)**
- Report: with the Extreme profile selected the fans stay near 2000 RPM at 80 C and above; restoring the OEM profile fixes it. Same
  symptom as #217 (88EE), #226 (8BD4), #143 (8DCD) and the 2026-10-15 Discord report (2300 RPM at all times, "OGH stops the fans").
- Log (e227, `OmenCore_20261012_015243.log`): the hand-back runs `SetFanMax(false)`, `SetFanMode(Default)`, then "Step 3:
  SetFanLevel(20, 20) succeeded (V1 krpm hint)". On this board `AllowV1AutoModeFloorClear` is false, so the paired `SetFanLevel(0, 0)`
  is skipped (`ClearV1AutoModeFloor`: "conservative WMI profile avoids manual-zero fan stops"). The (20, 20) manual level is never
  undone. The same log shows the CPU at 96 to 98 C with the fan at 1600 to 1700 RPM ("unusually low for this temperature"), and only
  the 96 C emergency path raising the fans.
- Code: the hint is sent at `WmiFanController.cs` lines ~536 (Performance to Default kick), ~1166 (`ResetFromMaxMode`) and ~1224
  (the handoff helper). Each was written as the first half of a pair whose second half (0, 0) is now disabled on conservative boards.
- Status: **hypothesis from code and one log, not confirmed on hardware.** Implemented as an opt-in (`SkipV1FanLevelHint`, Settings,
  off by default, restart) so the owner can A/B it. Not made the default because the hint was added for a real case (#102, fans staying
  at 100% after Performance to Balanced) and skipping it for every conservative board could bring that back.
- Next: the #227 owner's A/B result decides the default. If skipping fixes it and nothing regresses, make it the default for boards with
  `AllowV1AutoModeFloorClear` unset. Then answer #217, #226, #143 and the Discord reporter with the same finding.
- Also from the same comment: the owner asks why fan curves, MUX and undervolt are not offered; the answer is the evidence gate (draft
  `r227b.md`, not posted).

### L. Research log
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
- 3K.7 and 3K.8 add no unproven writes: the NVIDIA items are range checks, text and read-only status, and the hibernate item is
  diagnostics until an export shows a cause.
- Every item in 3K ends as a fix with a test, a corrective reply on the issue, or an honest "still unconfirmed"; K.4 is not
  fixed until a real export shows the failsafe.

## 5. Agent brief: how to work through this roadmap

Read first: `CLAUDE.md` (rules, layout, hard-won facts), this file, `docs/CHANGELOG_v4.5.0.md` (running notes). Then take work
packages (section 6) in phase order. Do not start a package marked *needs data* without the data.

**Non-negotiable rules (from `CLAUDE.md`, repeated because they decide most reviews)**
- Evidence gate: label every claim *confirmed* (a reporter saw it on real hardware) or *implemented, pending confirmation*.
  Unproven hardware features stay off by default. New board entries use `UserVerified = false` and cite the issue in `Notes`.
- A fix comes with a test, ideally one that fails on the old code. Hardware is behind interfaces (`IHpWmiBios`, `IEcAccess`,
  `IMsrAccess`); never touch real hardware from tests.
- Any code that turns the firmware Max flag on must release it through `HpWmiBios.ReleaseMaxAndHandBackToBios()`;
  `src/OmenCoreApp.Tests/FirmwareMaxPairingGateTests.cs` fails the build otherwise (it lists the files allowed to call `SetFanMax(true)`).
- Typed catches only: `ReleaseGateCodeHygieneTests` keeps a line-number baseline for bare `catch`.
- Commits go to `main` in small focused commits that cite the issue. **Authored as Matthew Hurley (theantipopau), with no
  `Co-Authored-By` trailer and no AI mention anywhere** (commits, PRs, GitHub comments). Push only after the suite is green.
- Never tag, release, close an issue, or post on GitHub or Discord without the maintainer's go-ahead. Draft replies into a scratch
  file; do not post them. Never add or enable usage-billed Cloudflare products (Durable Objects, Browser Rendering).
- Licences: Ohman, OmenCtl, victus-control and omen-fan-control are GPL-3. Use their *documented behaviour* only, never their
  code, key maps or tables. The NVIDIA backend is used with its authors' permission (see `THIRD-PARTY-NOTICES.md`).

**Commands** (Windows, Git Bash or PowerShell; repo root `E:\OmenCore\omencore`)
```
dotnet build OmenCore.sln                                   # must stay at 0 warnings, 0 errors
dotnet test src/OmenCoreApp.Tests/OmenCoreApp.Tests.csproj --filter "FullyQualifiedName~<Name>"
dotnet test src/OmenCore.Linux.Tests/OmenCore.Linux.Tests.csproj
dotnet test OmenCore.sln                                    # full suite, about 7 minutes, run in the background before every push
```
Last full run before this cycle: 1861 App tests and 66 Linux tests passing. Keep the count from falling.

**Package template.** Each package below has: *Goal*, *Evidence* (issue or file), *Where* (files, verified to exist on 2026-10-12),
*Do*, *Tests*, *Done when*, and *Gate* (what stops it being merged early). Status values: `todo`, `needs data`, `blocked`, `done`. Progress is ticked in section 5A.

## 5A. Tick sheet

The working agent edits this list as it goes. Tick `[x]` only when the item is finished **and verified** (the named test passes; build
has 0 warnings). After the box write the short commit hash, for example `[x] (a1b2c3d)`. Something blocked or skipped stays `[ ]`
with a one-line reason after it. Do not tick a *needs data* item without the data. Update the package status in section 6 in the same
commit. Before every push: tick the row, run the full suite, then update the "Suite" line at the bottom.

**Setup (first commit)**
- [x] (b628391) Versions set to 4.5.0: `VERSION.txt`, `installer/OmenCoreInstaller.iss` `MyAppVersion`, `<Version>`/AssemblyVersion/FileVersion in the Avalonia, Cli and Core csproj files (Desktop csproj stays on 3.6.3)
- [x] (this commit) `docs/CHANGELOG_v4.5.0.md` header and "Type:" line current
- [x] (b628391) Baseline suite run recorded below

**Phase 1: no hardware needed**
- [x] (4234cc9) WP-01 Hotkey retry: Win32 call injectable
- [x] (1b581fa) WP-01 retry timer plus on window activation, bounded (about 5 s, 12 tries) — done: timer (4234cc9) and retry on activation, at most once a minute (1b581fa); Settings lists the blocked keys (1b581fa)
- [x] (1b581fa) WP-01 one `Warn` per failed key at the end, status object exposes it, Settings shows it — done: timer (4234cc9) and retry on activation, at most once a minute (1b581fa); Settings lists the blocked keys (1b581fa)
- [x] (4234cc9) WP-01 tests: all-fail, fail-then-succeed, stops after Dispose
- [ ] WP-02 `Ohman` entry in `ConflictDetectionService` (process name confirmed on a running copy) — entry added (0386d18) with the process name from the download file name; not checked on a running copy
- [x] (0386d18) WP-02 new `ConflictDetectionServiceTests` (present and absent) — table entry only; the scan itself is not unit-testable without a refactor
- [x] (378d853) WP-03 Linux fan-writer scan (`/proc/*/comm`, injected list) and `diagnose` "Other fan writers"
- [x] (378d853) WP-03 daemon start warning, nothing is stopped or killed
- [x] (378d853) WP-03 Linux test for the scanner
- [x] (c27bd2f) WP-04 `[Fan Firmware State]` section — Max flag, level readback, thermal policy, fan count; fan-mode readback and watchdog-failsafe flag not included (no getter exists yet)
- [x] (242e829) WP-04 `[Power Transitions]` section (event IDs 41, 42, 107, 506, 507, no `powercfg`)
- [x] (242e829) WP-04 `[CPU Throttle]` placeholder line
- [x] (242e829) WP-04 tests: fake source and throwing source for each section
- [ ] WP-05 live RPM source and PL1/PL2 result passed into the capability card
- [ ] WP-05 card wording: "Estimated from fan level" / "not readable on this board" — the card already reads "Estimated only" when the flag is false; 8BBE and 8BD4 now get it (dfcf2ce); a live-probe override (probe wins over database) not done
- [x] (dfcf2ce) WP-05 list of entries relying on the `SupportsRpmReadback` default in the commit message — 56 of 82 entries; 8BBE and 8BD4 set false; 8BC2 has no entry yet (WP-07)
- [x] (63af90b) WP-05 `SupportsRpmReadback = false` set for 8BBE, 8BD4, 8BC2 with tests
- [ ] WP-06.1 calibration says RPM cannot be measured when the source is Estimated; table labelled estimated — not done: the wizard reads `ReadFanSpeeds()` (physical sources only, returns nothing on V1), while the "passes at 2000" result in K.1 comes from the verification path; needs a trace of which screen showed it before changing anything
- [ ] WP-06.2 repeated stable readback below request counts as a firmware ceiling at any step — deliberately not done: on 8BD4 the CPU fan reads level 20 at every step, so accepting a stable plateau at any step would pass a fan that ignores commands. Needs a rule that tells a clamp from a dead command (data from a board that clamps at a distinct level)
- [x] (912b818) WP-06.3 `IsFirmwareCeilingPlateau` 90% rule (88F8 case); the 60%-step rule and its test kept — implemented as `ReconcileCeilingAcrossFans`, run after each 100% step, because the sibling fan is only known then
- [x] (ace9941) WP-06.4 thermal interruption marked, not failed; cool-down wait below 85 C before a run — decisions in `GuidedRunRules` (tested); the run itself needs a hot machine to see, so the wiring is untested on hardware
- [x] (d1c1fc4) WP-06.5 refused per-fan write says "firmware refused level L" (878A)
- [ ] WP-06 scenario tests for #226 and #207 give "level-only" and "pass (ceiling)"
- [x] (63af90b) WP-07 8BC2 capability entry (V1, 2 fans, max 55, no RPM readback, curves/GPU boost/undervolt off, Intel) and test
- [x] (63af90b) WP-07 8BC2 `KeyboardModelDatabase` entry (`OneZoneWithNumpad`) and test
- [x] (cd8bf89) WP-07 88D1 entry (#228) and test
- [x] (cd8bf89) WP-07 88ED exact `KeyboardModelDatabase` row (#229) and test
- [x] (34951e2) WP-07 8F21 entry (#230) and keyboard row, with tests
- [x] (34951e2) WP-07 8E41: two fans and level-only RPM from the #157 export (per-key flag left, see K.16)
- [x] (34951e2) Fan diagnostics button reads "Run Guided Fan Verification" (#157)
- [x] (cd8bf89) WP-07 8A13 Notes and verified flags for the confirmed parts only (MUX stays off) — Notes only; `UserVerified` left false because the rest of the entry is unconfirmed
- [x] (63af90b) WP-07 8BD4 Notes: second owner (#226)
- [x] (cd8bf89) WP-07 8E9A Notes: keyboard colour confirmed through 30BF (#142)
- [x] (08465e2) WP-07 callers of `SetColorTable` that omit `zoneCount` — done as part of WP-14.1: the default now follows the board topology (`KeyboardLightingService.cs:546`, `WmiBiosBackend.cs:213, 372`) pass the topology count
- [x] (2613f4d) WP-08 write clamp for MAX/CURRENT with refusal text — already enforced in the backend (`SetMaxOverride` and `SetCurrent` refuse values outside the profile range, with text) and the dropdown only offers `Targets`; no change needed
- [x] (2613f4d) WP-08 "stock max read X W, expected Y W" explanation
- [x] (2613f4d) WP-08 panel text (do not disable PPAB, driver update may undo, RTX 5050 untested, re-toggle hint)
- [x] (2613f4d) WP-08 TPP read-only, or left out with a note — left out: the backend has no TPP read, and adding one would be a new probe; revisit with the PCF/TPP work
- [x] (2613f4d) WP-08 tests: range clamp, mismatch text — mismatch text tested (`NvidiaPowerStockNoteTests`); the range clamp needs a GPU and driver, so it stays untested here
- [x] (973b1ec) WP-09 `docs/LINUX_KERNEL_BOARD_SUPPORT.md` written, linked from the install guide, `46be1453` verified on kernel.org — verified through the GitHub mirror (kernel.org returned 403): Krishna Chomal, 2026-01-13, hp-wmi manual fan control for Victus S
- [x] (973b1ec) WP-09 #219 reply drafted into a scratch file (`r219b.md`, not posted; maintainer posts it)

**Phase 2: behaviour changes, off by default**
- [x] (0fa59d5) WP-10 Max auto-return setting (off / cool-down / timer) through `ReleaseMaxAndHandBackToBios()` — releases through the normal Max-to-Auto `ApplyPreset` path (which hands the flag back in the controller), not a direct call; "chassis" temperature has no sensor, so the higher of CPU and GPU stands in
- [x] (0fa59d5) WP-10 tests with fake clock; `FirmwareMaxPairingGateTests` green
- [ ] WP-11 confirm from a real #222 export first (needs data)
- [ ] WP-11 failsafe release fix and tests (70 C releases, 85 C does not)
- [ ] WP-12 fan-stall guard (needs data; physical RPM source only)
- [x] (62d39fb) WP-13 IA32_THERM_STATUS bits confirmed against the Intel SDM, decoder and test, result in `[CPU Throttle]` — bits 0, 6, 8, 10 cross-checked against Linux msr-index.h, not the SDM itself (SDM section number unchecked, noted in the code comment); the read goes through the Tuning page's existing MSR handle, so it shows "not available" until that page has opened it

**Phase 3: needs reporter data**
- [x] (08465e2) WP-14.1 `SetColorTable` one-zone layout (`t[0]=1`, RGB at offsets 1 to 3, verify reads offset 25)
- [x] (08465e2) WP-14.2 tests: payload bytes for zone count 1 and 4; 8BD4 and 8BC2 topology mapping
- [x] (5acc8ab) WP-14.3 changelog says "implemented, pending 8BD4 retest"; build offered to the 8BD4 owner (draft only) — changelog wording is in; reply drafted in `r212c.md`, not posted, no test build exists yet
- [ ] WP-14.4 30BF probe comparing interfaces (needs the maintainer's go-ahead on design)
- [x] (be14d99) WP-14.5 scene Wave/Rainbow reach the V2 keyboard engine (3K.14) — Wave, Spectrum (as the device colour cycle) and Breathing scenes go to the keyboard's own effect engine; static scenes unchanged; the engine's Rainbow palette is not used, so a Spectrum scene shows the scene's colour cycling rather than a fixed rainbow
- [x] (2b86584) WP-14.7 tray GPU Power submenu: Extended item — item always shown, like the Tuning page list; not per-board gated (when supported) and correct tick (#142)
- [ ] WP-14.8 per-key paint/Apply and box-select reproduced from a log after one Apply (#142, needs data)
- [x] (e67c343) WP-14.6 startup-restore label says "saved, not applied" and the setting is findable (#142) — the tray performance header now reads "Firmware default" when the window says Default (checkmarks still map Default to Balanced); the Settings startup-restore options already exist
- [x] (db96eec) WP-21.1 `OmenKeyAction.ToggleOsd`: enum, `LoadSettings` mapping, `ExecuteAction` case, `ToggleOsdRequested` event
- [x] (db96eec) WP-21.2 `MainViewModel` handler calls `_osdService?.Toggle()`; "ToggleOsd" added to `OmenKeyActionOptions`
- [x] (db96eec) WP-21.3 tests: string maps to the action, the action raises only `ToggleOsdRequested`; unknown string still falls back to quick popup
- [x] (db96eec) WP-21.4 OSD disabled in settings: key press does nothing visible, one log line says why
- [ ] WP-15 878A fan control (needs data)
- [ ] WP-16 V1 idle fan floor (needs data)
- [ ] WP-22.1 ask the reporter (via bobshmo) for the `NVIDIA power:` log lines and the panel status text; record board and GPU
- [ ] WP-22.2 confirm with bobshmo in writing that 1.2.x code may be used before copying anything
- [x] (bab4f61) WP-22.3 `FindNvflash` also finds `nvflashb.exe`; clear message when none is found; tests
- [ ] WP-22.4 CURRENT fault from WP-22.1 reproduced from the log and fixed, or recorded as unconfirmed
- [ ] WP-16.1 ask the reporters (#217, #226, #143, new 2026-10-15) for the stop-with-OmenCore-closed A/B; record the result per board — the #227 log already shows the hint left in place (K.17); the closed-app A/B is still wanted from #217, #226, #143
- [ ] WP-16.2 if OmenCore holds the floor: skip the level 20 hint and keepalive on those boards, opt-in "allow fans to stop at idle", tests — PARTLY: the level-20 hint skip is in as an opt-in (`bad5d47`, K.17, tested); the keepalive part and the "allow fans to stop at idle" option are not done and need the A/B result first
- [ ] WP-17 8E35 and AMD SMU (needs data)
- [ ] WP-18 Transcend 14 entries (needs data)

**Phase 4: foundations (separate commits from behaviour changes)**
- [ ] WP-19 fan write lease coordinator: design doc, then implementation
- [x] (this commit) WP-19 design note `docs/DESIGN_FAN_WRITE_LEASE.md` (lease coordinator and trace-replay plan); implementation waits for the maintainer's go-ahead on step 1 and on keeping scrubbed traces as test data
- [ ] WP-19 trace and replay harness with the 8BD4, 8BC2, 88F8 exports
- [ ] WP-19 sensor identity record (frozen CPU reading on 8E9A, 8BC2, 8DD0, 88D1, 88ED) — started, not done: a one-time warning when the BIOS CPU and GPU readings are identical for 30 reads (`6604a54`, #157, observation only); the stable per-board record of which sensor feeds CPU temperature is still to do
- [ ] WP-19 asymmetric temperature filter behind an off flag
- [ ] WP-19 shared capability resolver

**Housekeeping and release (maintainer owns the last block)**
- [x] (this commit) WP-20 Linux package suspend hook — copied into the zip by `build-linux-package.ps1` (snippet dry-run on Windows; the Linux build itself runs in CI on the next tag, unverified until then)
- [x] (this commit) WP-20 `CLAUDE.md` updated (version and open threads)
- [ ] Section 4 "What counts as done" reviewed line by line
- [ ] Replies drafted for every issue a shipped item touches; "Needs field confirmation" list rewritten
- [ ] Full suite green, 0 warnings, count not lower than the baseline
- [ ] Maintainer: PR #147 and code signing decisions
- [ ] Maintainer: tag `v4.5.0`, check the release notes step, record SHA256, update README and website
- [ ] Maintainer: close issues earmarked "close on ship"

**Suite line:** baseline (4.4.5, 2026-10-12): 1864 passing, Windows. Cycle baseline (2026-10-17, versions at 4.5.0): 1864 App + 66 Linux passing. Last full run (2026-10-17, through the opt-in fan-level-hint switch and the 8F21 entry): 1943 App + 71 Linux passing, 0 warnings. One earlier run of the same code aborted at 1750 tests with a native `AccessViolationException` in the test host (no test name in the log); it did not repeat after the CPU-throttle reader stopped being a static, and a re-run of the related test groups passed. The cause is unproven: if it returns, suspect a test that holds a native handle (MSR, NVML) past its owner.

## 6. Work packages

### Phase 1: no hardware needed, evidence already in hand

**WP-01 Hotkey retry that ends in a message** (`done`)
- Goal: a hotkey that fails to register at startup is retried for a bounded time and then reported, instead of silently dropped.
- Evidence: 4.4.5 test machine, three hotkeys never registered and no result was logged. Code: `RetryPendingHotkeys()` runs only
  from `Initialize` (`src/OmenCoreApp/Services/HotkeyService.cs` lines 104 to 119 and 176 onward); a failed key is queued at line 302.
- Do: retry on a timer (for example every 5 s, at most 12 times) and on window activation; after the last attempt log one `Warn`
  naming the key and action ("Ctrl+Shift+O is held by another app") and expose it in `HotkeyService`'s status object (the one
  with `PendingCount` and `PendingBindings`, near line 87) so the Settings page can show it. Make the Win32 call injectable
  (a `Func<IntPtr,int,uint,uint,bool>` field defaulting to the P/Invoke) so a test can force failures.
- Tests: `src/OmenCoreApp.Tests/Services/HotkeyServiceTests.cs`: all-fail then give-up with one warning; fail-twice then succeed
  clears the pending list; retry stops after `Dispose`.
- Done when: no queued key is ever left unreported; the log shows the final outcome per key.

**WP-02 Ohman in conflict detection** (`todo`)
- Goal: warn when Ohman is running, like OMEN Gaming Hub, because both write fan state.
- Where: `src/OmenCore.Core/Services/ConflictDetectionService.cs` (entries are keyed dictionary items with `ProcessNames`; copy the
  `OmenHub` entry at line 49). No test for this service exists yet: add `src/OmenCoreApp.Tests/Services/ConflictDetectionServiceTests.cs`.
- Do: add an `Ohman` entry. The download is `Ohman.exe`; confirm the process name on a running copy before merging, and say in the
  description that two controllers writing fan state will fight.
- Tests: a fake process list containing `Ohman` produces the conflict; one without it does not.

**WP-03 Linux: competing fan writers** (`todo`)
- Goal: warn when another tool that writes `pwm1_enable` is active.
- Where: `src/OmenCore.Linux/Commands/DiagnoseCommand.cs` (the `FanWriter` field, about line 109, and the report lines near 805 and
  912); `src/OmenCore.Linux/Daemon/OmenCoreDaemon.cs` (lock acquire near line 89); `src/OmenCore.Linux/Hardware/WriterLock.cs`.
- Do: scan `/proc/*/comm` (no external process) for `victus-fan`, `victus-backend`, `omen-fan-control`, `omenctld`; list hits in
  `diagnose` under "Other fan writers" and log one warning at daemon start. Do not stop or kill anything.
- Tests: `src/OmenCore.Linux.Tests/Hardware/`: a scanner that takes an injected list of process names.

**WP-04 Diagnostics export additions** (`done`, apart from fan-mode readback and the watchdog flag)
- Goal: the next stuck-fan, hibernate or throttling report carries its own cause.
- Evidence: #211 (no Max flag or mode readback in the export), K.8 (no sleep or hibernate reasons), #222 (no stuck-state data).
- Where: `src/OmenCoreApp/Services/Diagnostics/DiagnosticExportService.cs` (follow the existing `AppendMemoryBreakdown` pattern and
  its test `MemoryBreakdownTests`; snapshot tests are in `src/OmenCoreApp.Tests/Services/DiagnosticExportSnapshotTests.cs`).
- Do, three new sections, each read-only and wrapped so a failure writes one line, not an exception:
  1. `[Fan Firmware State]`: `GetFanMax()`, fan mode readback, CPU and GPU level readback, thermal policy, RPM source, and whether
     the watchdog failsafe is active (`HardwareWatchdogService` exposes the flag; add a getter if needed).
  2. `[Power Transitions]`: the last 20 Kernel-Power sleep, resume and hibernate events with their reasons (event IDs 41, 42, 107,
     506 and 507; read through the event log API, no `powercfg` process).
  3. `[CPU Throttle]`: reserved line for WP-13 ("not available" until that lands).
- Tests: each section renders with a fake source and with a throwing source.

**WP-05 Capability card tells the truth** (`todo`)
- Goal: where a live probe disagrees with the database, the card shows the probe.
- Evidence: K.3. `ModelCapabilities.SupportsRpmReadback` defaults to `true` (`src/OmenCore.Core/Hardware/ModelCapabilityDatabase.cs`
  line 70) so every entry that does not set it claims real RPM; the card is `src/OmenCoreApp/Views/DiagnosticsView.xaml` (line 344).
- Do: pass the live RPM source (`Estimated`, physical, none) and the PL1/PL2 readback result into the card's view model; show
  "Estimated from fan level" and "not readable on this board" instead of "Supported". Do not flip the database default blindly:
  list every entry that relies on it first (a count and the boards go in the commit message), and set `false` explicitly only for
  boards whose export shows null (8BBE, 8BD4, 8BC2 at least).
- Tests: `ModelCapabilityDatabaseTests`: those boards report `SupportsRpmReadback == false`; a view-model test for the probe-wins rule.

**WP-06 Calibration and Guided Fan Verification honesty** (`todo`)
- Goal: stop reporting estimates and interrupted steps as measurements or failures.
- Evidence: K.1 and K.9 (#226, #227, #207, #218), K.6 (thermal interruption on 8BC2).
- Where: `src/OmenCore.Core/Services/FanCalibration/FanCalibrationService.cs` (tests `FanCalibrationServiceSafetyTests.cs`);
  `src/OmenCore.Core/Services/FanVerificationService.cs` (`IsFirmwareCeilingPlateau` line 885, ceiling use near line 434; tests
  `FanVerificationCeilingTests.cs`, `FanVerificationServiceTests.cs`); `src/OmenCoreApp/ViewModels/FanDiagnosticsViewModel.cs`
  (the per-step loop at lines 335 to 365).
- Do, four separate commits:
  1. When the RPM source is `Estimated`, calibration says "RPM cannot be measured on this board" and does not store a duty-to-RPM
     map as if measured (store it flagged `LevelOnly`, or skip). Label the results table "estimated".
  2. A stable, repeated readback below the request is a firmware ceiling at any step, not only at 100%.
  3. `IsFirmwareCeilingPlateau` also passes a level within 90% of the best level another fan reached in the same run (88F8: 43 vs 46).
     The 60%-step rule (a board whose Max never rises past the 60% level still fails) stays and keeps its test.
  4. If thermal protection fires during a step, mark it "interrupted by thermal protection", do not count it as a failure, and
     offer a re-run after a cool-down (below 85 C). Also wait for that temperature before starting a run.
  Also: when a per-fan level write is refused (878A: `WMI fan control failed for fan N, level L`), say "the firmware refused level L"
  rather than "Physical RPM unavailable" (`FanVerificationService.cs` line 60 produces that text for `RpmSource.Unknown`).
- Tests: one per commit; reuse the fake `IHpWmiBios` used in `FanVerificationCeilingTests`.
- Done when: re-running the #226 and #207 scenarios in tests gives "level-only" and "pass (ceiling)".

**WP-07 Board entries and promotions** (`todo`; all `UserVerified = false` unless a reporter has confirmed)
- 8BC2 (#227): own entry, `ProductId = "8BC2"`, `ModelName = "HP Victus 16-r0xxx Intel"`, family Victus, V1, `FanZoneCount = 2`,
  max level 55, `SupportsRpmReadback = false`, curves, GPU boost, undervolt off, `RequiredCpuVendor = Intel`. Notes cite #227
  (BIOS F.31, i7-13700HX, RTX 4060). Keyboard: add a `KeyboardModelDatabase` entry for 8BC2 (topology `OneZoneWithNumpad`, WMI
  colour table, `UserVerified = false`) and make the V1 path send the topology's zone count, not a fixed 4
  (`HpWmiBios.MapLightingTypeToZoneCount`, line 1822). `HpWmiBios.SetColorTable` (line 2062) defaults `zoneCount` to 4, and three
  callers do not pass it: the V1 path at `KeyboardLightingService.cs` line 546, and the restore and scaled-colour paths in
  `WmiBiosBackend.cs` lines 213 and 372. Only line 133 of `WmiBiosBackend.cs` passes the topology count. That is why the 8BC2 log shows
  `SetColorTable: ZoneCount=4` on a one-zone keyboard.
  Tests: `ModelCapabilityDatabaseTests`, `KeyboardModelDatabaseTests`, `KeyboardLightingTopologyTests`.
- 8A13 (#225): fans, WMI GPU power and the RGB pattern are confirmed by the reporter (Guided Fan Verification 6/6, 82/100; zones
  visibly changed). Update the Notes and set the verified flags only for those; MUX stays off. Ask for a photo of the "white"
  that shows pink before any colour-balance work.
- 8BD4: a second owner (#226) confirms the board, BIOS F.32, Ryzen 7 7840HS; add to the Notes only.
- 8EDC (#201), 8E41 and 8C58 (Transcend 14: #157, #149, #99, #76, and K.8): entries only from an export; do not guess.
- Where for all: `src/OmenCore.Core/Hardware/ModelCapabilityDatabase.cs` (8BBE entry at line 2208 is the nearest model),
  `src/OmenCore.Core/Services/KeyboardLighting/KeyboardModelDatabase.cs`.

**WP-08 NVIDIA power panel hardening** (`done`, TPP left out)
- Goal: the panel cannot send out-of-range values, and it explains what it read. Evidence: K.7.
- Where: `src/OmenCoreApp/ViewModels/NvidiaPowerViewModel.cs`, `src/OmenCoreApp/Services/NvidiaPowerService.cs` (`Assess()` returns
  `Status` with `LiveCurrentW`, `LiveMaxW`, `Targets`), `src/OmenCore.NvidiaPower/**` (internal; `InternalsVisibleTo` already set).
- Do: (1) clamp and validate MAX and CURRENT to the stock-default to NVIDIA-stock-max range before any write, with the refusal text
  shown; (2) show "stock max read: X W, expected: Y W" and a short explanation when they differ (the 115 W against 140 W case);
  (3) text: do not disable PPAB, the adapter limits CPU+GPU together, a driver update may undo the unlock, RTX 5050 untested, "if CURRENT
  will not stick, re-toggle the HP state and unlock again"; (4) read TPP if the backend exposes it (read only).
- Tests: `NvidiaPowerServiceTests` for the range clamp; a view-model test for the mismatch text.
- Gate: no new write path. If TPP cannot be read without a new write, leave it out and say so.

**WP-09 Linux kernel-side guide and the #219 reply** (`done`, reply drafted not posted)
- Goal: one document that answers the repeating Linux question ("my board has no `platform_profile`").
- Evidence: #219, #99, #84, #76, #28, #127, #26, #60, #123; K.10; the third-party README claim about upstream commit `46be1453`.
- Where: new `docs/LINUX_KERNEL_BOARD_SUPPORT.md`, linked from `docs/LINUX_INSTALL_GUIDE.md`.
- Do: explain why OmenCore cannot route around the kernel allowlist; how to submit a board-ID patch (find the maintainers with
  `scripts/get_maintainer.pl` on the `hp-wmi` source in the kernel tree, post to the platform-driver-x86 list, include the board name
  from `/sys/class/dmi/id/board_name`, kernel version, and what works and fails); a safety checklist for out-of-tree drivers (does it
  write the EC directly, does it list your board, does it have a revert, is it GPL and maintained; EC-writing drivers have caused
  faults on the MAX family, see #60). Verify the `46be1453` commit on kernel.org before citing it.
- Done when: the doc exists and a reply to #219 is drafted into a scratch file for the maintainer to post.

### Phase 2: behaviour changes, each opt-in or off by default

**WP-10 Max auto-return** (`done`, pending hardware)
- Goal: optional return from Max to Auto after a cool-down or timer. Evidence: Ohman's rule (3A). Default off.
- Do: setting with three modes (off, cool-down: CPU and chassis under 60 C for 2 minutes, timer: 15, 30 or 60 minutes). Implement
  in `FanService` next to where the Max preset is applied; the release calls `ReleaseMaxAndHandBackToBios()` through the existing
  path and never `SetFanMax(false)` directly. Put the setting in `FeaturePreferences` and show it in Settings (`SettingsViewModel`).
- Tests: `FanServiceSuspendTests`-style fake clock; the cool-down resets if temperature rises; the timer survives a preset
  re-apply; `FirmwareMaxPairingGateTests` still green.
- Gate: pending hardware confirmation label in the changelog.

**WP-11 Watchdog failsafe release** (`needs data`, then `todo`)
- Goal: the 90% failsafe cannot hold fans high indefinitely on a warm machine. Evidence: K.4 (hypothesis from code).
- Where: `src/OmenCore.Core/Services/HardwareWatchdogService.cs` (`FailsafeSafeReleaseTempC = 65`, `FailsafeSafeReleaseSeconds = 15`,
  `FreezeThresholdSeconds = 90`; tests `HardwareWatchdogServiceTests.cs`).
- Do: first confirm from a real #222 export (`WATCHDOG` lines). Then release when readings are fresh and below the thermal-protection
  ramp (90 C) minus a margin for 15 s; keep 65 C as the quick path. Also find why monitoring went stale twice in 90 s.
- Tests: a warm-idle scenario at 70 C releases; a real 85 C scenario does not.

**WP-12 Fan-stall guard** (`needs data`)
- Goal: a fan reading 0 RPM while commanded above zero forces Max and warns. Only where the RPM source is physical
  (`RpmSource` in `FanVerificationService.cs`); never on `Estimated` or `Unknown`. Where: `FanService` monitor loop or
  `HardwareWatchdogService`. Gate: needs an export from a board that has stalled, or a rig; release through the shared Max helper.

**WP-13 CPU throttle reason, Intel first** (`done`, SDM section not checked)
- Goal: log why the CPU is throttled. Where: `src/OmenCore.Core/Hardware/IMsrAccess.cs`, `PawnIOMsrAccess.cs`;
  `EdpThrottlingMitigationService.cs` already reads MSRs for a related purpose, so reuse its access pattern.
- Do: read IA32_THERM_STATUS (`0x19C`) bits for thermal, PROCHOT and power-limit status; confirm the bit positions against the
  Intel SDM before coding and cite the section in a comment. Write the result into WP-04's `[CPU Throttle]` section. Read only.
  AMD stays "not available" until its registers are researched.
- Tests: decode a fake MSR value to the expected flags.

### Phase 3: needs reporter data (do not start without it)

**WP-14 Keyboard probes** (`ready for the 8BD4 part; the rest needs data`)
- 8BD4 (#212): the probe result is in, so this part is no longer waiting. In `HpWmiBios.SetColorTable` (line 2062), when the
  topology maps to one zone (`MapLightingTypeToZoneCount`, line 1822, returns 1), build `t[0] = 1` and put R, G, B at offsets 1 to 3,
  not at 25. Leave the four-zone layout untouched (8A13 and 8787 verify it). Keep the verify step reading offset 25, because the
  firmware reports a single-zone write there (K.11). Tests: payload bytes for zone count 1 and 4 in `HpWmiBiosTests` or the
  nearest existing colour-table test file; `KeyboardLightingTopologyTests` for the 8BD4 and 8BC2 mapping. Do not delete
  `ColorTableProbe`; retire it after the 8BD4 owner confirms on a build. Status wording: "implemented, pending 8BD4 retest".
- 8E9F (#224) and 8E9A (#142): the Darfon 30BF route did nothing on 8E9F (light bar works). Write a 30BF probe in the style of
  `ColorTableProbe` (one change per step, each logged with the device response), surfaced from `KeyboardDiagnosticsViewModel`.
  Mark the route "tried, no effect" on 8E9F in its notes and keep the light bar. The same route is reporter-confirmed on 8E9A
  (3K.14), so record `0D62:30BF` as working for colour on 8E9A only, and keep it unverified in `DojoKeyboardMcu` as a PID. The probe
  should compare the interfaces the two boards expose (`MI_01` on 8E9A; `MI_00` and `MI_03` on 8E9F).

**WP-15 878A fan control** (`needs data`): per-fan level writes are refused (K.9). Candidate: EC path through PawnIO for 878A and 878C.
Needs the board's EC register map from a source we may use and an owner test; keep it off by default.

**WP-16 V1 idle fan floor** (`needs data`): K.9. Investigate whether `SetFanLevel(20, 20)` after a hand-back keeps fans from
stopping. Needs an owner who can hear or measure the fans after Restore Auto and after quitting. If confirmed, skip the hint on boards
whose Auto stops the fans (evidence per board), with a test.
Add a user-facing option "Allow fans to stop at idle" (off by default) only if the A/B above shows OmenCore is what holds the floor. It
would hand fans back to the BIOS without the level 20 hint and without the keepalive re-asserting a level while CPU and chassis are cool
and load is low, and would re-engage the normal curve above a threshold. Never write level 0 ourselves as a "stop" command: boards that
refuse or misread it (K.9 878A) would be worse off. Implemented, pending hardware confirmation.

**WP-17 8E35 and AMD SMU** (`needs data`): a real diagnostics export from #222 (the three posted files are `config.json`); PM-table
dumps for Ryzen 9 8940HX, Ryzen 7 260 (#223) and the 8E9F CPU via `tools/SmuProbe --pmtable`; UI stutter profile (curve editor drag
path) from `runtime-performance.txt`.

**WP-18 Transcend 14** (`needs data`): Windows entries for 8C58 and 8E41 and the hibernate report (K.8) wait for an export.

**WP-21 OMEN key opens the OSD** (`ready`, small, user request 2026-10-14)
- Goal: the OMEN key can be set to show and hide the OSD overlay, the way it can already be set to open the quick popup.
- Code: `OmenKeyAction` enum and `ExecuteAction` switch (`src/OmenCore.Core/Services/OmenKeyService.cs` around lines 680 to 710, enum
  at 1138), string mapping in `LoadSettings` (around line 750; unknown strings fall back to quick popup), options list
  `SettingsViewModel.OmenKeyActionOptions` (line 1423), handlers in `MainViewModel` (line 4084 is the quick popup one; `_osdService`
  is field line 86). `OsdService.Toggle()` already exists and `Show()` only acts when `Osd.Enabled` is true.
- Work: add `ToggleOsd`, an event `ToggleOsdRequested`, a `MainViewModel` handler that calls `_osdService?.Toggle()`, and the settings
  entry. When the OSD is switched off in settings, log that once instead of silently doing nothing.
- Needs no hardware evidence beyond the OMEN key already being detected on the board; status wording: "implemented, pending a user test".
  The Settings option label should be readable ("Show/hide OSD"), checking how the other entries are labelled.
- Tests: mapping and event tests next to the existing OMEN key tests in `HotkeyAndMonitoringTests.cs`.

**WP-22 NVIDIA power unlock: catch up with the Prophecy standalone 1.2.x** (`needs data` for CURRENT, `ready` for the nvflash lookup; request 2026-10-17)
- Request (Discord, from bobshmo, 2026-10-17): "Apply current does not work and user is still prompted for nvflash, current build fixes this."
  No GitHub issue mentions the unlock yet (searched romOverride, Resolve VBIOS, CURRENT, Prophecy, nvflash). Which user and board is unknown.
- What our code does: the nvflash prompt comes only from the **Resolve VBIOS** step. `VbiosResolver.FindNvflash` (line 441) looks for
  `nvflash64.exe` only (tools folder, next to the exe, `C:\NVFlash`, PATH); the tooltip in `TuningView.xaml` line 920 says the same.
  bobshmo's standalone 1.2.0 notes say Blackwell cards use a bundled **nvflashb.exe**, detected automatically, and that the ROM-dump launch
  was fixed to run with a hidden console. Our dump launch already passes `createNoWindow = true`, so the second part may already hold.
- Backend `SetCurrent` (`PowerBackend.cs` line 441) does **not** need the VBIOS or nvflash: it needs the driver transport trusted
  (`CurrentWritesReady`), a consistent policy, GPU idle (under 30%), and live MAX readable. So "Apply CURRENT does not work" is a separate
  fault from the nvflash prompt. Cause unconfirmed: needs the OmenCore log lines starting `NVIDIA power:` and `NVIDIA power panel:` and the
  panel status text (it says why CURRENT is "not ready").
- Work, in order: (1) ask the reporter for the log and panel text; (2) add `nvflashb.exe` to `FindNvflash` and use the exact arguments
  bobshmo's notes document, if they differ; (3) show a clear message when no nvflash is found, saying CURRENT can still work without it;
  (4) re-read the 1.2.1 to 1.2.5 notes for fixes that touch MAX or CURRENT only. Tests next to `NvidiaPowerServiceTests`.
- Do **not** bundle nvflash. bobshmo's own 1.2.0 notes say the NVFlashB upstream README says MIT while its LICENSE file is GPLv3, so no
  blanket licence is claimed. We detect a copy the user supplies, as now. Not in 4.5.0: Backup ROM, Import ROM and Flash (physical flashing,
  unconfirmed), the PCF/TPP controls and calibration (see 3I; PCF stays read only), the GPU-cap tester.
- Permission: the 4.4.5 integration was "with permission". The permission for pulling in 1.2.x code should be confirmed with bobshmo
  before copying anything new; the standalone repo has no licence file, so ask in writing. Status wording: "implemented, pending hardware".

### Phase 4: engineering foundations (promised in `docs/V4.4.1_IMPLEMENTATION_STATUS.md`)

**WP-19 Foundations** (`todo`, large; do not mix with behaviour changes)
- Fan write lease coordinator (brief item P0.2, "revisit as the lease coordinator in 4.5"): one owner for fan writes across
  verification, cleaning, calibration, NVIDIA guard (`HeldLease` in `NvidiaPowerService` is the newest example). Must not hold a lock
  across `Task.Delay` verification; design first in a short doc, then implement behind tests.
- Trace and replay harness (P1.2): a fake `IHpWmiBios` that replays scrubbed traces from diagnostics exports, so a reporter's export
  becomes a regression test. Highest leverage for this project; start with the 8BD4, 8BC2 and 88F8 exports in this audit.
- Sensor identity record (P1.1): record which sensor feeds CPU temperature and why, stably across sessions (two boards now show a
  frozen ~28 C ACPI or BIOS CPU reading: 8E9A and 8BC2).
- Asymmetric temperature filter (P1.3): behind an off-by-default flag only.
- Shared capability resolver (P1.5): UI, tray and CLI still read separate flags.

**WP-20 Release hygiene remainder** (`todo`): Linux package suspend hook; `CLAUDE.md` update (it still names 4.4.1 as current and
lists 4.4.1 open threads); decisions for the maintainer on PR #147 and code signing; verify the new workflows on the first CI run.

## 7. Open-issue index (75 open issues and 1 open PR on 2026-10-12)

Every open item is listed once. "Owner reply last" means the maintainer answered last and the reporter has not responded.

**Active, in packages above**
| Issue | Board or topic | Where it is covered | State |
|---|---|---|---|
| #230 | 8F21 HyperX OMEN 16-ap1xxx | WP-07, K.15 | new, export read, entry added (`34951e2`), replied 2026-10-17 |
| #228 | 88D1 OMEN 15-en1xxx | WP-07, K.12 | replied 2026-10-12; entry added (`cd8bf89`); waiting on the reporter |
| #227 | 8BC2 Victus 16-r0xxx | WP-07, WP-14, K.2, K.17 | replied 2026-10-14; reporter answered (fans at 2000 RPM, will test one-zone RGB); level-20 finding and the opt-in setting posted 2026-10-17 |
| #226 | 8BD4 calibration, "stuck at 2000" | WP-06, K.1, K.17 | replied 2026-10-13; level-20 hint finding (K.17) not yet told to the reporter |
| #225 | 8A13 | WP-07 | reporter replied 2026-10-11 (6/6 pass) |
| #224 | 8E9F | WP-14, K.5 | reporter replied: keyboard no, light bar yes |
| #223 | 8EEC (Ryzen 7 260) | WP-17 | owner reply last; needs a 4.4.5 export with PawnIO |
| #222 | 8E35 | WP-11, WP-17, K.4 | reporter replied; files were config copies |
| #221 | 8DD0 temperature | section 3E | owner asked a question; no answer |
| #219 | Linux 8D2F | WP-09, K.10 | reporter replied 2026-10-05; kernel-guide answer posted 2026-10-17 |
| #218 | 878A | WP-06, WP-15, K.9 | reporter replied (1/6 on 4.4.1) |
| #217 | 88EE | WP-16, K.9 | reporter replied (6/6, idle fan question) |
| #215 | 88F7 | K.10 | undervolt test build promised |
| #212 | 8BD4 RGB | WP-14, K.11 | probe result posted 2026-10-12; owner has not replied; one-zone build note posted 2026-10-17 |
| #211 | 8BBE stuck fans | WP-04, K.9 | reporter replied with exports |
| #207 | 88F8 | WP-06, K.9 | reporter replied (5/6) |
| #195 | 8E35 | K.10 | reporter replied; rerun expected 6/6 |
| #189 | 8D87 factory curve | section 1 | waiting on mbilykov's run |
| #179, #151, #87 | Linux per-key RGB, 8D87 and 8D41 | section 3B | tester volunteered (#179) |
| #229 | 88ED Victus 16-e0xxx | WP-07, K.13 | replied 2026-10-14; reporter asks about replaced fans, answered 2026-10-17; keyboard row added (`cd8bf89`) |
| #142 | 8E9A | WP-14, K.14 | replied 2026-10-13; reporter answered with effects/startup questions, new reply drafted 2026-10-14 |

**Waiting on a reporter's export or test (owner reply last)**
#201 (8EDC), #158 and #159 (8A44), #145 (8D40), #143 (8DCD), #133 (UI performance), #130 and #171 (8E10), #103 (Ryzen AI 9 HX 375 undervolt),
#181 (8D41, OGH A/B), #163 (8BCA), #155 and #184 (8C2F Victus 15), #149 and #157 (Transcend 14, 8C58 and 8E41), #146 and #132 (88D2),
#134 (8A18), #128 (e0xxx), #66 (8A26), #54 (8D87), #14 (16-b0xxx per-key keyboard, likely Primax `0461:4E9B`), #177 (16-wf0032TX), #180
(startup and config).

**Linux, kernel-side limits (answer with WP-09)**
#219, #99, #84, #76, #28, #127, #126, #26, #60, #123. GUI blank window on Wayland and X11: #118, #114 (older, no new evidence).

**Fixed and shipped, candidates for the maintainer to close after checking the release**
#115 and #172 (8BBE entry), #205 (8C2D entry), #214 (`thermal.cpu_sensor` override), #220 (stuck fans after verification, fixed in 4.4.1),
#192 (updater SHA256 parsing), #185 (8E35 recognised), #173 (RAM Smart Clean), #46 and #16 and #12 and #111 and #48 (old, answered in the
2.x and 3.x line). Closing is the maintainer's call; verify each against the shipped release first.

**Declined or already exists**
#122 (headset support: a different driver family), #34 (battery saver: it is Power Automation), #131 (OMEN desktops: fan writes disabled by
a safety gate until the protocol is validated), #147 (PR: the 4.4.0 log-buffering part shipped; invite a resubmission of the tray and
uptime parts and close).

**Not on GitHub**
Discord: bobshmo (NVIDIA unlock, WP-08), BluSleeperOP (Transcend 14 hibernate, WP-18 and K.8). Reddit: MENTRAUZ (8BB3 export,
undervolt specifics, section 3B and 3D).

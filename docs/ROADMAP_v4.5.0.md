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
- **Will not do:** VBIOS flashing, bundling `nvflash`, or voltage/clock tuning in OmenCore. A bad flash can brick a laptop,
  and NVFlashB's licence is unclear (its README says MIT, its LICENSE file says GPLv3). The panel keeps asking users to
  supply their own `nvflash64.exe` or a ROM file.

### D. AMD and power
- Widen the SMU PM-table layouts (Ryzen 7 260, Ryzen 9 8940HX) from exports (data).
- A board-level CPU power-limit story for the MAX 16 Intel boards (8E9A reports 100 W+ in Gaming Hub against about 55 W
  with OmenCore). No limits are written today; this needs data before any write.
- The requested extra undervolting features (MENTRAUZ): get the specifics first; the 8E9A log shows the PawnIO MSR write
  failing on that board, so "more features" starts with why the existing path fails there.

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

## 4. What counts as done for 4.5.0

- Every item from section 1 has either a reporter's confirmation, a corrective fix, or an honest "still unconfirmed" in the notes.
- New board entries each cite their export and add a test; unproven features stay off.
- Full suite green (0 build warnings), installer and both zips built by CI, and the release notes carry the changelog
  and the hash table.
- The Needs-field-confirmation list is rewritten for whatever 4.5.0 adds.

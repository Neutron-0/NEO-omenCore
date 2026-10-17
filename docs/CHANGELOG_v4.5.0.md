# OmenCore v4.5.0

**Release Date:** TBD
**Type:** In development. Follow-up to 4.4.5.
**Roadmap:** `docs/ROADMAP_v4.5.0.md`

Nothing below is called confirmed unless a reporter has verified it on real hardware. Items marked
*pending* are implemented and tested in software only.

## Fixed

- **NVIDIA power unlock: the HP performance mode is chosen by firmware policy.** Before a CURRENT write on a Victus
  laptop, 4.4.5 always set HP's legacy Performance mode (`0x01`). V1 and newer firmware use `0x31`, which is what the
  rest of OmenCore sends on those boards, so on most Victus models the preparation step could be refused or ignored.
  V0 firmware keeps the legacy value. The write is still never sent if the preparation fails. Found by comparing with
  bobshmo's standalone Prophecy Power Unlocker 1.2.0. *Pending hardware confirmation.* Test: `NvidiaPowerServiceTests`.

- **Single-zone keyboards (8BD4, 8BC2): colour payload.** A tester's probe on 8BD4 (#212) showed the keyboard lights when the
  colour sits directly after the zone count (offsets 1 to 3) and stays dark at the four-zone offset (25). Boards that report a
  one-zone topology now get that layout. The four-zone layout is unchanged, and the callers that never passed a zone count
  (legacy lighting service, brightness scaling, restore) now follow the board's own topology instead of always sending 4.
  *Implemented, pending retest by the 8BD4 owner.* Test: `KeyboardLightingTopologyTests`.
- **Hotkeys held by another app are retried, then reported.** A key that fails to register is retried for about a minute
  (12 tries, 5 s apart), then logged once by name and action ("held by another app") and listed in the hotkey diagnostics.
  Before, it was retried only at startup and then dropped without a word. Test: `HotkeyServiceTests`.
- **Tray: GPU Power has an Extended item.** A profile set to Extended showed Medium in the tray because the menu stopped at
  Maximum (#142). The item mirrors the Tuning page list.
- **NVIDIA power unlock: Resolve VBIOS also finds `nvflashb.exe`** (the tool RTX 50 cards use) and its message says Apply CURRENT
  does not need nvflash. Reported through the Prophecy author. We do not ship nvflash. *Pending hardware confirmation.*
  Test: `NvflashLookupTests`.

- **Guided Fan Verification: a fan held by the same firmware ceiling as its sibling now passes.** On 88F8 (#207) the CPU fan
  settled at level 43/55 at 100% while the GPU fan reached 46/55 and passed; the CPU fan failed by two points. After each 100% step
  a fan within 90% of the best level another fan reached is judged by the same ceiling (evidence "FirmwareCeiling"). Two fans both
  stuck low still fail, and a physical tachometer reading zero is never upgraded. *Pending retest by the 88F8 owner.*
  Test: `FanVerificationCeilingTests`.
- **Fan verification: a refused per-fan write says so.** On 878A (#218) the write for a single fan is refused by the firmware; the
  result now reads "firmware refused level L" instead of "Physical RPM unavailable".

- **Board entries: 8BC2 (Victus 16-r0xxx Intel, #227), 88D1 (OMEN 15-en1xxx, #228) and an exact 88ED keyboard row (#229).** All three
  were borrowing a neighbouring board's profile by model name. Each entry holds only what the reporter's export measured (V1 policy,
  two fans, level readback, one-zone or four-zone keyboard) and leaves curves, GPU boost, undervolt and MUX off. `UserVerified` is
  false. 8BBE, 8BD4 and 8BC2 now say "Estimated only" for RPM on the capability card instead of "Supported". Tests:
  `ModelCapabilityDatabaseTests`, `KeyboardModelDatabaseTests`.

- **NVIDIA power panel explains what it read.** When the stock MAX read from the driver differs from the expected stock MAX for the
  GPU (a 115 W against 140 W case), the status line says both numbers and that a difference is normal with an override or a lower
  maker limit. New notes under the panel: keep PPAB (Dynamic Boost) on, a driver update can undo the unlock, the RTX 5050 is untested,
  and how to re-toggle if CURRENT will not stick. No new write path. Test: `NvidiaPowerStockNoteTests`.

- **Guided Fan Verification no longer blames the fan when thermal protection took over.** On 8BC2 (#227) protection fired during the run.
  A step that fails while protection is driving the fans is now listed as interrupted (and not counted as a failure) with a prompt to
  re-run after cooling, and a run waits up to three minutes for the machine to drop below 85 C before starting. Test: `GuidedRunRulesTests`.

- **Scenes: Wave, Spectrum and Breathing run on the keyboard itself.** On 8E9A (#142) a Heat Wave scene arrived as a plain colour because
  scene effects only reached third-party RGB providers. Keyboards with their own effect engine now get the matching effect (Wave, colour
  cycle, Breathing) in the scene's colour and speed; if the keyboard refuses it, the scene falls back to the static fill as before.
  *Implemented, pending confirmation by an 8E9A owner.* Test: `RgbSceneDeviceEffectTests`.

- **Experimental setting: do not pin a fan level when returning to Auto (off by default).** On several V1 boards (8BC2 #227, 8BD4 #226,
  88EE #217, 8DCD #143) the fans sit near 2000 RPM, even at 96 C, until the OEM profile is restored. The log points at a fan-level hint
  (level 20) that OmenCore sends when it hands fans back to the BIOS and that the disabled floor-clear never undoes. The setting skips the
  hint (restart needed). The cause is *not confirmed*; this is here so an owner can test it. Test: `WmiFanControllerLevelHintTests`.
- **Board entries: 8F21 (HyperX OMEN 16-ap1xxx, #230), and 8E41 (Transcend 14) now has two fans and level-only RPM (#157).** The fan
  diagnostics button now reads "Run Guided Fan Verification", matching what we tell people to look for.

## Added

- **OMEN key can show or hide the OSD.** New "ToggleOsd" choice for the OMEN key action in Settings. If the OSD is turned
  off in Settings the key logs that and does nothing. *Implemented, pending a user test.* Test: `OmenKeyActionTests`.
- **Conflict detection knows Ohman**, the other tool that sends HP fan and performance commands. The process name comes from
  the download's file name and has not been checked on a running copy.

- **Linux: other fan services are reported.** `omencore-cli diagnose` lists any of victus-fan, victus-backend, omen-fan-control or omenctld
  that are running (they also write `pwm1_enable`), and the daemon logs one warning at start. Nothing is stopped or killed.
  Test: `FanWriterScannerTests`.

- **Diagnostics export: three new sections for stuck-fan and wake reports.** `[Fan Firmware State]` (Max flag, fan level readback,
  thermal policy, fan count), `[Power Transitions]` (the last 20 sleep, resume and standby events with their reasons from the
  Windows event log) and `[CPU Throttle]` (Intel only: whether the CPU reports thermal or power-limit throttling right now, read from
  IA32_THERM_STATUS through the Tuning page's MSR handle; AMD and a closed Tuning page read "not available"). Each is read-only and a failing read writes one line. Reports like
  #211 had none of this, so their cause stayed open. Test: `FanFirmwareStateTests`.

- **Optional Max fan auto-return (off by default).** Settings, next to "Link fan to performance mode": Off keeps Max until you change it,
  Cooldown hands the fans back to BIOS Auto when CPU and GPU stay under 60 C for 2 minutes, Timer does it after 15, 30 or 60 minutes.
  The release uses the same path as choosing Auto yourself. There is no chassis sensor, so the hotter of CPU and GPU decides.
  *Implemented, pending hardware confirmation.* Tests: `MaxAutoReturnPolicyTests`, `FanServiceSuspendTests`.

## Release and CI (no change to the shipped program)

- **Release notes keep the changelog.** Release Build now writes the notes from the template, the version's changelog,
  and the SHA256 table the updater reads, instead of overwriting them with the template alone.
- **Linux QA tag job no longer creates a release.** It failed on every tag ("Resource not accessible by integration")
  and would have raced Release Build over the same release body. Release Build is the only publisher now.
- **CI actions moved off Node 20** (checkout, setup-dotnet, upload/download-artifact, softprops, Pages). Checked on
  the first CI run after the change (*pending*); the tag path (Release Build) only runs on the next release tag.

- **The Linux zip includes the keyboard suspend hook** (`scripts/linux/omencore-kbd-suspend.sh`), so it can be installed without cloning
  the repository. The Linux install guide says where to find it.
- **Workflows use the `ubuntu-24.04` runner instead of `ubuntu-latest`**, which moves to a newer image on 2026-10-19. A tag build should not be
  the first run on a new image; move to the new image on purpose after a normal CI run.

## Contributors

- **bobshmo**: the V0/V1+ performance-mode finding (standalone Prophecy Power Unlocker 1.2.0).
- **bobshmo**: also the nvflashb.exe (RTX 50) note and the request to catch up with the standalone's 1.2.x fixes.
- **VoRtam4**: the 8BD4 colour-payload probe that found the one-zone layout (#212).
- **Keith1341**: the 8E9A export and the tray GPU Power gap (#142).

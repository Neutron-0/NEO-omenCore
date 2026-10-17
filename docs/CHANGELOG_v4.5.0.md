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

## Added

- **OMEN key can show or hide the OSD.** New "ToggleOsd" choice for the OMEN key action in Settings. If the OSD is turned
  off in Settings the key logs that and does nothing. *Implemented, pending a user test.* Test: `OmenKeyActionTests`.
- **Conflict detection knows Ohman**, the other tool that sends HP fan and performance commands. The process name comes from
  the download's file name and has not been checked on a running copy.

- **Linux: other fan services are reported.** `omencore-cli diagnose` lists any of victus-fan, victus-backend, omen-fan-control or omenctld
  that are running (they also write `pwm1_enable`), and the daemon logs one warning at start. Nothing is stopped or killed.
  Test: `FanWriterScannerTests`.

## Release and CI (no change to the shipped program)

- **Release notes keep the changelog.** Release Build now writes the notes from the template, the version's changelog,
  and the SHA256 table the updater reads, instead of overwriting them with the template alone.
- **Linux QA tag job no longer creates a release.** It failed on every tag ("Resource not accessible by integration")
  and would have raced Release Build over the same release body. Release Build is the only publisher now.
- **CI actions moved off Node 20** (checkout, setup-dotnet, upload/download-artifact, softprops, Pages). Checked on
  the first CI run after the change (*pending*); the tag path (Release Build) only runs on the next release tag.

## Contributors

- **bobshmo**: the V0/V1+ performance-mode finding (standalone Prophecy Power Unlocker 1.2.0).
- **bobshmo**: also the nvflashb.exe (RTX 50) note and the request to catch up with the standalone's 1.2.x fixes.
- **VoRtam4**: the 8BD4 colour-payload probe that found the one-zone layout (#212).
- **Keith1341**: the 8E9A export and the tray GPU Power gap (#142).

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

## Release and CI (no change to the shipped program)

- **Release notes keep the changelog.** Release Build now writes the notes from the template, the version's changelog,
  and the SHA256 table the updater reads, instead of overwriting them with the template alone.
- **Linux QA tag job no longer creates a release.** It failed on every tag ("Resource not accessible by integration")
  and would have raced Release Build over the same release body. Release Build is the only publisher now.
- **CI actions moved off Node 20** (checkout, setup-dotnet, upload/download-artifact, softprops, Pages). Checked on
  the first CI run after the change (*pending*); the tag path (Release Build) only runs on the next release tag.

## Contributors

- **bobshmo**: the V0/V1+ performance-mode finding (standalone Prophecy Power Unlocker 1.2.0).

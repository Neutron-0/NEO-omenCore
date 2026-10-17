# My board has no `platform_profile`: what is going on, and what can be done

This page answers a question that keeps coming up on Linux (#219, #99, #84, #76, #28, #127, #26, #60, #123): the OmenCore
diagnostics say the board is `profile-only` or that performance modes cannot be set, and `/sys/firmware/acpi/platform_profile`
does not exist.

## Why OmenCore cannot route around it

On Linux, performance modes and manual fan speed on an HP laptop go through the kernel's `hp-wmi` driver
(`drivers/platform/x86/hp/hp-wmi.c`). That driver only registers the `platform_profile` interface, and only exposes fan PWM
controls, for board IDs it has been told about. If your board is not on its lists, the files OmenCore would write to are not
there. OmenCore does not write to the embedded controller behind the kernel's back on boards it has no evidence for, because
that is exactly how fan faults on the OMEN MAX family happened (#60). So the fix lives in the kernel, not in OmenCore.

## Step 1: collect the facts

Run these and keep the output:

```bash
cat /sys/class/dmi/id/board_name        # the 4-character board ID, for example 8D2F
cat /sys/class/dmi/id/product_name
cat /sys/class/dmi/id/bios_version
uname -r
lsmod | grep hp_wmi
sudo dmesg | grep -i -E "hp[-_]wmi|hp_wmi"
ls /sys/firmware/acpi/platform_profile* 2>&1
ls /sys/devices/platform/hp-wmi/ 2>&1
ls /sys/class/hwmon/*/name 2>&1
sudo ./omencore-cli diagnose --report > report.txt
```

Two answers matter most:

- **`dmesg` shows nothing from `hp_wmi` and `lsmod` shows no module:** the driver may not be bound to your machine at all.
  Try `sudo modprobe hp_wmi` and look again. If it still does nothing, say so in your report; that is a different problem from
  "board not on the list".
- **`hp_wmi` is loaded but no `platform_profile`:** the board is probably missing from the driver's lists.

## Step 2: send a board-ID patch upstream

Kernel patches go to the maintainers by email. A first patch is fine; the documentation is
[Submitting patches](https://www.kernel.org/doc/html/latest/process/submitting-patches.html).

1. Get the kernel source for the version you run (`git clone` of a mainline or distribution tree).
2. Find who to send it to, from inside the source tree:
   ```bash
   ./scripts/get_maintainer.pl -f drivers/platform/x86/hp/hp-wmi.c
   ```
   The platform driver list is `platform-driver-x86@vger.kernel.org`; `get_maintainer.pl` prints the current maintainers and
   any other lists to copy.
3. Look at how an existing board is added: search `hp-wmi.c` for a board ID close to yours (the lists are arrays of board-name
   strings). A new board is usually one string in the right list. Do not guess which list: pick the list that matches the
   behaviour your board actually has, and say how you know.
4. Create the commit with `git commit -s` (this adds the required sign-off line), and generate the email with
   `git format-patch` and send it with `git send-email`.
5. In the cover text include: the board name from `/sys/class/dmi/id/board_name`, the product name, the BIOS version, the kernel
   version you tested, what works and what fails before the change, and what you saw after it (modes listed in
   `platform_profile_choices`, whether a mode change moved the fans). A maintainer cannot accept a board on a guess.

If sending patches by email is new to you, the kernel documentation page above walks through it, and a distribution bug
tracker (Fedora, Ubuntu, Arch) is a reasonable place to ask for help getting the same patch into the distribution kernel.

OmenCore cannot do any of this for you and does not control what the maintainers accept.

## Step 3: what upstream already covers

The driver changes over time. For example, mainline commit `46be1453e6e6` (platform/x86: hp-wmi: add manual fan control for
Victus S models, January 2026) added manual fan speed control for Victus S boards. Whether a given feature reaches you depends
on your board being on the list and on your kernel including the commit, so compare your kernel version against the version
that contains the change you need before assuming it should work.

## Out-of-tree drivers: a safety checklist

Several third-party drivers (DKMS packages) add support the kernel lacks. Some are fine, some write to the embedded controller
directly. Before installing any of them, check:

- **Does it write the EC directly?** Direct EC writes bypass the firmware's own checks. On the OMEN MAX family this has caused
  faults (#60). Prefer a driver that talks to the firmware through WMI, the same interface `hp-wmi` uses.
- **Does it list your board?** A driver that does not name your board ID is guessing. Read its source or board table.
- **Can you undo it?** DKMS packages can be removed (`sudo dkms remove`), but a bad EC write can persist across reboots until
  the firmware resets. Know the reset method for your laptop (power off and hold the power button) before you try.
- **Is it GPL and maintained?** Look for recent commits, open issues about your hardware, and a licence file.
- **Do not run two fan writers.** If OmenCore, the driver and a fan service all write the same files, they undo each other.
  `omencore-cli diagnose` lists other fan services it finds.

If a driver's README says it works "via EC", treat that as a reason for more caution on a recent board, not less.

## What to send us

Post the output of Step 1 on the issue or on Discord. It lets us record the board in the capability matrix and, when the
kernel catches up, stop reporting it as `profile-only`.

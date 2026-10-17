# Design note: one owner for fan writes (WP-19, first step)

Status: migration step 1 implemented 2026-10-18 (`AcquireFanWriteLease`, wrappers on one anonymous lease; tests in `FanServiceSuspendTests`). Steps 2 to 4 and the trace work are not started. WP-19 says to design first, then build behind tests, and not to mix
this with behaviour changes.

## What exists today

Several features write fan state and need the curve engine and keepalive to stand down while they do:

| Feature | Where it takes control | How it gives it back |
|---|---|---|
| Guided Fan Verification, single-fan test | `FanDiagnosticsViewModel` lines ~178 and ~339 | `ExitDiagnosticMode()` in a `finally` |
| Verification from the Fan Control page | `FanControlViewModel` line ~1729 | `ExitDiagnosticMode()` |
| Fan cleaning | `SettingsViewModel` line ~3749 (`FanCleaningService`) | `ExitDiagnosticMode()` |
| NVIDIA CURRENT write (Victus) | `NvidiaPowerService` line ~114 (`HeldLease`) | `ExitDiagnosticMode()` in `Dispose` |
| Calibration | `FanCalibrationService` writes directly, no mode taken | its own `RestoreAutoControl` loop |
| Thermal protection, watchdog failsafe | their own flags in `FanService` and `HardwareWatchdogService` | their own release rules |

The mechanism is `FanService.EnterDiagnosticMode()` / `ExitDiagnosticMode()` (`FanService.cs` lines ~556 to ~590): one boolean
(`_diagnosticModeActive`) plus a static counter of how many `FanService` instances have it on (`IsAnyDiagnosticModeActive`).

## The problems

1. **It is a flag, not a lease.** A second `EnterDiagnosticMode()` while one is active returns at once, and the first
   `ExitDiagnosticMode()` clears the flag for both. If a single-fan test is started while the guided run is still going, the guided
   run's remaining steps run with the curve engine and keepalive live again.
2. **No owner.** The log says "Entered fan diagnostic mode" and nothing says who. A stuck flag (an owner that never exits) cannot be
   traced to a caller.
3. **Calibration takes no lease at all**, so the curve engine and its writes can interleave with the wizard.
4. **The rules for who wins are spread out.** `ApplyPreset` refuses during diagnostics (line ~1025), the curve loop checks the flag
   (~2516, ~2610), the keepalive checks the static counter, thermal protection overrides everything, and the watchdog has its own
   hold. Each is correct alone; the order they apply in is only written down in code.

## Proposal

Keep `EnterDiagnosticMode` / `ExitDiagnosticMode` as thin wrappers so no caller changes in step one, and add:

```csharp
public sealed class FanWriteLease : IDisposable { public string Owner { get; } ... }
public FanWriteLease AcquireFanWriteLease(string owner);   // reference counted, per FanService
public IReadOnlyList<string> FanWriteLeaseOwners { get; }   // for diagnostics
```

- `_diagnosticModeActive` becomes `leaseCount > 0`; the static counter keeps working off the first lease in and last lease out.
- `Dispose` on a lease is idempotent, so a `using` block or a `finally` that runs twice cannot over-release.
- The wrappers acquire one anonymous lease per `FanService` ("legacy caller") and release it on `Exit`, which keeps today's behaviour
  exactly for code that has not been migrated.
- The `[Fan Firmware State]` diagnostics section lists the current owners, so a stuck lease names its holder.
- Order of precedence is written once, as a table in this file and as a test: thermal protection, then watchdog failsafe, then a
  held lease, then the curve engine, then keepalive.

Rules the implementation must keep (from `CLAUDE.md` and the existing gates):

- A lease is never held across a `Task.Delay` verification wait by a lock; it is a counter, not a monitor.
- Anything that sets the firmware Max flag still releases through `HpWmiBios.ReleaseMaxAndHandBackToBios()`
  (`FirmwareMaxPairingGateTests`).
- Thermal protection must still be able to raise fans while a lease is held (guided runs can overheat the machine, see the 8BC2 log).

## Tests (written before the change)

1. Two owners acquire, the first releases: the engine stays suspended; the second releases: it resumes.
2. Releasing the same lease twice does not go below zero or release another owner's lease.
3. The legacy `Enter`/`Exit` pair behaves as it does now, including `Exit` without `Enter` being a no-op.
4. `ApplyPreset` is refused while any lease is held and allowed after the last release.
5. Thermal protection still applies fan changes while a lease is held.
6. The static `IsAnyDiagnosticModeActive` is true while any `FanService` holds a lease.

## Migration order

1. Add the lease type and make the wrappers use it (no caller change). Tests 1 to 6 pass.
2. Move the five callers above to `using var lease = _fanService.AcquireFanWriteLease("Guided Fan Verification")` and so on.
3. Give calibration a lease.
4. Add the owners to `[Fan Firmware State]`.

Each step is its own commit; step 1 changes no behaviour a caller can see except the two-owner case in problem 1.

## Second item: trace and replay (P1.2)

A fake `IHpWmiBios` that replays a scrubbed trace taken from a diagnostics export, so a reporter's export becomes a regression test.
Start with the three exports in the roadmap's K sections that already contain full command traces (8BD4, 8BC2, 88F8). Needs a trace
format first: a list of `{command, input bytes, output bytes or error}` per call, taken from `wmi-command-history.txt`. Open question
for the maintainer: how much of an export may be kept in the repo (serials and SKUs are scrubbed; board IDs and BIOS versions stay).

## Decision needed

Go-ahead for step 1 (it touches `FanService`, the file most other behaviour hangs off), and a yes or no on keeping scrubbed exports as
test data in `src/OmenCoreApp.Tests/Traces/`.

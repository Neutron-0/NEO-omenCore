# OmenCore v3.6.0 Roadmap

## Scope Source
This roadmap captures all forward-looking and deferred items moved out of the v3.5.0 changelog so v3.5.0 remains implementation/evidence focused.

## Carry-Over Reliability Work

### Fan and Profile State
- Single desired fan-state owner across fan service/controller paths with explicit Auto, Performance, MaxHold, ManualCurve, Transitioning states.
- Continue tightening requested vs confirmed behavior across sidebar, tray, fan page, OMEN/system page, startup restore, and hotkey flows.
- Additional regression coverage for profile transitions with fan/performance link on and off.
- Rebalance aggressive built-in fan curves (especially Gaming/Extreme) so moderate thermals do not ramp to near-max fan speed unnecessarily; keep Max as the explicit full-cooling mode.

### Linux Hold and Capability UX
- Continue daemon hold hardening for board and kernel variance.
- Improve Linux capability diagnostics for root/write path, hp-wmi/ec_sys/debugfs, and package/service prerequisites.
- Complete first-class service status/install/remove guidance in Linux packaging and docs.

### RGB Control Robustness
- Expand backend matrix and fallback sequencing for affected 4-zone systems.
- Add explicit ownership state for OmenCore vs OMEN Light Studio/OmenCap contention.
- Add a dedicated restore action for keyboard lighting fallback/recovery.

## Resource and Optimization Tracks

### Idle CPU and RAM Efficiency
- Further coalesce background timers and remove redundant startup probes.
- Increase lazy initialization coverage for optional providers/subsystems.
- Continue reducing recurring keepalive log noise with state-change and periodic summaries.
- Reuse hardware worker state to avoid repeated capability scans.

### Fan and Performance Reliability
- Expand readback-first verification for fan and power-limit paths.
- Extend bounded command-history and external-reset evidence/reporting.
- Add deeper tests around V1 WMI transitions, max hold, and default handoff behavior.

### RGB Reliability
- Add per-backend control-path diagnostics in UI/export.
- Continue provider lazy-load and startup probe minimization.
- Expand ownership visibility for HP keyboard and external RGB controllers.

## Optimizer and Cleanup Tracks

### System Optimizer
- Persist restore manifest/backup flow explicitly across restarts.
- Expand preflight to include exact operation-level impact and reversibility details.
- Keep risk-tier profile separation explicit and user-driven.
- Improve drift explanation coverage and export/report fidelity.
- Add hardware-aware recommendations (battery state, storage class, build/edition).

### Bloatware Manager
- Continue dependency metadata enrichment and risk hints.
- Add startup/scheduled-task quarantine path before destructive removal.
- Improve post-removal verification granularity (current user, all users, provisioning).
- Continue curated preset refinement for standalone prep workflows.

### Memory Optimizer
- Expand pressure-aware triggers and cooldown tuning.
- Continue game-aware quiet-window refinement to reduce stutter risk.
- Add richer before/after metrics for commit/standby/cache/paging impact.
- Improve process exclusion suggestions and guidance text.

## Tuning Safety and Verification
- Continue requested/applied/verified separation for all tuning surfaces.
- Extend conflict detection matrix and mitigation guidance.
- Add additional test-mode rollback triggers and event-based safety checks.
- Continue model-aware defaults and write/readback capability gating.
- Expand exportable tuning safety report coverage.

## UX and Visual Polish
- Add compact screenshot-friendly diagnostics surfaces where needed.
- Continue fan/profile wording and status clarity improvements.
- Deliver first-party visual asset updates (dashboard, fan, performance, RGB, installer).
- Expand status iconography for confirmed/degraded/blocked/overwritten states.

## Release Readiness Dependencies
- Physical fan/RGB validation on affected OMEN hardware.
- Linux long-hold validation on target board/kernel combinations.
- Tray/minimized cadence before/after measurement evidence.

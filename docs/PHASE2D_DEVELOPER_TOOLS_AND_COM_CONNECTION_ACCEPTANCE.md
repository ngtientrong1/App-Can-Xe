# Phase 2D — Developer Tools & COM Connection Acceptance

**Acceptance date:** 2026-06-27

## Verified on real scale PC

- DeveloperMode=true shows DEV drawer
- DEV drawer opens and closes without changing COM state
- Manual simulation accepts 1730 kg
- Return to zero works
- Switching back to Hardware reconnects once
- Restart returns to Hardware mode
- COM1 / 1200 auto-connect stable
- No overlapping connect operations
- No UpdateSettings while port is open
- 410 tests passed

## DeveloperMode

| Item | Result |
|------|--------|
| Default `DeveloperMode` | `false` in production appsettings |
| Production UI | No DEV button or drawer |
| Test configuration | DEV drawer, diagnostics, simulation controls |
| DEV open/close | Does not change active scale source or trigger connect |
| Mode selection | Uses explicit `appsettings.json` setting, not Debug/Release |

## Manual simulation

| Item | Result |
|------|--------|
| SimulationManual | Disconnects COM, stops hardware reading |
| Input range | Integer kg 0–999999 |
| Apply / Reset | ÁP DỤNG and VỀ 0 update simulated reading only |
| Ticket workflow | Does not auto-capture or auto-save |
| Restart on Hardware deployment | Returns to Hardware and auto-connects COM |

## XAML binding

| Item | Result |
|------|--------|
| DEV diagnostic displays | All `Run.Text` bindings use `Mode=OneWay` |
| Read-only properties | No TwoWay binding on diagnostic getters |
| Startup smoke | `MainWindow.Show()` with full ViewModel and dispatcher pump |

## COM connection

| Item | Result |
|------|--------|
| Connection gate | Semaphore at reader, service, and ViewModel layers |
| Reconfigure pipeline | Atomic close → apply settings → reset → open |
| UpdateSettings safety | Throws when port is open; startup never calls it on open port |
| Duplicate connect | Prevented by serialized connect and auto-connect generation |
| Retry | Sequential 0s / 2s / 5s; cancelled on success or mode change |
| DEV drawer | Open/close does not connect or disconnect COM |
| Manual → Hardware | Single reconfigure/connect cycle |
| Dispose | Idempotent service dispose |

## Logging (local only)

Runtime logs written under `%LOCALAPPDATA%\CanXe\Logs\`:

- `startup-error.log`
- `scale-mode.log`
- `scale-connection.log`

These files are excluded from the repository.

## Out of scope for this document

- Publish output, deployment ZIP files, runtime databases, and production log files remain local only.

# Phase 2D — Operator UI & Auto-Connect Acceptance

**Acceptance date:** 2026-06-27

## Verified on real scale PC

- Windows 10 build 19045
- COM1 / 1200 / 8N1 / None / One / None
- Automatic COM connection on startup
- Hardware mode persisted
- Legacy `SimulationAutomatic` DB normalized to Hardware
- No simulation in Hardware mode
- 16:9 operator layout verified
- Unit price input verified
- App restart verified

## Operator UI (16:9)

| Item | Result |
|------|--------|
| Default tab on startup | PHIẾU CÂN |
| Weight display area | Enlarged, responsive font via `WorkAreaLayoutCalculator` |
| Data entry form | Compact layout, no clipped TextBox text |
| Camera panel | Responsive within work area |
| Status bar | Does not overlap action buttons |
| Footer and ticket table | Flex with window resize |
| Layout at 1366×768 | No breakage |
| Unit price | Clear display; parse/format via `UnitPriceInputHelper` |

## Auto-connect COM

| Item | Result |
|------|--------|
| Default port / baud | COM1 @ 1200, 8N1, no handshake |
| `AutoConnectScaleOnStartup` | Stored in DB; default enabled |
| Auto-connect scope | Hardware mode only |
| UI thread | Non-blocking connect with bounded retry |
| Manual disconnect | Does not immediately auto-reconnect |
| Persistence | COM settings and auto-connect flag saved to DB |
| App restart | Remains Hardware; reconnects when configured |

## Mode sync

| Item | Result |
|------|--------|
| `DeviceMode=Hardware` | Always resolves to `ScaleInputMode.Hardware` |
| Legacy DB simulation values | Normalized to Hardware on startup |
| Radio buttons | TwoWay binding updates ViewModel immediately |
| Sync targets | UI mode, DB mode, and `CompositeScaleService` aligned |
| Select Hardware | Stops simulation; no simulated reading retained |
| Header / ticket source | Reflect COM connection state and Hardware source |

## Startup robustness

| Item | Result |
|------|--------|
| XAML resource order | `CanXeDesignSystem.xaml` — `SectionHeaderTextStyle` before dependents |
| Startup smoke tests | `CanXe.Desktop.Tests` loads `MainWindow` without parse errors |
| Startup error logging | `%LOCALAPPDATA%\CanXe\Logs\startup-error.log` |
| Scale mode diagnostics | `%LOCALAPPDATA%\CanXe\Logs\scale-mode.log` |

## Automated test gate

- **374 / 374** tests pass (`dotnet test CanXe.sln -c Release`)
- Protocol parser unchanged (no modifications in `CanXe.ScaleProtocol.Core`)

## Out of scope for this document

- Publish output (`publish/`), deployment ZIP files, runtime databases, and log files from production machines remain **local only** and are excluded from the repository.

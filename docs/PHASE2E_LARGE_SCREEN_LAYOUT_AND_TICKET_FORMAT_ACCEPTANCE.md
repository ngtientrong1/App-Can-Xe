# Phase 2E — Large-Screen Layout & Ticket Number Format Acceptance

**Acceptance decision:**

- Primary target resolutions: **1920×1080 and above** (including 2560×1440 and 2560×1600).
- **1366×768** remains supported at a **basic operational level** only.
- No further layout specialization for 1366×768 is required.
- Ticket display number uses a **minimum of two digits** (`D2` presentation format).

## Layout policy

### Primary targets (1920×1080+)

| Item | Result |
|------|--------|
| Workspace height | Capped on tall screens (~40% usable height, max ~480px) |
| DataGrid | Row `Height="*"` receives remaining vertical space |
| Footer | Compact summary bar (≤72px) |
| Live weight font | Width-responsive, capped on tall screens (≤88px) |
| Camera / DEV drawer | Overlay/drawer; does not replace main grid star row |

### 1366×768 (basic support)

| Item | Result |
|------|--------|
| App stability | Opens without crash |
| COM auto-connect | Unchanged from Phase 2D |
| Weigh + form | Usable; horizontal overflow avoided |
| DataGrid | Scrollable; row count not optimized |
| Footer | May have limited space; not hidden |

## Ticket number format

Presentation uses `TicketNumberFormatter` with minimum two digits:

| Sequence | Display (month 6) |
|----------|-------------------|
| 1 | `01/06` |
| 6 | `06/06` |
| 10 | `10/06` |
| 100 | `100/06` |

Legacy stored values (e.g. `0006/06`) normalize on read via `ResolveDisplayNumber`.

`InternalCode` retains four-digit sequence for internal/sort identity.

No database migration for presentation-only change.

## Formatter usage

- Current ticket on form (preview + saved)
- Ticket list (DataGrid)
- Ticket detail / view
- Print document renderer
- Edit/continuation draft load

## Out of scope (unchanged)

- Scale protocol parser, COM connection serialization
- DeveloperMode, DEV drawer, Manual Simulation
- Weigh capture, save workflow, camera drawer behavior

## Verification

- Release build succeeds
- Full test suite passes
- Publish path: `C:\CanXeApp\publish\win10-x64`
- Smoke: `DeviceMode=Hardware`, `DeveloperMode=true` — COM, DEV, manual simulation operational

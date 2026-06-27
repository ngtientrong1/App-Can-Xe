# Phase 2C — Scale Hardware Acceptance

**Hardware acceptance date:** 2026-06-27

## Serial configuration

| Setting | Value |
|---------|-------|
| Port | COM1 |
| Baud rate | 1200 |
| Data bits | 8 |
| Parity | None |
| Stop bits | One |
| Handshake | None |

## Protocol frame

- **Length:** 12 bytes
- **STX:** `0x02` (index 0)
- **Sign:** `+` / `-` (index 1)
- **Weight digits:** indexes 2–7 (6 ASCII digits, kg)
- **Protocol code:** indexes 8–9 (observed `01`)
- **Checksum:** index 10 (XOR of bytes 0–9 and 11, upper-case hex nibble)
- **ETX:** `0x03` (index 11)

Example frames:

| Condition | Payload (STX/ETX omitted) |
|-----------|---------------------------|
| Empty scale (0 kg) | `+00000001B` |
| Person (~50 kg) | `+00005001E` |
| Loaded vehicle (1,730 kg) | `+00173001…` (checksum per live capture) |

## Verified weights (live hardware)

| Weight | Stable | Checksum | Notes |
|--------|--------|----------|-------|
| 0 kg | Yes | Valid | Empty platform |
| 50 kg | Yes | Valid | Single person |
| 1,730 kg | Yes | Valid | Real load test on production scale |

## Verified behaviour

- Checksum validation passes on all captured stable frames.
- Stability detector: 8 consecutive matching frames, zero spread, 1 s max gap.
- DeviceTester decoded panel shows weight, stability, and last frame.
- CanXe Desktop connects via **KẾT NỐI** on tab **THIẾT BỊ** and displays live hardware weight.
- **No simulated reading** when `DeviceMode=Hardware` and `ScaleInputMode=Hardware`.
- Hardware mode does **not** auto-save weigh tickets; operator must capture explicitly.
- **Read-only COM:** `WindowsScaleSerialReader` and DeviceTester open the port for read only — no `Write`, `Send`, or command bytes.

## Startup (Desktop)

When `appsettings.json` contains `"DeviceMode": "Hardware"`:

- Default scale source: **Đầu cân COM** (`ScaleInputMode.Hardware`).
- Header: `● Đầu cân: COM` (or disconnected badge until connected).
- Ticket source line: `Nguồn: Đầu cân COM`.
- Scale source can be changed on tab **THIẾT BỊ** without opening the DEV drawer.

## Out of scope for this document

- Raw capture files, session JSON, operator photos, and private protocol samples remain **local only** and are excluded from the repository (see `.gitignore`).

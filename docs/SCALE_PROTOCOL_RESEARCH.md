# Scale Protocol Research

Research notes from Phase 2B offline analysis through Phase 2C live hardware acceptance.

## Production protocol confirmed (2026-06-27)

| Parameter | Value |
|-----------|-------|
| Baud rate | **1200** |
| Data bits | **8** |
| Parity | **None** |
| Stop bits | **One** |
| Handshake | **None** |
| Frame length | **12 bytes** |

### Frame layout

| Index | Field |
|-------|-------|
| 0 | STX `0x02` |
| 1 | Sign (`+` / `-`) |
| 2–7 | Weight (6 ASCII digits, integer kg) |
| 8–9 | Protocol code (observed `01`) |
| 10 | Checksum (XOR bytes 0–9 and 11 → upper-case hex ASCII) |
| 11 | ETX `0x03` |

Implementation: `CanXe.ScaleProtocol.Core` (`ScaleFrameParser`, `ScaleProtocolChecksum`, `WindowsScaleSerialReader`).

Live acceptance: see [PHASE2_SCALE_HARDWARE_ACCEPTANCE.md](./PHASE2_SCALE_HARDWARE_ACCEPTANCE.md).

## Historical diagnostic data (not production)

The following observations come from **early Phase 2B captures at 9600 baud** and offline bit-pattern analysis. They helped narrow the search space but **do not describe the confirmed production protocol**:

- Predominant bytes **`0x00`** and **`0x80`** in 9600-baud streams.
- Hypothesised 1-byte or bit-cell framing.
- Candidate decoders and scoring from Protocol Analyzer exports.
- Legacy test-app string `COM1 9600 n 8 1 10` and ASCII sample `+0282.77 G S`.

Treat these as **diagnostic history only**. Do not configure production Desktop or DeviceTester with 9600 baud unless deliberately re-running historical analysis.

## Phase 2B — Offline Protocol Analyzer

Tools (no COM access, no write/send):

- `CanXe.ProtocolAnalyzer.Core` — log parser, replay, timing, bit transform, candidate decoder/scoring.
- `CanXe.ProtocolAnalyzer` — CLI export to Markdown/JSON.
- `CanXe.ProtocolAnalyzer.Tests` — offline regression tests.

Guide: [PROTOCOL_ANALYZER_GUIDE.md](./PROTOCOL_ANALYZER_GUIDE.md).

## Guided Capture (DeviceTester)

Operator-guided sessions for protocol confirmation:

| Session type | Purpose |
|--------------|---------|
| `EmptyStable` | Known 0 kg, stable |
| `PersonStable` | Known person weight, stable |
| `EmptyPersonTransition` | Step change empty → loaded |

Outputs per session (local, git-ignored):

- `.log` — human-readable trace
- `.raw.bin` — byte-for-byte serial capture
- `.session.json` — metadata schema v2 (known weight, stable flags, event markers)

Guide: [GUIDED_SCALE_CAPTURE.md](./GUIDED_SCALE_CAPTURE.md).

## Sample sessions (external, not in repo)

Place private captures under `protocol-samples/private/` or `C:\CanXeProtocolSamples` on the operator PC. Never commit real scale logs, raw binaries, or session JSON.

## Related documentation

- [DEVICE_TESTER_GUIDE.md](./DEVICE_TESTER_GUIDE.md)
- [PHASE2_SCALE_HARDWARE_ACCEPTANCE.md](./PHASE2_SCALE_HARDWARE_ACCEPTANCE.md)
- [GUIDED_SCALE_CAPTURE.md](./GUIDED_SCALE_CAPTURE.md)

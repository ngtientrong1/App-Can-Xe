# CanXe Protocol Analyzer Guide

Offline WPF tool for analyzing DeviceTester serial capture logs. **Does not open COM ports.**

## Projects

| Project | Role |
|---------|------|
| `CanXe.ProtocolAnalyzer.Core` | Parse logs, reconstruct byte stream, timing/bit/decoder analysis, scoring, reports |
| `CanXe.ProtocolAnalyzer` | WPF UI: import, replay, analyze, export |
| `CanXe.ProtocolAnalyzer.Tests` | Unit and smoke tests |

## Workflow

1. Capture logs with **CanXe Device Tester** on the scale PC (read-only COM).
2. Copy `.log` files to a private folder (e.g. `C:\CanXeProtocolSamples`).
3. Launch **CanXe.ProtocolAnalyzer**.
4. Import logs (file picker or drag-and-drop).
5. Review/edit session metadata (known weight, stable/dynamic).
6. **Run full analysis** — timing, periodicity, bit-cell transforms, decoder candidates, multi-session scoring.
7. **Export report** — Markdown + JSON.

## Batch CLI (no UI)

```cmd
CanXe.ProtocolAnalyzer.exe --analyze-batch --input C:\CanXeProtocolSamples --output C:\CanXeProtocolSamples
```

Produces:

```text
ProtocolAnalysisReport.md
ProtocolAnalysisReport.json
```

## Replay

- Speeds: 0.25x, 0.5x, 1x, 2x, 10x, Instant
- Shows current chunk HEX and accumulated continuous stream
- Instant replay runs off the UI thread

## Publish

```cmd
build\publish-protocol-analyzer.cmd
```

Output: `publish\protocol-analyzer-win10-x64\`

## Phase 2B constraints

- No integration with `CanXe.Desktop` hardware mode
- No production parser — research tool only
- Confidence capped below production-ready (max 84 in scoring)

See also: [SCALE_PROTOCOL_RESEARCH.md](SCALE_PROTOCOL_RESEARCH.md)

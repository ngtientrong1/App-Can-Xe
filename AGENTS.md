# CanXe — Hướng dẫn cho Agent

## Tổng quan

**Giai đoạn hiện tại:** 1.7.1 — hoàn thiện UI theo mockup + chế độ nguồn cân rõ ràng.

**Branch:** `phase1-7-apple-ui-and-settings` (Phase 1.7 + 1.7.1 **chưa commit** — chờ nghiệm thu thủ công)

**Commit nền Phase 1.6:** `e68b9e2` — `feat: complete phase 1.6 MVP finalization and UI hotfixes`

## Lệnh

```bash
dotnet restore
dotnet build CanXe.sln
dotnet test CanXe.sln
build\publish-win10-x64.cmd
```

Publish: `C:\CanXeApp\publish\win10-x64\`

## MVP 1.7 / 1.7.1

- Design system: `src/CanXe.Desktop/Themes/CanXeDesignSystem.xaml` — card, nav, rail, segment, summary, DataGrid.
- Navigation active state; mặc định PHIẾU CÂN.
- Layout: cân 31* / form 51* (camera mở) hoặc 69* (camera đóng); rail 56px; camera drawer 300px.
- `ScaleInputMode` + `ScaleInputModeDisplay` — automatic/manual/hardware; không persist manual.
- DEV drawer: segment mode, TRỞ VỀ TỰ ĐỘNG, đóng drawer không đổi mode.
- Cấu hình DB: `StationSettings`, `ScaleDeviceSettings`, `CameraDeviceSettings`.
- RTSP password: DPAPI; mock connection testers.
- Autocomplete policy + styled dropdown.
- Migration `202606270002_Phase17_StationAndDeviceSettings`.

## Chưa triển khai

COM scale thật, RTSP stream thật, Brother print, Excel export thật.

## Tests

**163 tests** (gồm `Phase171ScaleInputModeTests` — 28 case cho mode, layout, nav, footer, compact).

Chạy nghiệm thu thủ công theo checklist Phase 1.7.1 (mockup screenshots + scale source flow).

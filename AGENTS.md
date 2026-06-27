# CanXe — Hướng dẫn cho Agent

## Tổng quan

**Giai đoạn hiện tại:** 1.3 — bố cục cân, autocomplete bàn phím, tab navigation, cột Cân một lần, số phiếu dự kiến.

## Lệnh

```bash
dotnet restore
dotnet build CanXe.sln
dotnet test CanXe.sln
build\publish-win10-x64.cmd
```

## UI chính

- **Cột trái:** trọng lượng trực tiếp + Cân lần 1/2 + Tổng/Bì/Hàng + billing.
- **Cột giữa:** form phiếu; số dự kiến qua `GetPreviewDisplayNumberAsync()` (Peek, không increment).
- **Autocomplete:** `AutoCompleteTextBox` + `AutoCompleteSelectionLogic` (Domain, unit-testable).
- **Danh sách:** cột `SingleRecordedWeightKg` khi `EventCount == 1`.

## Cấu hình

`appsettings.json`:

- `DeviceMode`: Simulation
- `ShowDeveloperPanel`: true/false — panel DEV mô phỏng

## Tài liệu

- [docs/PRODUCT_SPEC.md](docs/PRODUCT_SPEC.md)
- [docs/UI_SPEC.md](docs/UI_SPEC.md)

Chưa triển khai COM/RTSP/in/Excel thật.

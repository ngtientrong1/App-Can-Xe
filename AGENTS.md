# CanXe — Hướng dẫn cho Agent

## Tổng quan

**Giai đoạn hiện tại:** 1.5 — UI 16:9 responsive, ngữ cảnh xe từ lịch sử phiếu, bộ lọc nhanh/nâng cao.

## Lệnh

```bash
dotnet restore
dotnet build CanXe.sln
dotnet test CanXe.sln
build\publish-win10-x64.cmd
```

## Ngữ cảnh xe (`VehicleUsageContext`)

- Truy vấn tối đa **50 phiếu gần nhất** theo `VehicleId` / biển số.
- Khách gần nhất, loại hàng gần nhất, loại hàng thường dùng (tie-break theo ngày).
- Thẻ **XE ĐÃ TỪNG CÂN** — không tự điền; xác nhận qua DÙNG CẢ HAI / CHỈ DÙNG KHÁCH / CHỈ DÙNG LOẠI HÀNG.
- `VehicleUsageContextApplier`, `FrequentCargoTypeResolver`, `WorkAreaLayoutCalculator` — unit-testable.

## Bố cục UI (Full HD)

- Khu cân **32%** · Thông tin phiếu **50%** · Camera **18%** (thu gọn → info **68%**, camera **0**).
- Bộ lọc nhanh (HÔM NAY …) + bộ lọc nâng cao (ẩn/hiện).
- DataGrid stretch; thanh tổng hợp cố định phía dưới.

## Giai đoạn 1.4 (vẫn áp dụng)

Phiếu một lần cân (Bì=0), khóa Cân lần 1, autocomplete nhanh, chip lọc.

## Cấu hình

`appsettings.json`: `DeviceMode`, `ShowDeveloperPanel`.

## Tài liệu

- [docs/PRODUCT_SPEC.md](docs/PRODUCT_SPEC.md)
- [docs/UI_SPEC.md](docs/UI_SPEC.md)

Chưa triển khai COM/RTSP/in/Excel thật.

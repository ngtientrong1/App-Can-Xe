# CanXe — Hướng dẫn cho Agent

## Tổng quan

**Giai đoạn hiện tại:** 1.4 — phiếu cân một lần (Bì=0), khóa Cân lần 1, autocomplete nhanh, bộ lọc hai hàng + chip.

## Lệnh

```bash
dotnet restore
dotnet build CanXe.sln
dotnet test CanXe.sln
build\publish-win10-x64.cmd
```

## Quy tắc phiếu một lần cân

- 1 WeighEvent → `Gross = Net = weight`, `Tare = 0`.
- 2 WeighEvents → `Max/Min/Abs` (thay thế kết quả Bì=0).
- `IsSingleWeigh` suy diễn trên detail DTO; **không** loại khỏi báo cáo.
- Danh sách **không** có cột Cân một lần / `SingleRecordedWeightKg`.

## Khóa Cân lần 1

- Sau khi có `DraftWeight2`: khóa cập nhật Cân lần 1 (`DraftWorkflowRules.CanUpdateWeight1`).
- DEV override: `DeviceMode=Simulation` + `ShowDeveloperPanel=true` + checkbox `DeveloperWeight1OverrideEnabled`.

## Autocomplete

- `FastEntrySearchService` + `AutocompleteRanker` + `AutoCompleteTextBox`.
- Debounce ~200 ms; tối đa 8 gợi ý; xếp hạng prefix/token; không dấu (kể cả Đ→D).
- Enter/Tab commit + chuyển focus; Escape đóng popup.

## Bộ lọc

- Hàng 1: nút nhanh thời gian + Từ/Đến ngày.
- Hàng 2: Khách, Loại hàng, Biển số, Số phiếu, Đơn giá (exact hoặc Từ–Đến).
- Chỉ query khi **ÁP DỤNG LỌC** hoặc Enter; chip xóa từng điều kiện; tổng NetWeight + TotalAmount theo kết quả lọc.

## Cấu hình

`appsettings.json`:

- `DeviceMode`: Simulation
- `ShowDeveloperPanel`: true/false — panel DEV mô phỏng

## Tài liệu

- [docs/PRODUCT_SPEC.md](docs/PRODUCT_SPEC.md)
- [docs/UI_SPEC.md](docs/UI_SPEC.md)

Chưa triển khai COM/RTSP/in/Excel thật.

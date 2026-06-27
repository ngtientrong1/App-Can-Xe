# CanXe — Database Schema

SQLite, EF Core. Trọng lượng lưu **INTEGER gram**, đơn giá **INTEGER VNĐ/kg**, thành tiền **INTEGER VNĐ**.

## Customers

| Cột | Kiểu | Nullable | Mô tả |
|-----|------|----------|-------|
| Id | INTEGER PK | No | |
| Name | TEXT | No | Tên hiển thị |
| NormalizedName | TEXT | No | Chuẩn hóa tìm kiếm |
| LastUsedAt | TEXT (DateTimeOffset) | Yes | |
| IsActive | INTEGER (bool) | No | |

Index: `NormalizedName`

## CargoTypes

| Cột | Kiểu | Nullable |
|-----|------|----------|
| Id | INTEGER PK | No |
| Name | TEXT | No |
| NormalizedName | TEXT | No |
| IsActive | INTEGER (bool) | No |

Index: `NormalizedName`

## Vehicles

| Cột | Kiểu | Nullable |
|-----|------|----------|
| Id | INTEGER PK | No |
| PlateNumber | TEXT | No |
| NormalizedPlateNumber | TEXT | No |
| LastCustomerId | INTEGER FK | Yes |
| LastUsedAt | TEXT | Yes |

Index: `NormalizedPlateNumber`

## WeighTickets

| Cột | Kiểu | Nullable | Mô tả |
|-----|------|----------|-------|
| Id | INTEGER PK | No | |
| SequenceNumber | INTEGER | No | Trong tháng |
| TicketYear | INTEGER | No | |
| TicketMonth | INTEGER | No | |
| InternalCode | TEXT | No | `202606-0059` UNIQUE |
| DisplayNumber | TEXT | No | `0059/06` |
| TicketDateTime | TEXT (DateTimeOffset) | No | |
| CustomerId | INTEGER FK | Yes | |
| CustomerNameSnapshot | TEXT | Yes | |
| VehicleId | INTEGER FK | Yes | |
| LicensePlateSnapshot | TEXT | Yes | |
| CargoTypeId | INTEGER FK | Yes | |
| CargoTypeNameSnapshot | TEXT | Yes | |
| UnitPriceVndPerKg | INTEGER | Yes | VNĐ/kg |
| Notes | TEXT | Yes | |
| GrossWeightGrams | INTEGER | Yes | |
| TareWeightGrams | INTEGER | Yes | |
| NetWeightGrams | INTEGER | Yes | |
| DeductionWeightGrams | INTEGER | Yes | gram, có precision |
| BillableWeightGrams | INTEGER | Yes | kg nguyên × 1000 |
| TotalAmountVnd | INTEGER | Yes | |
| CreatedAt | TEXT | No | |
| UpdatedAt | TEXT | Yes | |

Indexes:

- `TicketDateTime DESC`
- `InternalCode` UNIQUE
- `CustomerNameSnapshot`, `CargoTypeNameSnapshot`, `LicensePlateSnapshot`
- `(CargoTypeNameSnapshot, CustomerNameSnapshot, UnitPriceVndPerKg)` — báo cáo

## WeighEvents

| Cột | Kiểu | Nullable |
|-----|------|----------|
| Id | INTEGER PK | No |
| WeighTicketId | INTEGER FK | No |
| Sequence | INTEGER | No | 1 hoặc 2 |
| WeightGrams | INTEGER | No |
| RecordedAt | TEXT (DateTimeOffset) | No |
| PhotoPath | TEXT | Yes |
| PhotoCaptureSucceeded | INTEGER (bool) | No |
| PhotoErrorMessage | TEXT | Yes |
| RawScaleData | TEXT | Yes |

Unique: `(WeighTicketId, Sequence)`

## TicketSequences (sinh số phiếu)

| Cột | Kiểu |
|-----|------|
| Year | INTEGER PK part |
| Month | INTEGER PK part |
| LastSequence | INTEGER |

## Chuyển đổi Domain ↔ DB

```
kg → gram:  (int)Math.Round(kg * 1000m, MidpointRounding.AwayFromZero)
gram → kg:  grams / 1000m
```

## Ảnh camera

`%LocalAppData%\CanXe\Photos\{yyyy-MM-dd}\{InternalCode}_W{1|2}.png`

Giữ **3 ngày**; dọn khi khởi động. Xóa ảnh không xóa phiếu/event.

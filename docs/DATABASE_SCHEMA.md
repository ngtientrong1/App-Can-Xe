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
| UnitPriceVndPerKg | INTEGER | Yes | VNĐ/kg; NULL = cân dịch vụ |
| Notes | TEXT | Yes | |
| GrossWeightGrams | INTEGER | Yes | |
| TareWeightGrams | INTEGER | Yes | |
| NetWeightGrams | INTEGER | Yes | |
| DeductionWeightGrams | INTEGER | Yes | gram; NULL khi cân dịch vụ |
| BillableWeightGrams | INTEGER | Yes | kg nguyên × 1000; NULL khi cân dịch vụ |
| TotalAmountVnd | INTEGER | Yes | NULL khi cân dịch vụ |
| CreatedAt | TEXT | No | |
| UpdatedAt | TEXT | Yes | |

Indexes:

- `TicketDateTime DESC`
- `InternalCode` UNIQUE
- `VehicleId`
- `LicensePlateSnapshot`
- `CustomerId`
- `CargoTypeId`
- `CustomerNameSnapshot`, `CargoTypeNameSnapshot`, `LicensePlateSnapshot` (query filters)
- `(CargoTypeNameSnapshot, CustomerNameSnapshot, UnitPriceVndPerKg)` — báo cáo

## WeighEvents

| Cột | Kiểu | Nullable | Ghi chú |
|-----|------|----------|---------|
| Id | INTEGER PK | No | |
| WeighTicketId | INTEGER FK | No | |
| Sequence | INTEGER | No | 1 hoặc 2 |
| WeightGrams | INTEGER | No | **OriginalWeightGrams** (domain) |
| OverrideWeightGrams | INTEGER | Yes | Phase 1.6 |
| IsManualOverride | INTEGER (bool) | No | Phase 1.6 |
| OverrideReason | TEXT | Yes | Phase 1.6 |
| OverrideAt | TEXT (DateTimeOffset) | Yes | Phase 1.6 |
| OverrideBy | TEXT | Yes | Phase 1.6 |
| RecordedAt | TEXT (DateTimeOffset) | No | |
| PhotoPath | TEXT | Yes | |
| PhotoCaptureSucceeded | INTEGER (bool) | No | |
| PhotoErrorMessage | TEXT | Yes | |
| RawScaleData | TEXT | Yes | |

`EffectiveWeightGrams` (domain) = `OverrideWeightGrams ?? OriginalWeightGrams`. Ảnh gắn với giá trị cân gốc.

Unique: `(WeighTicketId, Sequence)`

## AuditLogs (Phase 1.6)

| Cột | Kiểu | Nullable |
|-----|------|----------|
| Id | INTEGER PK | No |
| TicketId | INTEGER FK | No |
| FieldName | TEXT | No |
| OldValue | TEXT | Yes |
| NewValue | TEXT | Yes |
| Reason | TEXT | Yes |
| EditedAt | TEXT (DateTimeOffset) | No |
| EditedBy | TEXT | Yes |
| IsDeveloperOverride | INTEGER (bool) | No |

Index: `TicketId`. Không xóa từ UI.

Migration: `202606270001_Phase16_AuditAndWeightOverride` — xem [DATABASE_MIGRATION.md](DATABASE_MIGRATION.md).

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

**Nháp:** `%LocalAppData%\CanXe\Photos\Draft\{sessionId}_W{1|2}.png`  
**Chính thức:** `%LocalAppData%\CanXe\Photos\{yyyy-MM-dd}\{InternalCode}_W{1|2}.png`

Giữ **PhotoRetentionDays** (mặc định 3). SQLite chỉ lưu đường dẫn + trạng thái.

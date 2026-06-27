# CanXe — Database Migration (Phase 1.6)

## Migration mới

| Phiên bản | Tên |
|-----------|-----|
| `202606270001_Phase16_AuditAndWeightOverride` | AuditLogs + override cân |

## Thay đổi schema

### WeighEvents (bổ sung cột)

- `OverrideWeightGrams` INTEGER NULL
- `IsManualOverride` INTEGER NOT NULL DEFAULT 0
- `OverrideReason` TEXT NULL
- `OverrideAt` TEXT NULL
- `OverrideBy` TEXT NULL

Cột `WeightGrams` hiện có map tới `OriginalWeightGrams` trong domain — **không đổi tên cột DB**.

### AuditLogs (bảng mới)

- `Id`, `TicketId`, `FieldName`, `OldValue`, `NewValue`, `Reason`, `EditedAt`, `EditedBy`, `IsDeveloperOverride`

## Cách nâng cấp an toàn

1. **Đóng ứng dụng CanXe** trước khi migration.
2. Ứng dụng tự **sao lưu** file SQLite vào thư mục `backups/` cạnh database (tên có timestamp) qua `DatabaseUpgrader`.
3. Khởi động lại app — `InitializeDatabaseAsync` chạy `DatabaseUpgrader` (idempotent, không xóa database).
4. Kiểm tra bảng `__EFMigrationsHistory` có dòng `202606270001_Phase16_AuditAndWeightOverride`.

## Khôi phục nếu lỗi

1. Đóng app.
2. Đổi tên file DB hiện tại (ví dụ `canxe.db.broken`).
3. Copy file backup mới nhất từ `backups/` về vị trí database gốc.
4. Báo lỗi kèm log để xử lý migration.

## Lưu ý

- **Không** dùng `EnsureCreated()` cho schema mới.
- Dữ liệu thử nghiệm hiện có được giữ nguyên; override cột mặc định NULL/false.
- Không xóa AuditLog từ giao diện người dùng.

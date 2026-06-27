# CanXe — Hướng dẫn cho Agent

## Tổng quan

**Giai đoạn hiện tại:** 1.6 MVP — chỉnh sửa phiếu inline, danh sách/summary, autocomplete, preview phiếu.

**Branch:** `phase1-6-mvp-finalization`

## Lệnh

```bash
dotnet restore
dotnet build CanXe.sln
dotnet test CanXe.sln
build\publish-win10-x64.cmd
```

## MVP 1.6 (chưa triển khai phần cứng)

- Double-click DataGrid → sửa phiếu trên form chính (`IsEditingExistingTicket`, `TicketUpdateService`, `AuditLogs`).
- Tự điền khách/loại hàng khi commit biển số (không còn thẻ XE ĐÃ TỪNG CÂN).
- Autocomplete dropdown nội bộ (không Popup top-level); đóng khi Alt+Tab.
- Summary: Số phiếu, Tổng hàng, KL tính tiền, Tổng thành tiền, Chưa có đơn giá.
- Sort danh sách: `TicketDateTime ↓` → `Sequence ↓` → `Id ↓`.
- Preview phiếu overlay: `ITicketDocumentRenderer`, sao chép/lưu PNG.
- Migration `202606270001_Phase16_AuditAndWeightOverride` — xem [docs/DATABASE_MIGRATION.md](docs/DATABASE_MIGRATION.md).

## Bố cục UI

- Header thiết bị gọn · Khu làm việc ~280–340px · DataGrid `*` · Footer cố định.
- Camera: 32/50/18 mở; 34/66/0 + cột CAM thu gọn khi đóng.

## Cấu hình

`appsettings.json`: `DeviceMode`, `ShowDeveloperPanel`, `DeveloperTicketEditEnabled`.

Chưa triển khai COM/RTSP/Brother print/Excel thật.

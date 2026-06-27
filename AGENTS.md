# CanXe — Hướng dẫn cho Agent

## Tổng quan

Ứng dụng cân xe Windows (WPF, .NET 10, MVVM, SQLite + EF Core). Phát triển theo giai đoạn; **Giai đoạn 1** = nền móng + UI mô phỏng.

## Cấu trúc solution

```
CanXe.sln
├── src/CanXe.Domain          — Entities, WeightCalculator, TextNormalizer
├── src/CanXe.Application     — Use cases, interfaces (ports), DTOs
├── src/CanXe.Infrastructure  — EF Core, SQLite, dịch vụ mô phỏng
├── src/CanXe.Desktop         — WPF Views + ViewModels
└── tests/CanXe.Tests         — xUnit
```

## Lệnh thường dùng

```bash
dotnet restore
dotnet build
dotnet test
dotnet run --project src/CanXe.Desktop/CanXe.Desktop.csproj
```

## Quy ước quan trọng

### Đơn vị & lưu trữ

- **Domain/Application**: `decimal` theo kg và VNĐ.
- **SQLite**: trọng lượng = `INTEGER` gram; đơn giá = `INTEGER` VNĐ/kg; thành tiền = `INTEGER` VNĐ.
- Chuyển đổi chỉ ở **Infrastructure** (`WeightStorageMapper`).

### Số phiếu

- Hiển thị: `0059/06` (số thứ tự/tháng).
- Mã nội bộ: `202606-0059`.
- Chạy liên tục theo tháng; người dùng **không sửa** ở Giai đoạn 1.

### WeighEvent

Mỗi lần bấm `GHI TRỌNG LƯỢNG` tạo một `WeighEvent`. Camera gọi sau, không rollback trọng lượng.

### Phiếu một trọng lượng

Lưu được với 1 event. Tiếp tục cân = cập nhật **cùng** `WeighTicket`, thêm event thứ 2 — không tạo phiếu mới.

### Chưa triển khai (Giai đoạn 1)

COM scale thật, RTSP, máy in, Excel export, phân quyền admin.

## Tài liệu

- [docs/PRODUCT_SPEC.md](docs/PRODUCT_SPEC.md)
- [docs/UI_SPEC.md](docs/UI_SPEC.md)
- [docs/DATABASE_SCHEMA.md](docs/DATABASE_SCHEMA.md)
- [docs/ROADMAP.md](docs/ROADMAP.md)

# CanXe — Hướng dẫn cho Agent

## Tổng quan

Ứng dụng cân xe Windows (WPF, .NET 10, MVVM, SQLite + EF Core).

**Giai đoạn hiện tại:** 1.1 — workflow draft/lưu đồng bộ đặc tả. Chưa triển khai COM/RTSP thật.

## Lệnh

```bash
dotnet restore
dotnet build CanXe.sln
dotnet test CanXe.sln
dotnet run --project src/CanXe.Desktop/CanXe.Desktop.csproj
build\publish-win10-x64.cmd
```

## Workflow cốt lõi (1.1)

### Draft-only trước LƯU

- `LẤY CÂN LẦN 1` / `LẤY CÂN LẦN 2` (hoặc `CẬP NHẬT CÂN LẦN n`) chỉ cập nhật **draft**.
- Không INSERT `WeighTicket` / `WeighEvent` cho đến khi bấm **LƯU**.
- Ảnh nháp: `%LocalAppData%\CanXe\Photos\Draft\{sessionId}_W{n}.png`.

### LƯU

- Sinh số phiếu, tạo DB, promote ảnh hợp lệ sang thư mục chính thức, transaction SQLite.

### Tiếp tục phiếu 1 cân

- Khóa cân đã lưu; chỉ lấy lần còn thiếu; UPDATE cùng phiếu.

## Cấu hình

`src/CanXe.Desktop/appsettings.json` — `DeviceMode: Simulation` mặc định.

## Tài liệu

- [docs/PRODUCT_SPEC.md](docs/PRODUCT_SPEC.md)
- [docs/UI_SPEC.md](docs/UI_SPEC.md)
- [docs/DATABASE_SCHEMA.md](docs/DATABASE_SCHEMA.md)
- [docs/ROADMAP.md](docs/ROADMAP.md)
- [docs/LOW_END_PC_TEST_PLAN.md](docs/LOW_END_PC_TEST_PLAN.md)
- [docs/SECURITY_NOTES.md](docs/SECURITY_NOTES.md)

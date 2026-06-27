# CanXe — Hướng dẫn cho Agent

## Tổng quan

Ứng dụng cân xe Windows (WPF, .NET 10, MVVM, SQLite + EF Core).

**Giai đoạn hiện tại:** 1.2 — hoàn thiện giao diện MVP và quy tắc nghiệp vụ cân dịch vụ. Chưa triển khai COM/RTSP/in/Excel thật.

## Lệnh

```bash
dotnet restore
dotnet build CanXe.sln
dotnet test CanXe.sln
dotnet run --project src/CanXe.Desktop/CanXe.Desktop.csproj
build\publish-win10-x64.cmd
```

## Workflow cốt lõi

### Draft-only trước LƯU

- `LẤY CÂN LẦN 1` / `LẤY CÂN LẦN 2` (hoặc `CẬP NHẬT CÂN LẦN n`) chỉ cập nhật **draft**.
- Không INSERT `WeighTicket` / `WeighEvent` cho đến khi bấm **LƯU**.
- Ảnh nháp: `%LocalAppData%\CanXe\Photos\Draft\{sessionId}_W{n}.png`.

### Cân dịch vụ

- Không đơn giá hoặc đơn giá ≤ 0 → Tổng/Bì/Hàng vẫn tính (2 cân); trừ bì/KL TT/thành tiền = **NULL** trong DB và `—` trên UI.
- Chỉ tính billing khi `UnitPrice > 0`.

### LƯU

- Sinh số phiếu, tạo DB, promote ảnh hợp lệ sang thư mục chính thức, transaction SQLite.

### Tiếp tục phiếu 1 cân

- Cửa sổ **Chi tiết phiếu** → **TIẾP TỤC CÂN**. Khóa cân đã lưu; UPDATE cùng phiếu.

## UI chính

- `MainWindow`: layout 27/53/20, autocomplete khách/loại hàng, khối KẾT QUẢ CÂN, danh sách 12 cột (không W1/W2).
- `TicketDetailWindow`: W1/W2, ảnh, kết quả đầy đủ.

## Cấu hình

`src/CanXe.Desktop/appsettings.json` — `DeviceMode: Simulation` mặc định.

## Tài liệu

- [docs/PRODUCT_SPEC.md](docs/PRODUCT_SPEC.md)
- [docs/UI_SPEC.md](docs/UI_SPEC.md)
- [docs/DATABASE_SCHEMA.md](docs/DATABASE_SCHEMA.md)
- [docs/ROADMAP.md](docs/ROADMAP.md)
- [docs/LOW_END_PC_TEST_PLAN.md](docs/LOW_END_PC_TEST_PLAN.md)
- [docs/SECURITY_NOTES.md](docs/SECURITY_NOTES.md)

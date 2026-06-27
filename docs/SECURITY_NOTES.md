# Security Notes — CanXe

## NU1903 — SQLitePCLRaw.lib.e_sqlite3

### Trạng thái

`dotnet list package --vulnerable --include-transitive` có thể báo **NU1903** trên dependency transitive `SQLitePCLRaw.lib.e_sqlite3` (qua `Microsoft.EntityFrameworkCore.Sqlite`).

### Package gây ra

| Package | Phiên bản hiện tại | Nguồn |
|---------|-------------------|--------|
| Microsoft.EntityFrameworkCore.Sqlite | 10.0.x | CanXe.Infrastructure, CanXe.Tests |
| SQLitePCLRaw.lib.e_sqlite3 | 2.1.11 (transitive) | EF Core SQLite provider |

### Hành động đã thử

- Không suppress cảnh báo trong `.csproj`.
- Không nâng major EF Core tùy tiện (đang dùng .NET 10 / EF 10).
- Không thêm direct reference `SQLitePCLRaw` với version khác vì có thể xung đột native binary với EF.

### Phương án

1. Theo dõi bản vá từ [Microsoft.EntityFrameworkCore.Sqlite](https://www.nuget.org/packages/Microsoft.EntityFrameworkCore.Sqlite) và [SQLitePCLRaw](https://www.nuget.org/profiles/ericsink).
2. Khi có bản EF Core 10.x patch kéo `SQLitePCLRaw` đã vá, nâng patch version (không đổi major).
3. Ứng dụng desktop offline: bề mặt tấn công hạn chế (không lắng nghe mạng cho SQLite).

### Ghi chú vận hành

- Chỉ mở file `.db` và ảnh từ thư mục `%LocalAppData%\CanXe\`.
- Không mở database từ nguồn không tin cậy.

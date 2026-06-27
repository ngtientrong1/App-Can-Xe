# Kế hoạch kiểm thử PC cấu hình yếu

## Môi trường mục tiêu

| Thành phần | Yêu cầu |
|------------|---------|
| OS | Windows 10 22H2 x64 |
| CPU | 2 nhân |
| RAM | 4 GB |
| Màn hình | 1366×768 |
| Ổ đĩa | HDD hoặc SSD cũ |

## Cách triển khai

1. Chạy `build\publish-win10-x64.cmd`.
2. Copy thư mục `publish\win10-x64` sang PC thử nghiệm.
3. Chạy `CanXe.Desktop.exe` — **không cần** cài .NET Runtime (self-contained).

## Checklist chức năng (Simulation)

- [ ] Khởi động < 10 giây trên HDD
- [ ] Mặc định `Nguồn: Tự động mô phỏng` (không manual sau restart)
- [ ] Trọng lượng trực tiếp cập nhật mượt
- [ ] DEV: chuyển manual → Áp dụng 8.500 → `DEV thủ công`
- [ ] DEV: `TRỞ VỀ TỰ ĐỘNG` resume simulation, draft không mất
- [ ] Đóng DEV drawer không đổi mode
- [ ] Lấy cân lần 1 / lần 2 / cập nhật trước khi lưu
- [ ] Lưu phiếu 1 và 2 trọng lượng
- [ ] Tiếp tục phiếu 1 trọng lượng
- [ ] Hủy bỏ xóa draft
- [ ] Danh sách scroll + lọc hôm nay / 7 ngày
- [ ] Camera drawer thu gọn — form mở rộng, không cột trắng
- [ ] Utility rail CAMERA/DEV đủ lớn (≥52px)
- [ ] Footer 5 card summary hiển thị đúng
- [ ] Compact 1366×768: action bar đủ nút, font cân ≥68px
- [ ] Không cần Internet

## Checklist hiệu năng

- [ ] DataGrid 200 dòng không giật khi scroll
- [ ] Lọc SQLite không treo UI > 1 giây
- [ ] Không tăng RAM > ~300 MB sau 30 phút dùng liên tục

## Không kiểm tra ở giai đoạn này

- COM scale thật
- RTSP camera thật
- In phiếu / Excel

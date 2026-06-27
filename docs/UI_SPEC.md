# CanXe — UI Spec (Giai đoạn 1)

## Khung màn hình

- Tỷ lệ **16:9**, responsive trên 1366×768, 1600×900, 1920×1080.
- **Nửa trên**: trọng lượng trực tiếp (trái) · form phiếu (giữa) · camera mô phỏng (phải, ≤20% rộng).
- **Giữa**: nút `GHI TRỌNG LƯỢNG` lớn; `LƯU` · `HỦY BỎ`.
- **Nửa dưới**: bộ lọc + DataGrid 14 cột.

## Trường nhập (Giai đoạn 1)

| Trường | Ghi chú |
|--------|---------|
| Số phiếu | Chỉ đọc, tự sinh khi lưu |
| Ngày giờ | Chỉ đọc, tự động |
| Tên khách hàng | Autocomplete gợi ý |
| Biển số xe | Text |
| Loại hàng | Autocomplete gợi ý |
| Đơn giá | VNĐ/kg, nguyên |
| Ghi chú | Text |

## Nút thao tác

### GHI TRỌNG LƯỢNG

- Lần 1 → Weight1 + thời gian + WeighEvent + camera async.
- Lần 2 → Weight2 tương tự.
- Đủ 2 lần → **Disabled** (không ghi đè W2).

### LƯU

Lưu linh hoạt (1 hoặc 2 trọng lượng, thiếu đơn giá OK).

### HỦY BỎ

Clear form, không confirm.

## DataGrid (14 cột)

Ngày giờ · Số phiếu · Biển số · Khách hàng · Loại hàng · Lần ghi đầu · Lần ghi sau · Tổng · Bì · Hàng · KL tính tiền · Đơn giá · Thành tiền · Ghi chú

Double-click dòng phiếu **chưa đủ 2 cân** → load tiếp tục.

## Bộ lọc

Từ ngày · Đến ngày · Khách hàng · Loại hàng · Biển số · Số phiếu · Đơn giá · **Hôm nay** · **Xóa lọc**

## Dev — mô phỏng cân

Panel dev (có thể thu gọn): chế độ Random / Manual, preset 8.500 kg và 18.500 kg.

## Camera

- Preview nhỏ góc phải.
- Lỗi: toast/status bar, không popup.

## Format hiển thị

- Trọng lượng: `N0` kg (Deduction có thể `N3`).
- Tiền: `N0` VNĐ.

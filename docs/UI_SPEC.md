# CanXe — UI Spec (Giai đoạn 1.4)

## Bố cục 16:9

| Cột | Tỷ lệ | Nội dung |
|-----|-------|----------|
| Trái | 34% | Trọng lượng trực tiếp + kết quả cân |
| Giữa | 46% | Form phiếu |
| Phải | 20% | Camera + DEV (thu gọn mặc định) |

## Cột trái — nút cân

- Trước Cân lần 2: **LẤY CÂN LẦN 1** / **CẬP NHẬT CÂN LẦN 1**.
- Sau Cân lần 2: **CÂN LẦN 1 ĐÃ KHÓA** (disabled).
- Cân lần 2 vẫn cập nhật được trước LƯU.
- DEV: checkbox **Cho phép sửa lại Cân lần 1** + cảnh báo nhỏ.

## Autocomplete

Popup tối đa 8 dòng; highlight dòng đầu; phụ đề (xe gần nhất, khách gần nhất, số lần dùng).

Điều khiển: ↑↓ chọn · Enter/Tab commit + focus kế tiếp · Escape đóng · click chọn.

Ghi chú: popup khi gõ (tối đa 5 gợi ý); không auto-fill khi focus trống.

Biển số: banner **Xe này thường thuộc …** + **DÙNG KHÁCH NÀY**.

## DataGrid (12 cột)

Ngày giờ · Số phiếu · Biển số · Khách · Loại hàng · Tổng · Bì · Hàng · KL tính tiền · Đơn giá · Thành tiền · Ghi chú.

**Không** có cột Cân một lần. Phiếu 1 event hiển thị Tổng/Bì(0)/Hàng như cân đủ hai lần.

## Bộ lọc

```
[Hôm nay] [Hôm qua] [7 ngày] [Tháng này]  Từ: ___  Đến: ___
Khách: ___  Loại hàng: ___  Biển số: ___  Số phiếu: ___  Giá: ___ – ___
[ÁP DỤNG LỌC] [XÓA LỌC] [XUẤT EXCEL]
Chip: Hôm nay ×  Khách hàng: Chị Đức ×  ...
Đang hiển thị: 24 phiếu · Tổng trọng lượng hàng: … · Tổng thành tiền: …
```

Enter trong ô lọc → ÁP DỤNG. XUẤT EXCEL disabled (stub).

## TabIndex

1–5: form · 6–7: cân · 8–10: Lưu/Hủy/In · Filter/DataGrid/DEV: `-1`.

## Panel DEV

Chỉ khi `DeviceMode = Simulation` và `ShowDeveloperPanel = true`.

## Chi tiết phiếu

Double-click danh sách; hiển thị Weight1/Weight2; **TIẾP TỤC CÂN** cho phiếu một lần.

# CanXe — UI Spec (Giai đoạn 1.3)

## Bố cục 16:9

| Cột | Tỷ lệ | Nội dung |
|-----|-------|----------|
| Trái | 34% | Trọng lượng trực tiếp + kết quả cân |
| Giữa | 46% | Form phiếu (không có kết quả cân) |
| Phải | 20% | Camera + DEV (thu gọn mặc định) |

Camera thu gọn → cột giữa mở rộng 66%.

## Cột trái

```
TRỌNG LƯỢNG TRỰC TIẾP
11.247 kg
● ỔN ĐỊNH

CÂN LẦN 1    CÂN LẦN 2
...

TỔNG   BÌ   HÀNG
Trừ bì / KL tính tiền / Thành tiền
```

Không scroll dọc trên 1920×1080.

## Số phiếu

Giá trị lớn (ví dụ `0009/06`) + nhãn nhỏ **Số dự kiến** khi chưa lưu.

## Autocomplete

Keyboard đầy đủ; highlight dòng chọn; Enter/Tab commit; Escape đóng popup.

## TabIndex

1–5: form · 6–7: cân · 8–10: Lưu/Hủy/In · Filter/DataGrid/DEV: `-1`.

## DataGrid (13 cột)

Ngày giờ · Số phiếu · Biển số · Khách · Loại hàng · **Cân một lần** · Tổng · Bì · Hàng · KL tính tiền · Đơn giá · Thành tiền · Ghi chú.

## Panel DEV

Chỉ khi `DeviceMode = Simulation` và `ShowDeveloperPanel = true`. Nút **MỞ MÔ PHỎNG**; thu gọn mặc định; không tham gia Tab.

## Chi tiết phiếu

Double-click danh sách; **TIẾP TỤC CÂN** cho phiếu một lần.

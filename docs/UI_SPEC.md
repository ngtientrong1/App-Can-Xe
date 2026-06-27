# CanXe — UI Spec (Giai đoạn 1.5)

## Bố cục màn hình

```text
Tiêu đề + trạng thái
────────────────────
Cân 32% | Phiếu 50% | Camera 18%
────────────────────
[LẤY CÂN 1] [LẤY CÂN 2] [LƯU] [HỦY] [IN]
────────────────────
Lọc nhanh + [ẨN/MỞ BỘ LỌC NÂNG CAO]
(Điều kiện nâng cao khi mở)
Chip lọc (nếu có)
────────────────────
DataGrid — chiếm phần cao còn lại
────────────────────
Số phiếu | Tổng hàng | Tổng thành tiền
```

Camera thu gọn: cột 0*, phiếu 68*, nút MỞ CAMERA trên hàng nút.

## Thẻ XE ĐÃ TỪNG CÂN

Xuất hiện dưới hàng Khách/Biển số khi có lịch sử. Không chiếm chỗ khi ẩn.

```text
XE ĐÃ TỪNG CÂN
Khách gần nhất: …
Loại hàng thường dùng: …
Lần gần nhất: dd/MM/yyyy
(cảnh báo thay đổi nếu đã nhập khác)

[DÙNG CẢ HAI] [CHỈ DÙNG KHÁCH] [CHỈ DÙNG LOẠI HÀNG]
```

DÙNG CẢ HAI nổi bật khi cả hai trường trống.

## Khu cân (trái)

Trọng lượng trực tiếp lớn nhất; Tổng/Bì/Hàng một hàng; Hàng nhấn mạnh; billing gọn.

## Form phiếu

Một hàng: `Số phiếu: 0018/06` · ngày giờ bên phải.

Hai cột: Khách | Biển số · Loại hàng | Đơn giá · Ghi chú full width.

Ô nhập ~46 px; nhãn 17 px; nội dung 19 px.

## Camera & DEV

Preview 16:9 (Viewbox); THU GỌN. DEV drawer thu gọn, MỞ MÔ PHỎNG, không Tab.

## Nút thao tác

Cao ~58 px; cân cam rộng ~220 px; Lưu xanh lá; Hủy xám, margin rộng hơn Lưu.

## DataGrid

Header/row ~42 px; font 16 px; cột số/tiền căn phải; khách/loại hàng rộng hơn; virtualization bật.

## Phong cách

Segoe UI, nền sáng, viền nhẹ, bo góc 4–6 px, không blur/gradient mạnh.

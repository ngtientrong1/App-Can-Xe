# CanXe — UI Spec (Giai đoạn 1.6 MVP)

## Grid chính

```text
Row 0: Header thiết bị — Auto (~42–48px)
Row 1: Khu làm việc — Auto Max 340px
Row 2: Thanh nút — Auto
Row 3: Bộ lọc — Auto (nâng cao mặc định ẩn)
Row 4: DataGrid — *
Row 5: Summary — Auto
```

## Header

`CanXe` · `● Đầu cân: Ổn định` · `● Camera: Đã kết nối` · ngày giờ.

## Khu làm việc

Camera mở: 32/50/18. Thu gọn: 34/66 + cột CAM ~40px.

Trọng lượng trực tiếp ~68px. Form MaxWidth: Customer 420, Plate 280, Cargo 340, UnitPrice 230.

## Nút tạo phiếu

`LẤY CÂN 1/2` · `LƯU` · `HỦY` · `XEM PHIẾU` · `IN PHIẾU`

## Nút sửa phiếu

`CẬP NHẬT PHIẾU` · `THOÁT CHỈNH SỬA` · `XEM PHIẾU` · `IN PHIẾU`

## Summary footer

```text
Số phiếu | Tổng hàng | KL tính tiền | Tổng thành tiền | Chưa có đơn giá: N phiếu
```

## Preview overlay

`XEM TRƯỚC PHIẾU` · tab MẶT TRƯỚC/SAU · SAO CHÉP ẢNH · LƯU ẢNH · IN · ĐÓNG · Escape.

## Toast

Tự điền biển số: `Đã tự điền khách hàng và loại hàng theo lịch sử xe`

## DataGrid

Row/header 46px, font 16–17px, virtualization, highlight dòng vừa lưu/sửa ~1,5s.

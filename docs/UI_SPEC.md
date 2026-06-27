# CanXe — UI Spec (Giai đoạn 1.1)

## Thanh nút (MinHeight 56px)

```
[LẤY CÂN LẦN 1] [LẤY CÂN LẦN 2] [LƯU] [HỦY BỎ] [IN PHIẾU]
```

- Nút cân đổi nhãn **CẬP NHẬT CÂN LẦN n** sau lần đầu.
- Tiếp tục phiếu: khóa nút cân đã lưu, nhãn `(ĐÃ LƯU)`.
- **IN PHIẾU**: stub giai đoạn sau.

## Số phiếu

Trước lưu: `Tự động khi lưu`. Sau lưu / tiếp tục: hiển thị `0059/06`.

## Khu kết quả

Cân lần 1+giờ · Cân lần 2+giờ · Tổng/Bì/Hàng · Trừ bì · KL TT · Thành tiền. Thiếu → `—`.

## Camera

Góc phải ≤20% rộng, có **Thu gọn**. Dev: manual 8.500 / 18.500 kg.

## Danh sách (nửa dưới)

14 cột, virtualization, double-click tiếp tục phiếu 1 cân.

## Bộ lọc

Từ/Đến ngày · Khách · Loại hàng · Biển số · Số phiếu · Đơn giá · **Hôm nay** · **Hôm qua** · **7 ngày** · **Tháng này** · Xóa lọc.

Mặc định mở app: **Hôm nay**. Tối đa **200** bản ghi.

## Sau LƯU / HỦY

Focus về ô **Khách hàng**. Trọng lượng trực tiếp vẫn chạy.

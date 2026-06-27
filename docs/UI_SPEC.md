# CanXe — UI Spec (Giai đoạn 1.2)

## Cửa sổ chính

- Mở **Maximized** mặc định, tận dụng chiều rộng 16:9.
- Bố cục cột: trọng lượng trực tiếp ~27% · thông tin phiếu + kết quả ~53% · camera ~20%.
- Camera thu gọn → cột thông tin phiếu mở rộng, không để cột trống.
- Grid star sizing; không Width cố định cho panel.

## Font (Segoe UI, người lớn tuổi)

| Thành phần | Kích thước |
|---|---|
| Trọng lượng trực tiếp | 72 px (Full HD) |
| Nhãn trường | 17–18 px |
| Ô nhập | 19–22 px |
| Nút thao tác | 20–22 px, MinHeight 58 px |
| DataGrid | 16–18 px, dòng ≥42 px, header ≥44 px |

## Thanh nút

```
[LẤY CÂN LẦN 1] [LẤY CÂN LẦN 2] [LƯU] [HỦY BỎ] [IN PHIẾU]
```

- Nút cân đổi nhãn **CẬP NHẬT CÂN LẦN n** sau lần đầu.
- Tiếp tục phiếu: khóa nút cân đã lưu, nhãn `(ĐÃ LƯU)`.
- **IN PHIẾU**: stub giai đoạn sau.

## Khu cân lần 1 / 2

Chưa lấy cân:

```
CÂN LẦN 1
Chưa lấy cân
```

Đã lấy cân:

```
CÂN LẦN 1
8.500 kg
08:35:20
```

Không dùng `@`. Thời gian dòng riêng, chữ nhỏ hơn.

## Khu KẾT QUẢ CÂN

Ba thẻ lớn: **TỔNG · BÌ · HÀNG** (HÀNG nhấn mạnh).

Bên dưới: Trừ bì 3/1.000 · Khối lượng tính tiền · Thành tiền.

Cân dịch vụ: các dòng tính tiền = `—`, ghi chú nhỏ *Cân dịch vụ — chưa nhập đơn giá*.

## Autocomplete

Một control duy nhất cho **Khách hàng** và **Loại hàng**. Popup khi gõ/focus; không ListBox cố định bên dưới.

## Camera

Góc phải ~20% rộng, có **Thu gọn** / **MỞ CAMERA**. Dev: manual 8.500 / 18.500 kg.

## Danh sách (nửa dưới)

12 cột: Ngày giờ · Số phiếu · Biển số · Khách · Loại hàng · Tổng · Bì · Hàng · KL tính tiền · Đơn giá · Thành tiền · Ghi chú.

Virtualization, header cố định, scroll ngang, dòng mới nhất trên cùng. Double-click → **Chi tiết phiếu**.

## Bộ lọc

Nhãn rõ: Từ ngày · Đến ngày · Khách hàng · Loại hàng · Biển số · Số phiếu · Đơn giá.

Nút: **LỌC · HÔM NAY · HÔM QUA · 7 NGÀY · THÁNG NÀY · XÓA LỌC**.

Mặc định mở app: **Hôm nay**. Tối đa **200** bản ghi. Lọc đơn giá không trả phiếu cân dịch vụ (null).

## Chi tiết phiếu

Weight1/2 + thời gian + ảnh · Tổng/Bì/Hàng · trừ bì/KL TT/thành tiền (hoặc *Cân dịch vụ*) · khách/biển/loại/ghi chú · **TIẾP TỤC CÂN** nếu chỉ 1 cân.

## Sau LƯU / HỦY

Focus về ô **Khách hàng**. Trọng lượng trực tiếp vẫn chạy.

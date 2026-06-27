# CanXe — Product Spec (Giai đoạn 1.3)

## Bố cục cân

Cột trái (~34%): trọng lượng trực tiếp, ổn định, Cân lần 1/2, Tổng/Bì/Hàng, trừ bì/KL TT/thành tiền.

Cột giữa (~46%): chỉ thông tin phiếu (số phiếu dự kiến, ngày giờ, khách, biển số, loại hàng, đơn giá, ghi chú).

Cột phải (~20%): camera nhỏ + panel DEV thu gọn (Simulation + `ShowDeveloperPanel`).

## Số phiếu dự kiến

- Hiển thị số dự kiến (ví dụ `0009/06`) trước khi lưu — **không** tăng sequence.
- Số chính thức cấp trong transaction khi **LƯU**.
- **HỦY BỎ** không tăng sequence.
- Sau lưu: hiển thị số dự kiến tiếp theo.

## Autocomplete

Khách hàng, biển số, loại hàng — một control duy nhất mỗi trường:

- ArrowUp/Down, Enter, Tab, Escape, click chọn.
- Tab không có selection → giữ text người dùng gõ.
- Biển số: tìm không phân biệt dấu chấm/gạch, tự chữ hoa.

## Danh sách — cột Cân một lần

- 1 WeighEvent → hiển thị trọng lượng.
- 2 events → `—`.
- Không ảnh hưởng công thức Tổng/Bì/Hàng.

## Công thức & cân dịch vụ

Giữ nguyên quy tắc Giai đoạn 1.2.

## Tab order

Khách → Biển số → Loại hàng → Đơn giá → Ghi chú → Cân 1 → Cân 2 → Lưu → Hủy → In.

Panel DEV: `IsTabStop = false`.

## Ngoài phạm vi 1.3

COM, RTSP, in, Excel, phân quyền admin.

# CanXe — Product Spec (Giai đoạn 1.4)

## Phiếu cân một lần

Khi phiếu chỉ có **một** WeighEvent:

```text
GrossWeight = trọng lượng duy nhất
TareWeight = 0
NetWeight = trọng lượng duy nhất
```

Ví dụ: trọng lượng 11.576 kg → Tổng 11.576 · Bì 0 · Hàng 11.576.

Khi bổ sung cân lần 2 trên cùng phiếu:

```text
Gross = Max(W1, W2)
Tare = Min(W1, W2)
Net = Abs(W1 - W2)
```

Không cấp số phiếu mới. Phiếu một lần cân vẫn cộng vào báo cáo (Loại hàng → Khách → Đơn giá).

## Billing

- `UnitPrice > 0`: deduction, billable, total như Giai đoạn 1.2.
- Không đơn giá: Gross/Tare/Net có giá trị; billing null; UI hiển thị `—`.

## Khóa Cân lần 1

- Trước Cân lần 2: có thể lấy/cập nhật Cân lần 1 nhiều lần.
- Sau Cân lần 2: khóa Cân lần 1; Cân lần 2 vẫn cập nhật được trước LƯU.
- DEV (`Simulation` + `ShowDeveloperPanel`): checkbox **Cho phép sửa lại Cân lần 1** (mặc định tắt).

## Autocomplete nhập nhanh

Áp dụng: Khách hàng, Biển số, Loại hàng, Ghi chú gần đây (không cho Đơn giá).

- Tối đa 8 gợi ý; debounce 150–250 ms.
- Không phân biệt hoa/thường; bỏ dấu (Đ→D); biển số bỏ dấu chấm/gạch khi so khớp.
- Xếp hạng: khớp chính xác → starts-with → token prefix → contains → dùng gần đây.
- Enter/Tab chọn + chuyển trường kế tiếp; Escape đóng popup.
- Dòng **+ Dùng tên mới** giữ text nhập; tạo danh mục khi LƯU.

Biển số: tự chữ hoa; gợi ý khách thường gắn xe (xác nhận **DÙNG KHÁCH NÀY**).

## Bộ lọc

**Hàng 1 — Thời gian:** Hôm nay, Hôm qua, 7 ngày, Tháng này, Từ ngày, Đến ngày.

**Hàng 2 — Điều kiện:** Khách, Loại hàng, Biển số, Số phiếu, Đơn giá (exact hoặc Từ/Đến).

- Nút: ÁP DỤNG LỌC, XÓA LỌC, XUẤT EXCEL (stub/disabled).
- Chip điều kiện đang hoạt động (× xóa từng chip).
- Tổng: số phiếu, tổng NetWeight, tổng TotalAmount (cộng từng phiếu).
- Khoảng ngày: `FromDate 00:00:00` đến trước `ToDate + 1 ngày`.
- Query SQLite chỉ khi ÁP DỤNG hoặc Enter; danh sách tối đa 200 dòng.

## Bố cục & số phiếu

Giữ bố cục 34/46/20 và số phiếu dự kiến (Peek, không increment) từ Giai đoạn 1.3.

## Tab order

Khách → Biển số → Loại hàng → Đơn giá → Ghi chú → Cân 1 → Cân 2 → Lưu.

## Ngoài phạm vi 1.4

COM, RTSP, in, Excel thật, phân quyền admin.

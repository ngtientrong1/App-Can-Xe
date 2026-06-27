# CanXe — Product Spec (Giai đoạn 1.5)

## Ngữ cảnh xe từ lịch sử cân

Khi chọn biển số đã có trong hệ thống, truy vấn lịch sử WeighTickets (tối đa 50 phiếu gần nhất):

| Trường | Nguồn |
|--------|--------|
| Khách gần nhất | Phiếu mới nhất có khách (fallback `Vehicle.LastCustomer` nếu phiếu không có) |
| Loại hàng gần nhất | Phiếu mới nhất có loại hàng |
| Loại hàng thường dùng | Nhiều lượt nhất; hòa → dùng gần đây hơn |
| Bỏ qua | CargoType null / tên trống |

Không suy diễn loại hàng từ ghi chú.

Thẻ **XE ĐÃ TỪNG CÂN** — ba nút xác nhận; không ghi đè âm thầm. Enter trên thẻ (khi cả hai trường trống) → DÙNG CẢ HAI.

Autocomplete biển số: dòng phụ khách gần nhất + loại hàng thường dùng.

## Index (không migration mới)

Bổ sung EF index: `VehicleId`, `LicensePlateSnapshot`, `CustomerId`, `CargoTypeId` trên WeighTickets.

## Bố cục 16:9

- Maximized; hỗ trợ 1366×768, 1600×900, 1920×1080.
- Không scroll dọc toàn màn hình Full HD.
- Tỷ lệ trên: cân 32% · phiếu 50% · camera 18%.
- Camera thu gọn → phiếu mở rộng, không để cột trống.

## Bộ lọc

**Lọc nhanh:** HÔM NAY, HÔM QUA, 7 NGÀY, THÁNG NÀY.

**Nâng cao (ẩn/hiện):** Từ/Đến ngày, Khách, Loại hàng, Biển số, Số phiếu, Từ giá/Đến giá, ÁP DỤNG / XÓA / XUẤT EXCEL (stub).

Chip chỉ khi có lọc thực sự. Tổng hợp ở thanh cuối: Số phiếu, Tổng hàng, Tổng thành tiền.

## Quy tắc nghiệp vụ (giữ từ 1.4)

Phiếu một lần cân, khóa Cân lần 1, autocomplete, billing.

## Ngoài phạm vi 1.5

COM, RTSP, in, Excel thật.

# CanXe — Product Spec (Giai đoạn 1.6 MVP)

## Danh sách và summary

Sort mặc định: `TicketDateTime DESC` → `Sequence DESC` → `Id DESC`.

Summary trên bộ lọc hiện tại:

- Số phiếu
- Tổng hàng (mọi phiếu có NetWeight, kể cả cân dịch vụ)
- KL tính tiền (chỉ phiếu có BillableWeight)
- Tổng thành tiền
- Số phiếu chưa có đơn giá

Sau lưu mới: refresh, highlight dòng đầu nếu khớp lọc. Sau sửa: giữ vị trí sort, scroll + highlight.

## Tự điền theo biển số

Khi commit biển số (Enter/Tab/click suggestion): tự điền khách gần nhất và loại hàng thường dùng. Toast nhỏ tự ẩn. Không còn thẻ xác nhận thủ công.

## Autocomplete

Dropdown trong visual tree MainWindow. Debounce ~200ms. Ranking: exact → starts-with → token → contains → recent.

## Chỉnh sửa phiếu

Double-click → form chính. DEV mở khóa sửa trọng lượng + lý do + audit. Không đổi số phiếu/ngày gốc.

## Xem trước phiếu

Overlay nội bộ: mặt trước/sau, sao chép ảnh ghép, lưu PNG. `ITicketDocumentRenderer`.

## Migration

`202606270001_Phase16_AuditAndWeightOverride`

## Ngoài phạm vi

COM, RTSP, Brother print, Excel thật.

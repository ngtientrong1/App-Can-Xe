# CanXe — Roadmap

## Giai đoạn 1 ✅ (đang triển khai)

- Solution 5 projects (.NET 10)
- SQLite schema đầy đủ (Customers, CargoTypes, Vehicles, WeighTickets, WeighEvents)
- UI WPF mô phỏng layout cân cũ
- Công thức + lưu linh hoạt + tiếp tục phiếu 1 cân
- SimulatedScale (random + manual dev)
- SimulatedCamera + dọn ảnh 3 ngày
- xUnit cho calculator và use cases

## Giai đoạn 2 — Phần cứng cân

- COM scale thật (`IScaleService` implementation)
- `RawScaleData` trên WeighEvent
- Cấu hình cổng COM

## Giai đoạn 3 — Camera RTSP

- RTSP stream + chụp ảnh thật
- Preview live

## Giai đoạn 4 — In & Xuất

- Máy in phiếu
- Xuất Excel báo cáo nhóm Loại hàng → Khách hàng → Đơn giá

## Giai đoạn 5 — Quản trị

- Phân quyền admin
- Ghi lại trọng lượng có kiểm soát (thay thế khóa cứng sau 2 lần ghi)
- Sửa/xóa phiếu có audit

## Giai đoạn 6 — Báo cáo

- Báo cáo theo nhóm, không gộp đơn giá khác nhau
- Tổng thành tiền = SUM(TotalAmountVnd) từng phiếu

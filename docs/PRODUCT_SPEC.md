# CanXe — Product Spec (Giai đoạn 1.1)

## Luồng lấy cân

Hai nút độc lập:

- **LẤY CÂN LẦN 1** → `DraftWeight1` (+ thời gian, ảnh nháp). Lần sau: **CẬP NHẬT CÂN LẦN 1**.
- **LẤY CÂN LẦN 2** → tương tự. Không khóa sau khi đủ 2 lần — có thể cập nhật trước khi LƯU.

Tổng/Bì/Hàng chỉ tính khi có đủ 2 giá trị draft.

## Draft vs dữ liệu chính thức

Trước **LƯU**: không phiếu DB, không danh sách, không số phiếu, không báo cáo.

## Ảnh

- Mỗi lần cân → chụp mới. Cập nhật thành công → xóa ảnh nháp cũ.
- Camera lỗi → giữ kg mới, ảnh cũ **không** hợp lệ; toast/status bar.
- **LƯU** → promote ảnh hợp lệ sang thư mục chính thức.

## LƯU linh hoạt

Tối thiểu: ≥ 1 trọng lượng. Các field khác nullable.

## Tiếp tục phiếu 1 cân

Double-click / tiếp tục → UPDATE phiếu cũ, không số phiếu mới. Không sửa cân đã lưu (admin sau này).

## Công thức

```
GrossWeight = Max(W1,W2)
TareWeight = Min(W1,W2)
NetWeight = Abs(W1-W2)
DeductionWeight = NetWeight/1000×3
BillableWeight = Round(Net-Deduction, 0, AwayFromZero)
TotalAmount = BillableWeight × UnitPrice (khi có đơn giá)
```

## DeviceMode

`Simulation` (mặc định) — random/manual cân, camera mô phỏng. `Hardware` — giai đoạn sau.

## Ngoài phạm vi 1.1

COM, RTSP, in, Excel, phân quyền admin.

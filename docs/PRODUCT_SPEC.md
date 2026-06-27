# CanXe — Product Spec (Giai đoạn 1.2)

## Luồng lấy cân

Hai nút độc lập:

- **LẤY CÂN LẦN 1** → `DraftWeight1` (+ thời gian, ảnh nháp). Lần sau: **CẬP NHẬT CÂN LẦN 1**.
- **LẤY CÂN LẦN 2** → tương tự. Không khóa sau khi đủ 2 lần — có thể cập nhật trước khi LƯU.

Tổng/Bì/Hàng chỉ tính khi có **đủ 2** giá trị draft. Phiếu chỉ 1 cân: Tổng/Bì/Hàng = null (UI `—`).

## Cân dịch vụ (không đơn giá)

Không nhập đơn giá hoặc đơn giá ≤ 0 được coi là **cân dịch vụ**:

- Vẫn lưu Tổng, Bì, Hàng khi có 2 cân.
- **Không** tính trừ bì, khối lượng tính tiền, thành tiền.
- DB lưu `NULL` (không lưu 0).
- UI và danh sách hiển thị `—` cho các trường tính tiền.

Nhập/xóa đơn giá trên draft → tính lại ngay trên giao diện.

## Draft vs dữ liệu chính thức

Trước **LƯU**: không phiếu DB, không danh sách, không số phiếu, không báo cáo.

## Ảnh

- Mỗi lần cân → chụp mới. Cập nhật thành công → xóa ảnh nháp cũ.
- Camera lỗi → giữ kg mới, ảnh cũ **không** hợp lệ; toast/status bar.
- **LƯU** → promote ảnh hợp lệ sang thư mục chính thức.

## LƯU linh hoạt

Tối thiểu: ≥ 1 trọng lượng. Các field khác nullable.

## Tiếp tục phiếu 1 cân

Cửa sổ **Chi tiết phiếu** hoặc nút **TIẾP TỤC CÂN** → UPDATE phiếu cũ. Không sửa cân đã lưu (admin sau này).

## Công thức

Khi có đủ 2 trọng lượng:

```
GrossWeight = Max(W1,W2)
TareWeight = Min(W1,W2)
NetWeight = Abs(W1-W2)
```

Chỉ khi `UnitPrice > 0`:

```
DeductionWeight = NetWeight/1000×3
BillableWeight = Round(Net-Deduction, 0, AwayFromZero)
TotalAmount = BillableWeight × UnitPrice
```

## Danh sách vs chi tiết

- **Danh sách chính:** Ngày giờ, Số phiếu, Biển số, Khách, Loại hàng, Tổng, Bì, Hàng, KL tính tiền, Đơn giá, Thành tiền, Ghi chú. Không hiển thị Lần 1/Lần 2.
- **Chi tiết phiếu:** Weight1/2, thời gian, ảnh, kết quả đầy đủ.

## DeviceMode

`Simulation` (mặc định) — random/manual cân, camera mô phỏng. `Hardware` — giai đoạn sau.

## Ngoài phạm vi 1.2

COM, RTSP, in, Excel, phân quyền admin.

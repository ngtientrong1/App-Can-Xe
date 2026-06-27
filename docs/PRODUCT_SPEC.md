# CanXe — Product Spec (Giai đoạn 1)

## Mục tiêu

Phần mềm cân xe Windows thay thế phần mềm cũ, giữ bố cục quen thuộc, lưu SQLite, mô phỏng đầu cân và camera.

## Công nghệ

- C# / .NET 10 (SDK 10.0.301+)
- WPF + MVVM (CommunityToolkit.Mvvm)
- SQLite + Entity Framework Core
- xUnit

## Luồng nghiệp vụ chính

### Ghi trọng lượng

1. Bấm `GHI TRỌNG LƯỢNG` → chốt kg tại thời điểm bấm.
2. Tạo `WeighEvent` (sequence 1 rồi 2).
3. Gọi camera mô phỏng **bất đồng bộ**; lỗi camera → toast/status bar, **không** rollback.
4. Sau 2 lần ghi → **khóa** nút `GHI TRỌNG LƯỢNG`.

### Lưu phiếu

- Bắt buộc: ≥ 1 trọng lượng.
- Khách hàng, biển số, loại hàng, đơn giá: **tùy chọn**; thiếu → `null`.
- Phiếu mới → sinh số phiếu tự động.
- Phiếu 1 trọng lượng đã lưu → chọn dòng → tiếp tục → cập nhật phiếu cũ + event 2.
- Lưu thành công → prepend danh sách, clear form, giữ trọng lượng trực tiếp.
- Lưu thất bại → **không** clear.

### Hủy bỏ

Clear form ngay, không xác nhận, không xóa DB.

## Công thức (Domain)

```
GrossWeight      = Max(W1, W2)
TareWeight       = Min(W1, W2)
NetWeight        = Abs(W1 - W2)
DeductionWeight  = NetWeight / 1000 × 3    // kg, có thể thập phân
BillableWeight   = Round(NetWeight - DeductionWeight, 0, AwayFromZero)  // kg nguyên
TotalAmount      = BillableWeight × UnitPrice   // VNĐ nguyên, khi có đơn giá
```

Chỉ tính đủ khi có **hai** trọng lượng.

## Đơn vị hiển thị

| Trường | Đơn vị |
|--------|--------|
| Trọng lượng | kg |
| Trọng lượng trực tiếp | kg nguyên |
| DeductionWeight | kg (có thập phân) |
| BillableWeight | kg nguyên |
| Đơn giá | VNĐ/kg, nguyên |
| Thành tiền | VNĐ nguyên |

## Số phiếu

| Loại | Ví dụ |
|------|-------|
| Hiển thị | `0059/06` |
| Nội bộ | `202606-0059` |

Số thứ tự chạy liên tục trong tháng. Không cho sửa (Giai đoạn 1).

## Ngày giờ

`DateTimeOffset`, tự động, không sửa tay (Giai đoạn 1).

## Khách hàng / tìm kiếm

- Gợi ý theo tên (có/không dấu, không phân biệt hoa thường).
- Chỉ gợi ý, không tự thay thế.
- Cảnh báo tên gần giống khi lưu.
- `CustomerNameSnapshot`, `CargoTypeNameSnapshot` trên phiếu.

## Báo cáo (chuẩn bị schema)

Nhóm: **Loại hàng → Khách hàng → Đơn giá**. Không gộp đơn giá khác nhau. Tổng = cộng `TotalAmountVnd` từng phiếu.

## Ngoài phạm vi Giai đoạn 1

COM, RTSP, máy in, Excel, phân quyền admin.

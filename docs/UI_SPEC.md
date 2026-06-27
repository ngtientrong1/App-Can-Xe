# CanXe — UI Spec (Giai đoạn 1.7 / 1.7.1)

## Design system

File: `src/CanXe.Desktop/Themes/CanXeDesignSystem.xaml`

Brushes: `PageBackgroundBrush`, `CardBackgroundBrush`, `CardBorderBrush`, `PrimaryTextBrush`, `SecondaryTextBrush`, `AccentBlueBrush`, `SuccessGreenBrush`, `WeightOrangeBrush`, `DangerBrush`, `SelectedRowBrush`.

Styles bắt buộc trên MainWindow: `CanXeCardStyle`, `CanXeTextBoxStyle`, `CanXeComboBoxStyle`, `CanXePrimaryButtonStyle`, `CanXeSuccessButtonStyle`, `CanXeWeightButtonStyle`, `CanXeSecondaryButtonStyle`, `CanXeNavigationButtonStyle`, `CanXeDataGridStyle`, `CanXeDataGridHeaderStyle`, `CanXeStatusBadgeStyle`, `CanXeSummaryCardStyle`, `CanXeUtilityRailButtonStyle`, `CanXeSegmentButtonStyle`.

Font: Segoe UI Variable (fallback Segoe UI). Card radius 10–12px; input 7–9px; button 8–10px; input height 46–50px; action button 54–58px; nav 48–52px; DataGrid row/header 44–48px.

## Grid chính (PHIẾU CÂN)

```text
Row 0: Header thiết bị — Auto
Row 1: Khu làm việc — Auto Max ~340px (compact giảm font cân)
Row 2: Thanh nút — Auto (UniformGrid, cùng chiều cao)
Row 3: Bộ lọc — Auto (segment tabs + nâng cao ẩn)
Row 4: DataGrid — *
Row 5: Footer summary — 5 card
```

## Header

Logo CanXe · tên app · badge `● Đầu cân: Tự động | DEV thủ công | Mất kết nối` · badge camera · ngày giờ.

## Navigation

HỆ THỐNG · PHIẾU CÂN (mặc định) · DANH MỤC · THIẾT BỊ · BÁO CÁO · CÀI ĐẶT.

Tab active: nền xanh nhạt + chữ/icon xanh (`CanXeNavigationButtonStyle` + DataTrigger).

## Khu làm việc (Full HD)

4 cột: Cân ~31* · Form ~51* · Camera drawer 300px (khi mở) · Utility rail 56px.

Camera đóng: drawer width = 0, form mở rộng ~69*, rail vẫn hiện — không để cột trắng.

### Khu trọng lượng

`TRỌNG LƯỢNG TRỰC TIẾP` · số kg (88–104px Full HD, ≥68px compact) · `● ỔN ĐỊNH` · `Nguồn: Tự động mô phỏng | DEV thủ công`.

Card Cân lần 1/2 · Tổng/Bì/Hàng (Hàng màu xanh) · billing.

### Utility rail

~56px, icon + chữ `CAMERA` / `DEV`, tooltip, trạng thái active khi drawer mở.

### Camera drawer

Header CAMERA · preview 16:9 · badge trạng thái · phiếu vừa lưu · Xem phiếu · Thu gọn.

### DEV drawer

Segment: `TỰ ĐỘNG MÔ PHỎNG` · `NHẬP THỦ CÔNG` · `ĐẦU CÂN COM — CHƯA KÍCH HOẠT` (disabled khi Simulation).

Manual: nhập kg · ÁP DỤNG · preset 8.500 / 18.500 · nút `TRỞ VỀ TỰ ĐỘNG` khi manual.

Đóng drawer **không** đổi `ScaleInputMode`. Checkbox Weight1 override độc lập với nguồn cân.

## Form

MaxWidth gợi ý: Customer 440, Plate 320, Cargo 340, Price 230. Autocomplete dùng card dropdown (không ListBox mặc định).

## Action bar

Tạo: Cân 1/2 · LƯU · HỦY · XEM · IN (UniformGrid 6 cột).

Sửa: CẬP NHẬT · THOÁT · MỞ KHÓA · XEM · IN (5 cột).

## Bộ lọc

Segment: HÔM NAY · HÔM QUA · 7 NGÀY · THÁNG NÀY · Thêm/Ẩn bộ lọc. Tab active highlight qua `ActiveQuickFilterKey`.

## DataGrid

`CanXeDataGridStyle`, row 46px, số căn phải, dòng chọn xanh nhạt, xen kẽ nhẹ.

## Footer summary

5 card: Số phiếu · Tổng hàng · KL tính tiền · Tổng tiền · Chưa có giá.

## CÀI ĐẶT / THIẾT BỊ

Card layout, label trên input, input cao 46–50px, nhóm Trạm cân / Đầu cân / Camera, nút Kiểm tra kết nối + Lưu cấu hình. Mật khẩu camera có toggle hiện/ẩn.

## Preview overlay

`XEM TRƯỚC PHIẾU` · tab MẶT TRƯỚC/SAU · SAO CHÉP ẢNH · LƯU ẢNH · IN · ĐÓNG · Escape.

## Responsive

Nghiệm thu: 1920×1080 (100%/125%), 1600×900, 1366×768 (100%/125%). Compact mode ≤1366px.

## Chưa giống mockup (1.7.1)

- Logo vector chính thức (hiện text/icon đơn giản).
- Preview camera RTSP thật (placeholder 16:9).
- Icon action bar tùy chỉnh (hiện emoji).
- Password masking khi ẩn (toggle hiện/ẩn text, chưa dùng PasswordBox binding).

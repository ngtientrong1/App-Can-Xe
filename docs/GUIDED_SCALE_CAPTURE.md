# Guided Scale Capture — CanXe Device Tester

Hướng dẫn thu bộ dữ liệu COM chuẩn phục vụ phân tích protocol bàn cân.

## Chuẩn bị

1. **Đóng hoàn toàn** phần mềm cân cũ (không chia sẻ COM1).
2. Mở `CanXe.DeviceTester.exe`.
3. Chọn **Đo theo hướng dẫn**.
4. Chọn **Cấu hình thử nghiệm** (mặc định `9600 / 8 / None / 1 / None`).
5. Chọn thư mục lưu log (mặc định `Documents\CanXeDeviceTester\Logs`).

Mỗi lần đo chỉ dùng **một** cấu hình COM. Sau khi hoàn thành 3 phiên, đóng cổng rồi mới chọn cấu hình khác nếu cần.

## Ba phiên bắt buộc

### 1. EmptyStable — Bàn cân trống ổn định

1. Bảo đảm không có người và không có xe trên bàn cân.
2. Nhập số đang hiển thị trên đầu cân (có thể `0`).
3. Mở cổng, xác nhận có byte đang nhận.
4. Chờ countdown ổn định **10 giây**.
5. Ghi **30 giây**, tự dừng.
6. Xem bảng kiểm tra chất lượng → **Lưu phiên**.

Output mẫu:

```text
CanXe_COM1_9600_8N1_EmptyStable_yyyyMMdd_HHmmss.log
CanXe_COM1_9600_8N1_EmptyStable_yyyyMMdd_HHmmss.raw.bin
CanXe_COM1_9600_8N1_EmptyStable_yyyyMMdd_HHmmss.session.json
```

### 2. PersonStable — Một người đứng ổn định

1. Một người đứng an toàn trên bàn cân, chờ số ổn định.
2. Nhập **chính xác** số hiển thị trên đầu cân (không mặc định 50 kg).
3. Countdown ổn định **10 giây**.
4. Ghi **30 giây**, tự dừng.
5. Lưu phiên.

Sau phiên 1 và 2, xem **So sánh stable** để biết hai stream giống/khác.

### 3. EmptyPersonTransition — Trống → người lên → ổn định → xuống → trống

1. Bắt đầu với bàn cân trống.
2. Ghi ngay; **10 giây** trống.
3. Bấm **NGƯỜI BẮT ĐẦU BƯỚC LÊN** khi người bước lên.
4. Bấm **NGƯỜI ĐÃ ĐỨNG ỔN ĐỊNH** khi số ổn định.
5. Giữ **20 giây**.
6. Bấm **NGƯỜI BẮT ĐẦU BƯỚC XUỐNG**.
7. Bấm **BÀN CÂN ĐÃ TRỞ LẠI TRỐNG** khi cân trống.
8. Ghi thêm **10 giây**, tự dừng và lưu.

Marker được ghi vào `.session.json` và text log, **không** chèn vào `.raw.bin`.

## Kiểm tra chất lượng

| Trạng thái | Ý nghĩa |
|------------|---------|
| Capture hợp lệ | Đủ thời lượng, có byte, đủ 3 file, raw size khớp |
| Capture có cảnh báo | Chỉ 1–2 byte unique (00/80), gap dài, thiếu marker transition |
| Capture không hợp lệ | Không byte, raw rỗng, lỗi cổng, thiếu file |

## Lưu ý

- Ứng dụng **chỉ đọc** COM — không gửi command.
- Không dùng kết quả để ghi phiếu cân.
- Copy bộ 9 file (3 phiên × 3 file) sang `C:\CanXeProtocolSamples` để phân tích offline bằng Protocol Analyzer.

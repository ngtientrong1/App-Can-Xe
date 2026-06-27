# CanXe Device Tester — Hướng dẫn sử dụng (Giai đoạn 2A)

Công cụ **chỉ đọc** dữ liệu thô từ cổng COM để chẩn đoán đầu cân. Không gửi byte, không reset thiết bị, không tích hợp vào CanXe.Desktop.

## Chuẩn bị trên PC bàn cân

1. Chép toàn bộ thư mục `publish\device-tester-win10-x64` sang PC bàn cân.
2. **Đóng hoàn toàn** phần mềm cân cũ.
3. Mở Task Manager, xác nhận không còn process phần mềm cân cũ.
4. Cắm cáp COM/USB-serial, xác nhận Windows nhận **COM1** (hoặc tên cổng tương ứng).

## Chạy DeviceTester

5. Chạy `CanXe.DeviceTester.exe`.
6. Bấm **QUÉT CỔNG**, chọn **COM1**.
7. Kiểm tra cấu hình mặc định: **9600, 8, None, One, None**.
8. Chọn nhãn phiên test (ví dụ: *Không có xe*, *Xe trống*, *Trọng lượng ổn định*).
9. Nhập ghi chú: trọng lượng hiển thị trên đầu cân, tình trạng xe.
10. Bấm **MỞ CỔNG** — chỉ mở khi bạn chủ động bấm.
11. Quan sát **Raw HEX**, **text escape**, **Event chunks** (mỗi lần DataReceived là một block riêng).
12. Bấm **BẮT ĐẦU GHI** trước hoặc trong khi test.
13. Thực hiện các phiên test theo kịch bản vận hành (có/không xe, ổn định/đang thay đổi…).
14. Bấm **DỪNG GHI** (nếu cần), sau đó **LƯU LOG**.
15. Bấm **ĐÓNG CỔNG**.
16. Đóng DeviceTester.
17. Mở lại phần mềm cân cũ nếu cần vận hành bình thường.

## Lưu log

- Mặc định: `Documents\CanXeDeviceTester\Logs`
- Tên file: `CanXe_COM1_yyyyMMdd_HHmmss.log`
- Có thể chọn thư mục khác trước khi lưu.
- **Không** commit file log vào Git.

## Cảnh báo quan trọng

> Chỉ chạy công cụ này khi phần mềm cân cũ đã được đóng hoàn toàn. Hai ứng dụng thường **không thể** cùng sử dụng COM1.

DeviceTester **không** tự đóng phần mềm khác.

## Pause hiển thị

- **PAUSE HIỂN THỊ** tạm dừng cập nhật màn hình (giảm tải PC yếu).
- Ghi log vẫn tiếp tục nếu đang **BẮT ĐẦU GHI**.

## Những gì chưa biết (Giai đoạn 2A)

Chưa xác định:

- Ký tự kết thúc frame (CR/LF/CRLF/khác)
- Độ dài frame
- Encoding thực tế từ đầu cân
- Text hay binary
- Vị trí số trọng lượng và đơn vị
- Ký hiệu ổn định/không ổn định
- Tần suất gửi (liên tục / theo sự kiện)

Giai đoạn 2A **không** parse trọng lượng — chỉ thu thập raw log để phân tích sau.

## Publish

```cmd
build\publish-device-tester.cmd
```

Output: `publish\device-tester-win10-x64\`

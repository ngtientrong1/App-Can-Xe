using CanXe.DeviceTester.Core.Models;

namespace CanXe.DeviceTester.Core;

public static class SerialCaptureUiLimits
{
    public const int MaxDisplayChunks = 5000;
}

public static class SerialPortErrorMessages
{
    public const string PortNotFound = "Cổng COM không tồn tại. Hãy kiểm tra tên cổng hoặc bấm QUÉT CỔNG.";
    public const string AccessDenied = "Không thể mở {0}. Hãy đóng phần mềm cân cũ và thử lại.";
    public const string PortBusy = "Cổng {0} đang được phần mềm khác sử dụng. Hãy đóng phần mềm cân cũ hoàn toàn.";
    public const string Disconnected = "Mất kết nối cổng nối tiếp. Kiểm tra cáp và đầu cân.";
    public const string ReadError = "Lỗi đọc dữ liệu từ cổng nối tiếp.";
    public const string InvalidConfiguration = "Cấu hình cổng không hợp lệ.";
    public const string NoDataYet = "Chưa nhận dữ liệu từ đầu cân.";
}

public static class SerialPortExceptionMapper
{
    public static (SerialConnectionStatus Status, string Message) Map(Exception ex, string portName) =>
        ex switch
        {
            System.IO.FileNotFoundException or System.IO.DirectoryNotFoundException =>
                (SerialConnectionStatus.ReadError, SerialPortErrorMessages.PortNotFound),
            UnauthorizedAccessException =>
                (SerialConnectionStatus.PortBusy, string.Format(SerialPortErrorMessages.AccessDenied, portName)),
            InvalidOperationException when ex.Message.Contains("denied", StringComparison.OrdinalIgnoreCase) =>
                (SerialConnectionStatus.PortBusy, string.Format(SerialPortErrorMessages.PortBusy, portName)),
            InvalidOperationException =>
                (SerialConnectionStatus.ReadError, SerialPortErrorMessages.InvalidConfiguration),
            System.IO.IOException =>
                (SerialConnectionStatus.Disconnected, SerialPortErrorMessages.Disconnected),
            _ => (SerialConnectionStatus.ReadError, $"{SerialPortErrorMessages.ReadError} ({ex.Message})")
        };
}

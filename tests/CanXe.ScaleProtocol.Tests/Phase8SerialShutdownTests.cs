using CanXe.ScaleProtocol.Core;

namespace CanXe.ScaleProtocol.Tests;

public sealed class Phase8SerialShutdownTests
{
    [Fact]
    public void WindowsScaleSerialReader_Dispose_DoesNotThrowWhenNeverOpened()
    {
        var reader = new WindowsScaleSerialReader(new ScaleSerialSettings { PortName = "COM1" });
        var ex = Record.Exception(() => reader.Dispose());
        Assert.Null(ex);
        Assert.False(reader.IsPortOpen);
        Assert.Equal(ScaleConnectionState.Disconnected, reader.ConnectionState);
    }

    [Fact]
    public void WindowsScaleSerialReader_Dispose_IsIdempotent()
    {
        var reader = new WindowsScaleSerialReader();
        reader.Dispose();
        var ex = Record.Exception(() => reader.Dispose());
        Assert.Null(ex);
    }

    [Fact]
    public async Task WindowsScaleSerialReader_Connect_BusyPort_FailsSoftly()
    {
        // Use an invalid port name — Open fails without freezing the calling thread forever.
        var reader = new WindowsScaleSerialReader(new ScaleSerialSettings
        {
            PortName = "COM_DOES_NOT_EXIST_999",
            BaudRate = 9600,
            ReadTimeout = 500
        });

        try
        {
            var sw = System.Diagnostics.Stopwatch.StartNew();
            var ex = await Record.ExceptionAsync(async () =>
                await reader.ConnectAsync().WaitAsync(TimeSpan.FromSeconds(5)));
            sw.Stop();

            Assert.NotNull(ex);
            Assert.True(sw.Elapsed < TimeSpan.FromSeconds(5));
            Assert.False(reader.IsPortOpen);
        }
        finally
        {
            reader.Dispose();
        }
    }
}

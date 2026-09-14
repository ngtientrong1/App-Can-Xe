using System.Diagnostics;
using CanXe.ScaleProtocol.Core;

namespace CanXe.ScaleProtocol.Tests;

/// <summary>
/// A fake never blocks by default; give it a <see cref="ManualResetEventSlim"/> that the test
/// never sets to simulate a wedged USB-to-serial driver whose Open()/Close() never returns.
/// </summary>
internal sealed class FakeSerialPortHandle : ISerialPortHandle
{
    private readonly ManualResetEventSlim _releaseOpen;
    private readonly ManualResetEventSlim _releaseClose;

    public FakeSerialPortHandle(ManualResetEventSlim? releaseOpen = null, ManualResetEventSlim? releaseClose = null)
    {
        _releaseOpen = releaseOpen ?? new ManualResetEventSlim(true);
        _releaseClose = releaseClose ?? new ManualResetEventSlim(true);
    }

    public bool ThrowOnOpen { get; set; }
    public bool CloseCalled { get; private set; }
    public bool IsOpen { get; private set; }
    public int BytesToRead => 0;
    public int PokeCount { get; private set; }
    public void Poke() => PokeCount++;

    public void Open()
    {
        _releaseOpen.Wait();
        if (ThrowOnOpen)
            throw new InvalidOperationException("simulated open failure");
        IsOpen = true;
    }

    public void Close()
    {
        CloseCalled = true;
        _releaseClose.Wait();
        IsOpen = false;
    }

    public int Read(byte[] buffer, int offset, int count) => 0;
    public void Dispose() { }

    public event EventHandler? DataAvailable;
    public event EventHandler<string>? SerialErrorOccurred;
}

internal sealed class FakeSerialPortFactory(Func<ScaleSerialSettings, ISerialPortHandle> create) : ISerialPortFactory
{
    public ISerialPortHandle Create(ScaleSerialSettings settings) => create(settings);
}

public sealed class WindowsScaleSerialReaderHangTests
{
    [Fact]
    public async Task ConnectAsync_ReturnsWithinTimeout_WhenOpenHangsForever()
    {
        var neverReleased = new ManualResetEventSlim(false);
        var stuckHandle = new FakeSerialPortHandle(releaseOpen: neverReleased);
        var factory = new FakeSerialPortFactory(_ => stuckHandle);
        var reader = new WindowsScaleSerialReader(new ScaleSerialSettings { OpenCloseTimeoutMs = 200 }, factory);

        var sw = Stopwatch.StartNew();
        await reader.ConnectAsync().WaitAsync(TimeSpan.FromSeconds(5));
        sw.Stop();

        Assert.True(sw.ElapsedMilliseconds < 2000, $"ConnectAsync took {sw.ElapsedMilliseconds}ms — should abandon around 200ms.");
        Assert.Equal(ScaleConnectionState.Hung, reader.ConnectionState);
        Assert.NotNull(reader.LastError);

        reader.Dispose();
    }

    [Fact]
    public async Task ConnectAsync_SucceedsOnFreshAttempt_WhileOldAttemptStillStuck()
    {
        var neverReleased = new ManualResetEventSlim(false);
        var stuckHandle = new FakeSerialPortHandle(releaseOpen: neverReleased);
        var goodHandle = new FakeSerialPortHandle();

        var callCount = 0;
        var factory = new FakeSerialPortFactory(_ => ++callCount == 1 ? stuckHandle : goodHandle);
        var reader = new WindowsScaleSerialReader(new ScaleSerialSettings { OpenCloseTimeoutMs = 150 }, factory);

        await reader.ConnectAsync().WaitAsync(TimeSpan.FromSeconds(5));
        Assert.Equal(ScaleConnectionState.Hung, reader.ConnectionState);

        // The watchdog's next retry must not be blocked by the still-stuck first attempt.
        await reader.ConnectAsync().WaitAsync(TimeSpan.FromSeconds(5));
        Assert.Equal(ScaleConnectionState.Connected, reader.ConnectionState);
        Assert.True(reader.IsPortOpen);

        reader.Dispose();
    }

    [Fact]
    public async Task AbandonedAttempt_CompletingLate_DoesNotOverwriteNewerConnection()
    {
        var releaseStuckOpen = new ManualResetEventSlim(false);
        var stuckHandle = new FakeSerialPortHandle(releaseOpen: releaseStuckOpen);
        var goodHandle = new FakeSerialPortHandle();

        var callCount = 0;
        var factory = new FakeSerialPortFactory(_ => ++callCount == 1 ? stuckHandle : goodHandle);
        var reader = new WindowsScaleSerialReader(new ScaleSerialSettings { OpenCloseTimeoutMs = 150 }, factory);

        await reader.ConnectAsync().WaitAsync(TimeSpan.FromSeconds(5)); // attempt 1: hangs -> Hung
        await reader.ConnectAsync().WaitAsync(TimeSpan.FromSeconds(5)); // attempt 2: succeeds -> Connected
        Assert.Equal(ScaleConnectionState.Connected, reader.ConnectionState);

        // Let the abandoned attempt 1 finally "succeed" in the background.
        releaseStuckOpen.Set();
        await Task.Delay(TimeSpan.FromMilliseconds(500));

        // The current (attempt 2) connection must remain untouched, and the stale handle must
        // have been quietly closed rather than left dangling as the reader's active port.
        Assert.Equal(ScaleConnectionState.Connected, reader.ConnectionState);
        Assert.True(reader.IsPortOpen);
        Assert.True(stuckHandle.CloseCalled);
        Assert.False(stuckHandle.IsOpen);

        reader.Dispose();
    }

    [Fact]
    public async Task DisconnectAsync_ReturnsWithinTimeout_WhenCloseHangsForever()
    {
        var neverReleased = new ManualResetEventSlim(false);
        var handle = new FakeSerialPortHandle(releaseClose: neverReleased);
        var factory = new FakeSerialPortFactory(_ => handle);
        var reader = new WindowsScaleSerialReader(new ScaleSerialSettings { OpenCloseTimeoutMs = 150 }, factory);

        await reader.ConnectAsync().WaitAsync(TimeSpan.FromSeconds(5));
        Assert.Equal(ScaleConnectionState.Connected, reader.ConnectionState);

        var sw = Stopwatch.StartNew();
        await reader.DisconnectAsync().WaitAsync(TimeSpan.FromSeconds(5));
        sw.Stop();

        Assert.True(sw.ElapsedMilliseconds < 2000, $"DisconnectAsync took {sw.ElapsedMilliseconds}ms — should abandon around 150ms.");
        // The internal port reference is already cleared before the blocking Close() call, so a
        // fresh connect isn't blocked by it — but the hang itself is still surfaced as Hung rather
        // than silently reporting Disconnected, since a wedged driver is worth flagging.
        Assert.Equal(ScaleConnectionState.Hung, reader.ConnectionState);
        Assert.False(reader.IsPortOpen);

        reader.Dispose();
    }
}

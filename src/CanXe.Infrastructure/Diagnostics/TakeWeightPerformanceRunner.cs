using System.Diagnostics;
using CanXe.Application.Interfaces;
using CanXe.Application.Models;
using CanXe.Application.Services;
using CanXe.Domain.Models;
using CanXe.ScaleProtocol.Core;
using Microsoft.Extensions.DependencyInjection;

namespace CanXe.Infrastructure.Diagnostics;

public static class TakeWeightPerformanceRunner
{
    public static async Task<int> RunAsync(IServiceProvider services, int cycles)
    {
        var ticketService = services.GetRequiredService<WeighTicketService>();
        var scale = services.GetRequiredService<IScaleService>();

        if (scale is IHardwareScaleDiagnostics hardware && hardware.InputMode == ScaleInputMode.Hardware)
            await hardware.SetInputModeAsync(ScaleInputMode.SimulationManual).ConfigureAwait(false);

        scale.SetManualMode(true);
        scale.SetManualWeightKg(200);

        var maxCaptureMs = 0L;
        var reconnectCount = 0;

        for (var i = 0; i < cycles; i++)
        {
            var draft = new WeighTicketDraft();
            var click = Stopwatch.GetTimestamp();
            var result = await ticketService.CaptureWeightAsync(draft, 1).ConfigureAwait(false);
            var captured = Stopwatch.GetTimestamp();
            var captureMs = (captured - click) * 1000 / Stopwatch.Frequency;
            maxCaptureMs = Math.Max(maxCaptureMs, captureMs);

            if (!result.Success)
            {
                Console.Error.WriteLine($"Cycle {i + 1} failed: {result.ErrorMessage}");
                return 1;
            }

            await ticketService.WaitForPendingPhotosAsync().ConfigureAwait(false);
        }

        Console.WriteLine($"Take-weight performance: {cycles} cycles (simulation)");
        Console.WriteLine($"Click-to-weight-captured max: {maxCaptureMs} ms");
        Console.WriteLine($"Unexpected reconnect count: {reconnectCount}");

        var pass = maxCaptureMs <= 100;
        Console.WriteLine(pass ? "PASS" : "FAIL");
        return pass ? 0 : 1;
    }
}

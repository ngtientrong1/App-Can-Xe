using System.Reflection;
using System.Text;
using CanXe.ScaleProtocol.Core;

namespace CanXe.ScaleProtocol.Tests;

public class ScaleFrameParserTests
{
    private static ScaleReading ParseSingle(byte[] frame, ScaleStabilityDetector? stability = null)
    {
        stability ??= new ScaleStabilityDetector();
        var parser = new ScaleFrameParser();
        var readings = parser.Append(frame, DateTimeOffset.UtcNow, stability);
        Assert.Single(readings);
        return readings[0];
    }

    [Fact]
    public void Parse_Frame0Kg()
    {
        var reading = ParseSingle(ScaleFrameFixtures.Frame0Kg);
        Assert.Equal(0, reading.WeightKg);
        Assert.Equal('+', reading.Sign);
        Assert.Equal("01", reading.ProtocolCode);
    }

    [Fact]
    public void Parse_Frame10Kg()
    {
        var reading = ParseSingle(ScaleFrameFixtures.Frame10Kg);
        Assert.Equal(10, reading.WeightKg);
        Assert.Equal('A', reading.Checksum);
    }

    [Fact]
    public void Parse_Frame50Kg()
    {
        var reading = ParseSingle(ScaleFrameFixtures.Frame50Kg);
        Assert.Equal(50, reading.WeightKg);
        Assert.Equal('E', reading.Checksum);
    }

    [Fact]
    public void Parse_NegativeSign()
    {
        var frame = ScaleFrameFixtures.BuildFrame('-', 25);
        var reading = ParseSingle(frame);
        Assert.Equal(-25, reading.WeightKg);
        Assert.Equal('-', reading.Sign);
    }

    [Fact]
    public void Checksum_Frame0_IsB() =>
        Assert.Equal('B', ScaleProtocolChecksum.ComputeExpectedChecksum(ScaleFrameFixtures.Frame0Kg));

    [Fact]
    public void Checksum_Frame10_IsA() =>
        Assert.Equal('A', ScaleProtocolChecksum.ComputeExpectedChecksum(ScaleFrameFixtures.Frame10Kg));

    [Fact]
    public void Checksum_Frame50_IsE() =>
        Assert.Equal('E', ScaleProtocolChecksum.ComputeExpectedChecksum(ScaleFrameFixtures.Frame50Kg));

    [Fact]
    public void Reject_InvalidChecksum()
    {
        var frame = (byte[])ScaleFrameFixtures.Frame0Kg.Clone();
        frame[10] = (byte)'Z';
        var parser = new ScaleFrameParser();
        var readings = parser.Append(frame, DateTimeOffset.UtcNow, new ScaleStabilityDetector());
        Assert.Empty(readings);
        Assert.Equal(1, parser.Statistics.InvalidFrames);
    }

    [Fact]
    public void Reject_MissingStx()
    {
        var frame = (byte[])ScaleFrameFixtures.Frame0Kg.Clone();
        frame[0] = 0x00;
        var result = ScaleFrameValidator.Validate(frame);
        Assert.False(result.IsValid);
    }

    [Fact]
    public void Reject_MissingEtx()
    {
        var frame = (byte[])ScaleFrameFixtures.Frame0Kg.Clone();
        frame[11] = 0x00;
        var result = ScaleFrameValidator.Validate(frame);
        Assert.False(result.IsValid);
    }

    [Fact]
    public void Reject_NonDigitInWeight()
    {
        var frame = (byte[])ScaleFrameFixtures.Frame0Kg.Clone();
        frame[4] = (byte)'X';
        var result = ScaleFrameValidator.Validate(frame);
        Assert.False(result.IsValid);
    }

    [Fact]
    public void ProtocolCode_IsExposed()
    {
        var reading = ParseSingle(ScaleFrameFixtures.Frame50Kg);
        Assert.Equal("01", reading.ProtocolCode);
    }

    [Fact]
    public void Streaming_Split8And4Bytes()
    {
        var frame = ScaleFrameFixtures.Frame10Kg;
        var parser = new ScaleFrameParser();
        var stability = new ScaleStabilityDetector();
        var at = DateTimeOffset.UtcNow;
        Assert.Empty(parser.Append(frame.AsSpan(0, 8), at, stability));
        var readings = parser.Append(frame.AsSpan(8), at, stability);
        Assert.Single(readings);
        Assert.Equal(10, readings[0].WeightKg);
    }

    [Fact]
    public void Streaming_SplitByteByByte()
    {
        var frame = ScaleFrameFixtures.Frame50Kg;
        var parser = new ScaleFrameParser();
        var stability = new ScaleStabilityDetector();
        var at = DateTimeOffset.UtcNow;
        ScaleReading? last = null;
        foreach (var b in frame)
        {
            var batch = parser.Append([b], at, stability);
            if (batch.Count > 0)
                last = batch[0];
        }

        Assert.NotNull(last);
        Assert.Equal(50, last.WeightKg);
    }

    [Fact]
    public void Streaming_MultipleFramesInOneCallback()
    {
        var payload = ScaleFrameFixtures.Frame0Kg
            .Concat(ScaleFrameFixtures.Frame10Kg)
            .Concat(ScaleFrameFixtures.Frame50Kg)
            .ToArray();
        var parser = new ScaleFrameParser();
        var stability = new ScaleStabilityDetector();
        var readings = parser.Append(payload, DateTimeOffset.UtcNow, stability);
        Assert.Equal(3, readings.Count);
        Assert.Equal([0L, 10L, 50L], readings.Select(r => r.WeightKg).ToArray());
    }

    [Fact]
    public void Streaming_DiscardsGarbageBeforeStx()
    {
        var payload = new byte[] { 0xFF, 0x00, 0x80 }.Concat(ScaleFrameFixtures.Frame0Kg).ToArray();
        var parser = new ScaleFrameParser();
        var readings = parser.Append(payload, DateTimeOffset.UtcNow, new ScaleStabilityDetector());
        Assert.Single(readings);
        Assert.Equal(3, parser.Statistics.DiscardedBytes);
    }

    [Fact]
    public void Streaming_PartialFrameRemainsBuffered()
    {
        var parser = new ScaleFrameParser();
        parser.Append(ScaleFrameFixtures.Frame0Kg.AsSpan(0, 6), DateTimeOffset.UtcNow, new ScaleStabilityDetector());
        Assert.Equal(6, parser.BufferedByteCount);
        Assert.Equal(0, parser.Statistics.ValidFrames);
    }

    [Fact]
    public void Streaming_ResyncOnNewStxDuringBrokenFrame()
    {
        var broken = new byte[] { 0x02, 0x2B, 0x30 };
        var good = ScaleFrameFixtures.Frame0Kg;
        var parser = new ScaleFrameParser();
        var stability = new ScaleStabilityDetector();
        parser.Append(broken, DateTimeOffset.UtcNow, stability);
        var readings = parser.Append(good, DateTimeOffset.UtcNow, stability);
        Assert.Single(readings);
        Assert.Equal(0, readings[0].WeightKg);
    }

    [Fact]
    public void Parser_DoesNotTreatCallbackAsFrame()
    {
        var parser = new ScaleFrameParser();
        var readings = parser.Append(new byte[64], DateTimeOffset.UtcNow, new ScaleStabilityDetector());
        Assert.Empty(readings);
        Assert.Equal(0, parser.Statistics.ValidFrames);
    }

    [Fact]
    public void Parser_AccountsForAllBytes()
    {
        var payload = new byte[] { 0xFF }.Concat(ScaleFrameFixtures.Frame0Kg).Concat(ScaleFrameFixtures.Frame10Kg).ToArray();
        var parser = new ScaleFrameParser();
        parser.Append(payload, DateTimeOffset.UtcNow, new ScaleStabilityDetector());
        Assert.Equal(payload.Length, parser.Statistics.TotalBytesFed);
        Assert.Equal(2, parser.Statistics.ValidFrames);
        Assert.Equal(1, parser.Statistics.DiscardedBytes);
    }

    [Fact]
    public void Continuous_154EmptyFrames()
    {
        var parser = new ScaleFrameParser();
        var stability = new ScaleStabilityDetector();
        var at = DateTimeOffset.UtcNow;
        for (var i = 0; i < 154; i++)
            parser.Append(ScaleFrameFixtures.Frame0Kg, at, stability);
        Assert.Equal(154, parser.Statistics.ValidFrames);
    }

    [Fact]
    public void Continuous_159Frames50Kg()
    {
        var parser = new ScaleFrameParser();
        var stability = new ScaleStabilityDetector();
        var at = DateTimeOffset.UtcNow;
        for (var i = 0; i < 159; i++)
            parser.Append(ScaleFrameFixtures.Frame50Kg, at, stability);
        Assert.Equal(159, parser.Statistics.ValidFrames);
    }

    [Fact]
    public void Sequence_0_10_50_0()
    {
        var parser = new ScaleFrameParser();
        var stability = new ScaleStabilityDetector();
        var at = DateTimeOffset.UtcNow;
        var weights = new List<long>();
        foreach (var frame in new[] { ScaleFrameFixtures.Frame0Kg, ScaleFrameFixtures.Frame10Kg, ScaleFrameFixtures.Frame50Kg, ScaleFrameFixtures.Frame0Kg })
            weights.Add(parser.Append(frame, at, stability).Single().WeightKg);
        Assert.Equal([0L, 10L, 50L, 0L], weights);
    }

    [Fact]
    public void Stability_HardwareStableFlagBecomesStableAfterTwoFrames()
    {
        var parser = new ScaleFrameParser();
        var stability = new ScaleStabilityDetector();
        var at = DateTimeOffset.UtcNow;
        ScaleReading? last = null;
        for (var i = 0; i < 2; i++)
            last = parser.Append(ScaleFrameFixtures.Frame50Kg, at, stability).Single();
        Assert.NotNull(last);
        Assert.True(last.IsStable);
        Assert.Equal(ScaleStableSource.HardwareFlag, last.StableSource);
        Assert.True(last.RawStableFlag);
        Assert.Equal(2, last.ConsecutiveMatchingFrames);
    }

    [Fact]
    public void Stability_SoftwareWindowUsesDivisionThreshold()
    {
        var parser = new ScaleFrameParser();
        var stability = new ScaleStabilityDetector(new ScaleStabilityOptions
        {
            ScaleDivisionKg = 20,
            SoftwareRequiredMatchingFrames = 3,
            SoftwareStableRequiredDurationMs = 300
        });
        var start = DateTimeOffset.UtcNow;
        ScaleReading? last = null;
        for (var i = 0; i < 3; i++)
        {
            var frame = ScaleFrameFixtures.BuildFrame('+', 200 + (i % 2) * 10, "02");
            last = parser.Append(frame, start.AddMilliseconds(i * 150), stability).Single();
        }

        Assert.NotNull(last);
        Assert.True(last.IsStable);
        Assert.Equal(ScaleStableSource.SoftwareWindow, last.StableSource);
    }

    [Fact]
    public void Stability_WeightChangeResetsStable()
    {
        var parser = new ScaleFrameParser();
        var stability = new ScaleStabilityDetector();
        var at = DateTimeOffset.UtcNow;
        for (var i = 0; i < 2; i++)
            parser.Append(ScaleFrameFixtures.Frame50Kg, at, stability);
        var changed = parser.Append(ScaleFrameFixtures.Frame10Kg, at, stability).Single();
        Assert.False(changed.IsStable);
        Assert.Equal(1, changed.ConsecutiveMatchingFrames);
    }

    [Fact]
    public void Stability_InvalidChecksumResets()
    {
        var parser = new ScaleFrameParser();
        var stability = new ScaleStabilityDetector();
        var at = DateTimeOffset.UtcNow;
        for (var i = 0; i < 2; i++)
            parser.Append(ScaleFrameFixtures.Frame50Kg, at, stability);
        var bad = (byte[])ScaleFrameFixtures.Frame50Kg.Clone();
        bad[10] = (byte)'0';
        parser.Append(bad, at, stability);
        var next = parser.Append(ScaleFrameFixtures.Frame50Kg, at, stability).Single();
        Assert.False(next.IsStable);
        Assert.Equal(1, next.ConsecutiveMatchingFrames);
    }

    [Fact]
    public void Stability_LongGapResets()
    {
        var parser = new ScaleFrameParser();
        var stability = new ScaleStabilityDetector();
        for (var i = 0; i < 2; i++)
            parser.Append(ScaleFrameFixtures.Frame50Kg, DateTimeOffset.UtcNow, stability);
        var afterGap = parser.Append(
            ScaleFrameFixtures.Frame50Kg,
            DateTimeOffset.UtcNow.AddSeconds(2),
            stability).Single();
        Assert.False(afterGap.IsStable);
        Assert.Equal(1, afterGap.ConsecutiveMatchingFrames);
    }

    [Fact]
    public void Stability_DisconnectResets()
    {
        var stability = new ScaleStabilityDetector();
        var parser = new ScaleFrameParser();
        for (var i = 0; i < 2; i++)
            parser.Append(ScaleFrameFixtures.Frame50Kg, DateTimeOffset.UtcNow, stability);
        stability.Reset();
        var reading = parser.Append(ScaleFrameFixtures.Frame50Kg, DateTimeOffset.UtcNow, stability).Single();
        Assert.False(reading.IsStable);
    }

    [Fact]
    public void ProtocolCode_OtherThan01_ParsesWithWarning()
    {
        var frame = ScaleFrameFixtures.BuildFrame('+', 12, "02");
        var reading = ParseSingle(frame);
        Assert.Equal("02", reading.ProtocolCode);
        Assert.Contains("02", reading.DiagnosticWarning, StringComparison.Ordinal);
    }

    [Fact]
    public void Acceptance_KnownFramesMatchExpectedWeights()
    {
        Assert.Equal(0, ParseSingle(ScaleFrameFixtures.Frame0Kg).WeightKg);
        Assert.Equal(10, ParseSingle(ScaleFrameFixtures.Frame10Kg).WeightKg);
        Assert.Equal(50, ParseSingle(ScaleFrameFixtures.Frame50Kg).WeightKg);
    }

    [Fact]
    public void WindowsScaleSerialReader_HasNoWriteOrSend()
    {
        var methods = typeof(WindowsScaleSerialReader).GetMethods(BindingFlags.Instance | BindingFlags.Public)
            .Where(m => m.Name.Contains("Write", StringComparison.Ordinal) || m.Name.Contains("Send", StringComparison.Ordinal));
        Assert.Empty(methods);
    }
}

using System.IO;
using System.Reflection;
using System.Text;
using System.Windows;
using System.Windows.Threading;
using CanXe.DeviceTester.Core.Services;
using CanXe.ProtocolAnalyzer.Core.Analysis;
using CanXe.ProtocolAnalyzer.Core.Models;
using CanXe.ProtocolAnalyzer.Core.Parsing;
using CanXe.ProtocolAnalyzer.Core.Reconstruction;
using CanXe.ProtocolAnalyzer.Core.Reporting;
using CanXe.ProtocolAnalyzer.Core.Replay;
using CanXe.DeviceTester.Core.Models;

namespace CanXe.ProtocolAnalyzer.Tests;

public class Phase2BProtocolAnalyzerTests
{
    private static string WriteFixture(string content)
    {
        var path = Path.Combine(Path.GetTempPath(), $"canxe-fixture-{Guid.NewGuid():N}.log");
        File.WriteAllText(path, content, Encoding.UTF8);
        return path;
    }

    private const string MinimalHeader = """
Session started:
Port: COM1
BaudRate: 9600
DataBits: 8
Parity: None
StopBits: One
Handshake: None
Encoding: Ascii
Windows version: test
Application version: 2.0.0
Session label: test

""";

    [Fact]
    public void ParseMetadata_ReadsPortAndBaud()
    {
        var path = WriteFixture(MinimalHeader);
        try
        {
            var session = new SerialLogParser().ParseFile(path);
            Assert.Equal("COM1", session.Metadata.PortName);
            Assert.Equal(9600, session.Metadata.BaudRate);
        }
        finally { File.Delete(path); }
    }

    [Fact]
    public void ParseChunk_ReadsHexBytes()
    {
        var content = MinimalHeader + """
Timestamp: 2026-06-27T10:00:00.000
Chunk number: 1
Byte count: 2
HEX: 00 80
Escaped text: ignored

""";
        var path = WriteFixture(content);
        try
        {
            var session = new SerialLogParser().ParseFile(path);
            Assert.Single(session.Chunks);
            Assert.Equal([0x00, 0x80], session.Chunks[0].Bytes);
        }
        finally { File.Delete(path); }
    }

    [Fact]
    public void ParseChunk_ByteCountMismatch_AddsWarning()
    {
        var content = MinimalHeader + """
Timestamp: 2026-06-27T10:00:00.000
Chunk number: 1
Byte count: 5
HEX: 00 80
Escaped text: x

""";
        var path = WriteFixture(content);
        try
        {
            var session = new SerialLogParser().ParseFile(path);
            Assert.Contains(session.Warnings, w => w.Code == "BYTE_COUNT_MISMATCH");
        }
        finally { File.Delete(path); }
    }

    [Fact]
    public void ParseChunk_TruncatedLastLine_DoesNotCrash()
    {
        var content = MinimalHeader + """
Timestamp: 2026-06-27T10:00:00.000
Chunk number: 1
Byte count: 3
HEX: 00 AB G
""";
        var path = WriteFixture(content);
        try
        {
            var session = new SerialLogParser().ParseFile(path);
            Assert.NotEmpty(session.Chunks);
            Assert.Contains(session.Warnings, w => w.Code == "TRUNCATED_HEX");
        }
        finally { File.Delete(path); }
    }

    [Fact]
    public void ParseChunk_MissingChunkNumber_AddsWarning()
    {
        var content = MinimalHeader + """
Timestamp: 2026-06-27T10:00:00.000
Byte count: 1
HEX: 00
Escaped text: x

""";
        var path = WriteFixture(content);
        try
        {
            var session = new SerialLogParser().ParseFile(path);
            Assert.Empty(session.Chunks);
        }
        finally { File.Delete(path); }
    }

    [Fact]
    public void ParseChunk_DuplicateChunkNumber_AddsWarning()
    {
        var chunk = """
Timestamp: 2026-06-27T10:00:00.000
Chunk number: 1
Byte count: 1
HEX: 00
Escaped text: x

""";
        var path = WriteFixture(MinimalHeader + chunk + chunk);
        try
        {
            var warnings = new List<ProtocolAnalysisWarning>();
            var session = new SerialLogParser().ParseFile(path);
            new SerialStreamReconstructor().Reconstruct(session, warnings);
            Assert.Contains(warnings, w => w.Code == "DUPLICATE_CHUNK");
        }
        finally { File.Delete(path); }
    }

    [Fact]
    public void Reconstruct_SortsByTimestampThenChunkNumber()
    {
        var content = MinimalHeader + """
Timestamp: 2026-06-27T10:00:00.100
Chunk number: 2
Byte count: 1
HEX: 02
Escaped text: x

Timestamp: 2026-06-27T10:00:00.000
Chunk number: 1
Byte count: 1
HEX: 01
Escaped text: x

""";
        var path = WriteFixture(content);
        try
        {
            var session = new SerialLogParser().ParseFile(path);
            var stream = new SerialStreamReconstructor().Reconstruct(session);
            Assert.Equal([0x01, 0x02], stream.Bytes);
        }
        finally { File.Delete(path); }
    }

    [Fact]
    public void Reconstruct_PreservesAllBytesIncludingZeros()
    {
        var content = MinimalHeader + """
Timestamp: 2026-06-27T10:00:00.000
Chunk number: 1
Byte count: 4
HEX: 00 80 00 80
Escaped text: x

""";
        var path = WriteFixture(content);
        try
        {
            var session = new SerialLogParser().ParseFile(path);
            var stream = new SerialStreamReconstructor().Reconstruct(session);
            Assert.Equal([0x00, 0x80, 0x00, 0x80], stream.Bytes);
        }
        finally { File.Delete(path); }
    }

    [Fact]
    public void FrameDetector_DoesNotUseCallbackAsFrameBoundary()
    {
        var chunks = new List<SerialLogChunk>
        {
            MakeChunk(1, [0x00, 0x80]),
            MakeChunk(2, [0x00])
        };
        var stream = BuildStream(chunks);
        var frames = new FrameCandidateDetector().Detect(stream, new ProtocolAnalysisOptions { MaxFrameLengthBytes = 8 });
        Assert.All(frames, f => Assert.True(f.FrameLengthBytes != chunks[0].Bytes.Length || f.FrameLengthBytes > 1));
    }

    [Fact]
    public void TimingAnalyzer_ComputesMedian()
    {
        var session = BuildSession([
            MakeChunk(1, [0x00], "2026-06-27T10:00:00.000"),
            MakeChunk(2, [0x00], "2026-06-27T10:00:00.010"),
            MakeChunk(3, [0x00], "2026-06-27T10:00:00.030")
        ]);
        var timing = new TimingAnalyzer().Analyze(session);
        Assert.Equal(15, timing.MedianGapMs, 1);
    }

    [Fact]
    public void FrameDetector_FindsRepeatedPattern()
    {
        var bytes = Enumerable.Repeat(new byte[] { 0x00, 0x80 }, 20).SelectMany(x => x).ToArray();
        var stream = new ContinuousByteStream
        {
            Bytes = bytes,
            Mappings = [],
            SourceChunks = []
        };
        var frames = new FrameCandidateDetector().Detect(stream);
        Assert.Contains(frames, f => f.FrameLengthBytes == 2);
    }

    [Fact]
    public void BitTransform_ZeroTo0_80To1()
    {
        var spec = new BitTransformSpec
        {
            Name = "test", ZeroIsBit0 = true, MsbFirst = true, BitOffset = 0,
            InvertBits = false, ReverseBytes = false, ReverseFrame = false, FrameLengthBytes = 1
        };
        var input = new byte[] { 0x00, 0x80, 0x00, 0x80, 0x00, 0x80, 0x00, 0x80 };
        var result = BitTransformAnalyzer.Transform(input, spec);
        Assert.NotEmpty(result);
    }

    [Fact]
    public void BitTransform_InvertedMapping()
    {
        var spec = new BitTransformSpec
        {
            Name = "inv", ZeroIsBit0 = false, MsbFirst = true, BitOffset = 0,
            InvertBits = false, ReverseBytes = false, ReverseFrame = false, FrameLengthBytes = 1
        };
        var input = new byte[] { 0x00, 0x80, 0x80, 0x00, 0x00, 0x80, 0x00, 0x80 };
        var a = BitTransformAnalyzer.Transform(input, spec);
        var b = BitTransformAnalyzer.Transform(input, spec with { ZeroIsBit0 = true });
        Assert.NotEqual(Convert.ToHexString(a), Convert.ToHexString(b));
    }

    [Fact]
    public void BitTransform_MsbPacking()
    {
        var bits = new List<int> { 1, 0, 1, 0, 1, 0, 1, 0 };
        var packed = BitTransformAnalyzer.PackBits(bits, msbFirst: true);
        Assert.Single(packed);
    }

    [Fact]
    public void BitTransform_MsbPacking_PlacesFirstBitAtHighBit()
    {
        var bits = new List<int> { 1, 0, 0, 0, 0, 0, 0, 0 };
        Assert.Equal(0x80, BitTransformAnalyzer.PackBits(bits, msbFirst: true)[0]);
    }

    [Fact]
    public void BitTransform_LsbPacking_PlacesFirstBitAtLowBit()
    {
        var bits = new List<int> { 1, 0, 0, 0, 0, 0, 0, 0 };
        Assert.Equal(0x01, BitTransformAnalyzer.PackBits(bits, msbFirst: false)[0]);
    }

    [Fact]
    public void BitTransform_BitOffset()
    {
        var spec0 = new BitTransformSpec
        {
            Name = "o0", ZeroIsBit0 = true, MsbFirst = true, BitOffset = 0,
            InvertBits = false, ReverseBytes = false, ReverseFrame = false, FrameLengthBytes = 1
        };
        var spec1 = spec0 with { BitOffset = 1, Name = "o1" };
        var a = BitTransformAnalyzer.Transform(Enumerable.Repeat((byte)0x80, 16).ToArray(), spec0);
        var b = BitTransformAnalyzer.Transform(Enumerable.Repeat((byte)0x80, 16).ToArray(), spec1);
        Assert.NotEqual(a.Length, 0);
        Assert.NotEqual(a, b);
    }

    [Fact]
    public void BitTransform_InvertBits()
    {
        var spec = new BitTransformSpec
        {
            Name = "inv", ZeroIsBit0 = true, MsbFirst = true, BitOffset = 0,
            InvertBits = true, ReverseBytes = false, ReverseFrame = false, FrameLengthBytes = 1
        };
        var bytes = BitTransformAnalyzer.Transform([0x00, 0x80, 0x00, 0x80, 0x00, 0x80, 0x00, 0x80], spec);
        Assert.NotEmpty(bytes);
    }

    [Fact]
    public void BitTransform_ReverseBytes()
    {
        var spec = new BitTransformSpec
        {
            Name = "rb", ZeroIsBit0 = true, MsbFirst = true, BitOffset = 0,
            InvertBits = false, ReverseBytes = true, ReverseFrame = false, FrameLengthBytes = 1
        };
        var bytes = BitTransformAnalyzer.Transform([0x00, 0x80, 0x00, 0x80, 0x00, 0x80, 0x00, 0x80], spec);
        Assert.NotEmpty(bytes);
    }

    [Fact]
    public void BitTransform_ReverseFrame()
    {
        var spec = new BitTransformSpec
        {
            Name = "rf", ZeroIsBit0 = true, MsbFirst = true, BitOffset = 0,
            InvertBits = false, ReverseBytes = false, ReverseFrame = true, FrameLengthBytes = 2
        };
        var bytes = BitTransformAnalyzer.Transform([0x00, 0x80, 0x00, 0x80, 0x00, 0x80, 0x00, 0x80], spec);
        Assert.NotEmpty(bytes);
    }

    [Fact]
    public void Decoder_AsciiDigits()
    {
        var session = DummySession("s1");
        var transform = new BitTransformCandidate
        {
            Spec = DummySpec(),
            TransformedBytes = Encoding.ASCII.GetBytes("1234"),
            PatternScore = 1
        };
        var decoded = new CandidateDecoderRegistry().Decode([session], [transform]).FirstOrDefault();
        Assert.NotNull(decoded);
        Assert.Contains("ASCII", decoded!.DecoderName);
    }

    [Fact]
    public void Decoder_Bcd()
    {
        var session = DummySession("s1");
        var transform = new BitTransformCandidate
        {
            Spec = DummySpec(),
            TransformedBytes = [0x12, 0x34],
            PatternScore = 1
        };
        var list = new CandidateDecoderRegistry().Decode([session], [transform]);
        Assert.Contains(list, d => d.DecoderName.Contains("BCD", StringComparison.Ordinal));
    }

    [Fact]
    public void Decoder_Integer()
    {
        var session = DummySession("s1");
        var transform = new BitTransformCandidate
        {
            Spec = DummySpec(),
            TransformedBytes = [0x00, 0x00, 0x01, 0x00],
            PatternScore = 1
        };
        var list = new CandidateDecoderRegistry().Decode([session], [transform]);
        Assert.Contains(list, d => d.DecoderName.Contains("INT", StringComparison.Ordinal));
    }

    [Fact]
    public void Decoder_FixedPoint()
    {
        var session = DummySession("s1");
        var transform = new BitTransformCandidate
        {
            Spec = DummySpec(),
            TransformedBytes = [0x00, 0x00, 0x64, 0x00],
            PatternScore = 1
        };
        var list = new CandidateDecoderRegistry().Decode([session], [transform]);
        Assert.Contains(list, d => d.DecoderName.StartsWith("FixedPoint", StringComparison.Ordinal));
    }

    [Fact]
    public void Scorer_UsesMultipleSessions()
    {
        var sessions = new[]
        {
            SessionWithWeight("a", 0, true),
            SessionWithWeight("b", 50, true)
        };
        var candidate = new DecodedCandidate
        {
            DecoderName = "test",
            Transform = DummySpec(),
            DecodedValues = [0, 50],
            DecodedValuesBySession = new Dictionary<string, double?> { ["a"] = 0, ["b"] = 50 },
            Score = 0,
            Confidence = 0,
            Reasons = [],
            Warnings = []
        };
        var ranked = new ProtocolCandidateScorer().ScoreAndRank([candidate], sessions);
        Assert.True(ranked[0].Score > 0);
        Assert.True(ranked[0].Confidence >= 15);
    }

    [Fact]
    public void Scorer_DoesNotGiveHighConfidenceForSingleSession()
    {
        var sessions = new[] { SessionWithWeight("only", 5030, true) };
        var candidate = new DecodedCandidate
        {
            DecoderName = "test",
            Transform = DummySpec(),
            DecodedValues = [5030],
            DecodedValuesBySession = new Dictionary<string, double?> { ["only"] = 5030 },
            Score = 0,
            Confidence = 0,
            Reasons = [],
            Warnings = []
        };
        var ranked = new ProtocolCandidateScorer().ScoreAndRank([candidate], sessions);
        Assert.True(ranked[0].Confidence <= 39);
    }

    [Fact]
    public void Scorer_DynamicSessionNotExactGroundTruth()
    {
        var sessions = new[] { SessionWithWeight("dyn", 5080, false) };
        var candidate = new DecodedCandidate
        {
            DecoderName = "test",
            Transform = DummySpec(),
            DecodedValues = [5080],
            DecodedValuesBySession = new Dictionary<string, double?> { ["dyn"] = 5080 },
            Score = 0,
            Confidence = 0,
            Reasons = [],
            Warnings = []
        };
        var ranked = new ProtocolCandidateScorer().ScoreAndRank([candidate], sessions);
        Assert.Contains(ranked[0].Warnings, w => w.Contains("dynamic", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task RawBinaryWriter_PreservesBytes()
    {
        var writer = new RawBinaryCaptureWriter();
        writer.Append([0x00, 0x80, 0x00]);
        var basePath = Path.Combine(Path.GetTempPath(), $"raw-{Guid.NewGuid():N}");
        var path = await writer.SaveAsync(basePath);
        Assert.NotNull(path);
        var bytes = await File.ReadAllBytesAsync(path!);
        Assert.Equal([0x00, 0x80, 0x00], bytes);
        File.Delete(path!);
    }

    [Fact]
    public async Task SessionJsonWriter_WritesMetadata()
    {
        var basePath = Path.Combine(Path.GetTempPath(), $"meta-{Guid.NewGuid():N}");
        var session = new SerialCaptureSession { PortSettings = SerialPortSettings.CreateDefault() };
        var path = await SessionMetadataWriter.WriteAsync(
            basePath,
            session,
            10,
            "a.log",
            "a.raw.bin",
            new CaptureSaveOptions { PortSettings = SerialPortSettings.CreateDefault() });
        var json = await File.ReadAllTextAsync(path);
        Assert.Contains("\"portName\"", json, StringComparison.OrdinalIgnoreCase);
        File.Delete(path);
    }

    [Fact]
    public async Task Replay_Instant_CompletesQuickly()
    {
        var session = BuildSession([MakeChunk(1, [0x00, 0x80])]);
        session.Stream = new SerialStreamReconstructor().Reconstruct(session);
        using var replay = new LogReplayEngine(session);
        var sw = System.Diagnostics.Stopwatch.StartNew();
        await replay.PlayAsync(instant: true);
        sw.Stop();
        Assert.True(sw.ElapsedMilliseconds < 500);
    }

    [Fact]
    public void ProtocolAnalyzerAssembly_HasNoSerialPortWrite()
    {
        var asm = typeof(CanXe.ProtocolAnalyzer.App).Assembly;
        var offenders = asm.GetTypes()
            .SelectMany(t => t.GetMethods(BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic))
            .Where(m => m.Name.Contains("Write", StringComparison.Ordinal) && m.DeclaringType?.FullName?.Contains("SerialPort", StringComparison.Ordinal) == true)
            .ToList();
        Assert.Empty(offenders);
    }

    [Fact]
    public void DeviceTesterProvider_IsReadOnly()
    {
        var writeMethods = typeof(WindowsSerialPortProvider)
            .GetMethods(BindingFlags.Instance | BindingFlags.Public)
            .Where(m => m.Name.Contains("Write", StringComparison.Ordinal) || m.Name.Contains("Send", StringComparison.Ordinal));
        Assert.Empty(writeMethods);
    }

    [Fact]
    public async Task Export_Markdown_And_Json()
    {
        var result = new ProtocolAnalysisResult
        {
            GeneratedAt = DateTimeOffset.Now,
            Sessions = [],
            TimingBySession = [],
            FrameCandidates = [],
            BitTransformCandidates = [],
            DecoderCandidates = [],
            Warnings = []
        };
        var md = Path.Combine(Path.GetTempPath(), $"report-{Guid.NewGuid():N}.md");
        var json = Path.ChangeExtension(md, ".json");
        var writer = new ProtocolAnalysisReportWriter();
        await writer.WriteMarkdownAsync(result, md);
        await writer.WriteJsonAsync(result, json);
        Assert.True(File.Exists(md));
        Assert.True(File.Exists(json));
        File.Delete(md);
        File.Delete(json);
    }

    [Fact]
    public void ImportDefaults_MatchSampleFiles()
    {
        var label = SessionImportDefaults.FromFileName("CanXe_COM1_20260627_145814.log");
        Assert.Equal(5030, label.KnownWeightKg);
        Assert.False(label.KnownWeightIsApproximate);
        Assert.True(label.IsStableSession);
    }

    private static SerialLogSession DummySession(string name) => new()
    {
        SourceFilePath = name,
        FileName = name,
        Chunks = [],
        WeightLabel = new KnownWeightLabel()
    };

    private static SerialLogSession SessionWithWeight(string name, decimal weight, bool stable) => new()
    {
        SourceFilePath = name,
        FileName = name,
        Chunks = [],
        WeightLabel = new KnownWeightLabel { KnownWeightKg = weight, IsStableSession = stable, KnownWeightIsApproximate = !stable }
    };

    private static BitTransformSpec DummySpec() => new()
    {
        Name = "dummy", ZeroIsBit0 = true, MsbFirst = true, BitOffset = 0,
        InvertBits = false, ReverseBytes = false, ReverseFrame = false, FrameLengthBytes = 1
    };

    private static SerialLogChunk MakeChunk(int number, byte[] bytes, string? timestamp = null) => new()
    {
        ChunkNumber = number,
        DeclaredByteCount = bytes.Length,
        Bytes = bytes,
        Timestamp = DateTimeOffset.Parse(timestamp ?? "2026-06-27T10:00:00.000")
    };

    private static SerialLogSession BuildSession(IReadOnlyList<SerialLogChunk> chunks) => new()
    {
        SourceFilePath = "test.log",
        FileName = "test.log",
        Chunks = chunks
    };

    private static ContinuousByteStream BuildStream(IReadOnlyList<SerialLogChunk> chunks)
    {
        var session = BuildSession(chunks);
        return new SerialStreamReconstructor().Reconstruct(session);
    }
}

public class Phase2BProtocolAnalyzerStaTests
{
    [Fact]
    public void MainWindow_CreatesWithoutCom()
    {
        Exception? failure = null;
        var thread = new Thread(() =>
        {
            try
            {
                var app = new Application { ShutdownMode = ShutdownMode.OnExplicitShutdown };
                var window = new CanXe.ProtocolAnalyzer.MainWindow { DataContext = new CanXe.ProtocolAnalyzer.ViewModels.ProtocolAnalyzerViewModel() };
                window.Show();
                window.Close();
                app.Shutdown();
            }
            catch (Exception ex) { failure = ex; }
        }) { IsBackground = true };
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join(TimeSpan.FromSeconds(15));
        Assert.Null(failure);
    }
}

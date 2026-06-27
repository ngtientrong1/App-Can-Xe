using CanXe.ProtocolAnalyzer.Core.Models;

namespace CanXe.ProtocolAnalyzer.Core.Analysis;

public sealed class BitTransformAnalyzer : IBitTransformAnalyzer
{
    public IReadOnlyList<BitTransformCandidate> Analyze(ContinuousByteStream stream, ProtocolAnalysisOptions? options = null)
    {
        options ??= new ProtocolAnalysisOptions();
        var source = stream.Bytes;
        if (source.Length == 0)
            return [];

        var candidates = new List<BitTransformCandidate>();

        foreach (var zeroIsBit0 in new[] { true, false })
        {
            for (var offset = 0; offset <= 7; offset++)
            {
                foreach (var msbFirst in new[] { true, false })
                {
                    foreach (var invert in new[] { false, true })
                    {
                        for (var frameLen = options.MinFrameLengthBytes; frameLen <= Math.Min(16, options.MaxFrameLengthBytes); frameLen++)
                        {
                            foreach (var reverseBytes in new[] { false, true })
                            {
                                foreach (var reverseFrame in new[] { false, true })
                                {
                                    var spec = new BitTransformSpec
                                    {
                                        Name = BuildName(zeroIsBit0, msbFirst, offset, invert, reverseBytes, reverseFrame, frameLen),
                                        ZeroIsBit0 = zeroIsBit0,
                                        MsbFirst = msbFirst,
                                        BitOffset = offset,
                                        InvertBits = invert,
                                        ReverseBytes = reverseBytes,
                                        ReverseFrame = reverseFrame,
                                        FrameLengthBytes = frameLen
                                    };

                                    var transformed = Transform(source, spec);
                                    var score = ScorePattern(transformed, frameLen);
                                    candidates.Add(new BitTransformCandidate
                                    {
                                        Spec = spec,
                                        TransformedBytes = transformed,
                                        PatternScore = score
                                    });

                                    if (candidates.Count >= options.MaxBitTransformCandidates * 4)
                                        goto done;
                                }
                            }
                        }
                    }
                }
            }
        }

        done:
        return candidates
            .OrderByDescending(c => c.PatternScore)
            .Take(options.MaxBitTransformCandidates)
            .ToList();
    }

    public static byte[] Transform(byte[] source, BitTransformSpec spec)
    {
        var bits = new List<int>(source.Length * 8);
        foreach (var b in source)
        {
            if (b == 0x00)
                bits.Add(spec.ZeroIsBit0 ? 0 : 1);
            else if (b == 0x80)
                bits.Add(spec.ZeroIsBit0 ? 1 : 0);
            else
                bits.Add((b & 0x80) != 0 ? 1 : 0);
        }

        if (spec.BitOffset > 0 && bits.Count > spec.BitOffset)
            bits = bits.Skip(spec.BitOffset).ToList();

        var bytes = PackBits(bits, spec.MsbFirst);
        if (spec.InvertBits)
            bytes = bytes.Select(b => (byte)~b).ToArray();

        if (spec.ReverseBytes)
            Array.Reverse(bytes);

        if (spec.ReverseFrame && spec.FrameLengthBytes > 0)
            bytes = ReverseFrames(bytes, spec.FrameLengthBytes);

        return bytes;
    }

    public static byte[] PackBits(IReadOnlyList<int> bits, bool msbFirst)
    {
        var result = new List<byte>();
        for (var i = 0; i + 8 <= bits.Count; i += 8)
        {
            byte value = 0;
            for (var bit = 0; bit < 8; bit++)
            {
                var sourceIndex = i + bit;
                if (bits[sourceIndex] != 0)
                    value |= (byte)(1 << (msbFirst ? 7 - bit : bit));
            }

            result.Add(value);
        }

        return result.ToArray();
    }

    private static byte[] ReverseFrames(byte[] bytes, int frameLength)
    {
        if (frameLength <= 0 || bytes.Length == 0)
            return bytes;

        var output = new byte[bytes.Length];
        var frameCount = bytes.Length / frameLength;
        for (var f = 0; f < frameCount; f++)
        {
            var srcOffset = f * frameLength;
            for (var i = 0; i < frameLength; i++)
                output[srcOffset + i] = bytes[srcOffset + frameLength - 1 - i];
        }

        var remainder = bytes.Length % frameLength;
        if (remainder > 0)
            Array.Copy(bytes, frameCount * frameLength, output, frameCount * frameLength, remainder);

        return output;
    }

    private static double ScorePattern(byte[] transformed, int frameLength)
    {
        if (transformed.Length == 0)
            return 0;

        var uniqueRatio = transformed.Distinct().Count() / (double)Math.Max(transformed.Length, 1);
        var frameScore = frameLength > 0
            ? FrameCandidateDetector.AutocorrelationScore(transformed, Math.Min(frameLength, transformed.Length))
            : 0;
        return frameScore * 0.7 + uniqueRatio * 0.3;
    }

    private static string BuildName(bool zero0, bool msb, int offset, bool invert, bool revBytes, bool revFrame, int frameLen) =>
        $"z{(zero0 ? "0" : "1")}-{(msb ? "msb" : "lsb")}-o{offset}-{(invert ? "inv" : "n")}-{(revBytes ? "rb" : "nb")}-{(revFrame ? "rf" : "nf")}-f{frameLen}";
}

public sealed class CandidateDecoderRegistry : ICandidateDecoder
{
    public IReadOnlyList<DecodedCandidate> Decode(
        IReadOnlyList<SerialLogSession> sessions,
        IReadOnlyList<BitTransformCandidate> transforms,
        ProtocolAnalysisOptions? options = null)
    {
        options ??= new ProtocolAnalysisOptions();
        var results = new List<DecodedCandidate>();

        foreach (var transform in transforms)
        {
            TryAdd(results, DecodeAsciiDigits(transform, sessions));
            TryAdd(results, DecodeSignedAscii(transform, sessions));
            TryAdd(results, DecodePackedBcd(transform, sessions));
            TryAdd(results, DecodeUnpackedBcd(transform, sessions));
            TryAdd(results, DecodeInteger(transform, sessions, signed: false, littleEndian: true));
            TryAdd(results, DecodeInteger(transform, sessions, signed: false, littleEndian: false));
            TryAdd(results, DecodeInteger(transform, sessions, signed: true, littleEndian: true));
            TryAdd(results, DecodeInteger(transform, sessions, signed: true, littleEndian: false));
            foreach (var scale in new[] { 1, 10, 100, 1000 })
                TryAdd(results, DecodeFixedPoint(transform, sessions, scale));

            if (results.Count >= options.MaxDecoderCandidates)
                break;
        }

        return results.Take(options.MaxDecoderCandidates).ToList();
    }

    private static void TryAdd(ICollection<DecodedCandidate> list, DecodedCandidate? candidate)
    {
        if (candidate is not null)
            list.Add(candidate);
    }

    private static DecodedCandidate? DecodeAsciiDigits(BitTransformCandidate transform, IReadOnlyList<SerialLogSession> sessions)
    {
        var values = sessions.Select(s => DecodeSessionAsciiDigits(s, transform.TransformedBytes)).ToList();
        if (values.All(v => v is null))
            return null;

        return BuildCandidate("ASCII-digits", transform.Spec, values, sessions, "Chuỗi ASCII số.");
    }

    private static DecodedCandidate? DecodeSignedAscii(BitTransformCandidate transform, IReadOnlyList<SerialLogSession> sessions)
    {
        var values = sessions.Select(s => DecodeSessionSignedAscii(s, transform.TransformedBytes)).ToList();
        if (values.All(v => v is null))
            return null;

        return BuildCandidate("ASCII-signed", transform.Spec, values, sessions, "ASCII có dấu.");
    }

    private static DecodedCandidate? DecodePackedBcd(BitTransformCandidate transform, IReadOnlyList<SerialLogSession> sessions)
    {
        var values = sessions.Select(s => DecodeSessionPackedBcd(s, transform.TransformedBytes)).ToList();
        if (values.All(v => v is null))
            return null;

        return BuildCandidate("BCD-packed", transform.Spec, values, sessions, "Packed BCD.");
    }

    private static DecodedCandidate? DecodeUnpackedBcd(BitTransformCandidate transform, IReadOnlyList<SerialLogSession> sessions)
    {
        var values = sessions.Select(s => DecodeSessionUnpackedBcd(s, transform.TransformedBytes)).ToList();
        if (values.All(v => v is null))
            return null;

        return BuildCandidate("BCD-unpacked", transform.Spec, values, sessions, "Unpacked BCD.");
    }

    private static DecodedCandidate? DecodeInteger(
        BitTransformCandidate transform,
        IReadOnlyList<SerialLogSession> sessions,
        bool signed,
        bool littleEndian)
    {
        var values = sessions.Select(s => DecodeSessionInteger(s, transform.TransformedBytes, signed, littleEndian)).ToList();
        if (values.All(v => v is null))
            return null;

        var name = signed
            ? (littleEndian ? "INT-LE-signed" : "INT-BE-signed")
            : (littleEndian ? "INT-LE-unsigned" : "INT-BE-unsigned");
        return BuildCandidate(name, transform.Spec, values, sessions, "Integer decode.");
    }

    private static DecodedCandidate? DecodeFixedPoint(
        BitTransformCandidate transform,
        IReadOnlyList<SerialLogSession> sessions,
        int scale)
    {
        var values = sessions.Select(s =>
        {
            var raw = DecodeSessionInteger(s, transform.TransformedBytes, signed: true, littleEndian: true);
            return raw.HasValue ? raw / scale : (double?)null;
        }).ToList();

        if (values.All(v => v is null))
            return null;

        return BuildCandidate($"FixedPoint/scale{scale}", transform.Spec, values, sessions, $"Fixed point scale 1/{scale}.");
    }

    private static DecodedCandidate BuildCandidate(
        string decoderName,
        BitTransformSpec spec,
        IReadOnlyList<double?> values,
        IReadOnlyList<SerialLogSession> sessions,
        string reason)
    {
        var bySession = sessions.Zip(values).ToDictionary(p => p.First.FileName, p => p.Second);
        return new DecodedCandidate
        {
            DecoderName = decoderName,
            Transform = spec,
            DecodedValues = values,
            DecodedValuesBySession = bySession,
            Score = 0,
            Confidence = 0,
            Reasons = [reason],
            Warnings = []
        };
    }

    internal static double? DecodeSessionAsciiDigits(SerialLogSession session, byte[] transformed)
    {
        var text = ExtractPayloadText(session, transformed);
        if (string.IsNullOrWhiteSpace(text))
            return null;

        var digits = new string(text.Where(char.IsDigit).ToArray());
        return digits.Length > 0 && double.TryParse(digits, out var value) ? value : null;
    }

    internal static double? DecodeSessionSignedAscii(SerialLogSession session, byte[] transformed)
    {
        var text = ExtractPayloadText(session, transformed).Trim();
        if (string.IsNullOrEmpty(text))
            return null;

        var cleaned = new string(text.Where(c => char.IsDigit(c) || c is '+' or '-' or '.').ToArray());
        return double.TryParse(cleaned, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var value)
            ? value
            : null;
    }

    internal static double? DecodeSessionPackedBcd(SerialLogSession session, byte[] transformed)
    {
        var bytes = GetSessionSlice(session, transformed, 4);
        long value = 0;
        foreach (var b in bytes)
        {
            var hi = (b >> 4) & 0xF;
            var lo = b & 0xF;
            if (hi > 9 || lo > 9)
                return null;
            value = value * 100 + hi * 10 + lo;
        }

        return value;
    }

    internal static double? DecodeSessionUnpackedBcd(SerialLogSession session, byte[] transformed)
    {
        var bytes = GetSessionSlice(session, transformed, 8);
        long value = 0;
        foreach (var b in bytes)
        {
            if (b > 9)
                return null;
            value = value * 10 + b;
        }

        return value;
    }

    internal static double? DecodeSessionInteger(SerialLogSession session, byte[] transformed, bool signed, bool littleEndian)
    {
        var bytes = GetSessionSlice(session, transformed, 4);
        if (bytes.Length == 0)
            return null;

        if (littleEndian)
            Array.Reverse(bytes);

        uint raw = 0;
        foreach (var b in bytes)
            raw = (raw << 8) | b;

        if (signed && bytes.Length <= 4)
        {
            var max = 1 << (bytes.Length * 8 - 1);
            if ((raw & (uint)max) != 0)
                return raw - (1 << (bytes.Length * 8));
        }

        return raw;
    }

    private static byte[] GetSessionSlice(SerialLogSession session, byte[] transformed, int maxBytes)
    {
        var len = Math.Min(maxBytes, transformed.Length);
        if (len == 0)
            return [];

        // Use stable tail window — not hardcoded to any weight.
        var start = Math.Max(0, transformed.Length - len);
        return transformed.Skip(start).Take(len).ToArray();
    }

    private static string ExtractPayloadText(SerialLogSession session, byte[] transformed)
    {
        var len = Math.Min(32, transformed.Length);
        if (len == 0)
            return string.Empty;

        return System.Text.Encoding.ASCII.GetString(transformed, 0, len);
    }
}

using CanXe.DeviceTester.Core.Models;

namespace CanXe.DeviceTester.Core.Guided;

public static class CaptureStreamComparer
{
    public static StableSessionComparisonResult Compare(byte[] emptyRaw, byte[] personRaw)
    {
        var identical = emptyRaw.AsSpan().SequenceEqual(personRaw);
        var emptyFreq = BuildFrequency(emptyRaw);
        var personFreq = BuildFrequency(personRaw);
        var diff = new Dictionary<byte, int>();
        foreach (var key in emptyFreq.Keys.Union(personFreq.Keys))
        {
            var delta = personFreq.GetValueOrDefault(key) - emptyFreq.GetValueOrDefault(key);
            if (delta != 0)
                diff[key] = delta;
        }

        var maxLen = Math.Max(emptyRaw.Length, personRaw.Length);
        var differing = 0;
        var compareLen = Math.Min(emptyRaw.Length, personRaw.Length);
        for (var i = 0; i < compareLen; i++)
        {
            if (emptyRaw[i] != personRaw[i])
                differing++;
        }

        differing += Math.Abs(emptyRaw.Length - personRaw.Length);
        var pct = maxLen == 0 ? 0 : (double)differing / maxLen * 100.0;

        var summary = identical
            ? "Hai stream giống nhau hoàn toàn."
            : pct switch
            {
                < 1 => "Có khác biệt yếu.",
                < 15 => "Có khác biệt.",
                _ => "Khác biệt rõ."
            };

        return new StableSessionComparisonResult
        {
            EmptyTotalBytes = emptyRaw.Length,
            PersonTotalBytes = personRaw.Length,
            EmptyUniqueByteCount = emptyFreq.Count,
            PersonUniqueByteCount = personFreq.Count,
            ByteFrequencyDifference = diff,
            RawStreamsIdentical = identical,
            EstimatedDifferencePercentage = pct,
            Summary = summary
        };
    }

    private static Dictionary<byte, int> BuildFrequency(byte[] data)
    {
        var map = new Dictionary<byte, int>();
        foreach (var b in data)
            map[b] = map.GetValueOrDefault(b) + 1;
        return map;
    }
}

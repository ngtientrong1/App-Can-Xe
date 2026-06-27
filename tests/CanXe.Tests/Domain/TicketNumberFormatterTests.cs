using CanXe.Domain.Services;

namespace CanXe.Tests.Domain;

public class TicketNumberFormatterTests
{
    [Theory]
    [InlineData(1, "01")]
    [InlineData(6, "06")]
    [InlineData(10, "10")]
    [InlineData(99, "99")]
    [InlineData(100, "100")]
    [InlineData(1000, "1000")]
    public void FormatDisplayNumber_UsesMinimumTwoDigits(int sequence, string expectedSequencePart)
    {
        var formatted = TicketNumberFormatter.FormatDisplayNumber(sequence, 6);
        Assert.Equal($"{expectedSequencePart}/06", formatted);
    }

    [Fact]
    public void FormatDisplayNumber_Sequence6Month6_Is06Over06()
    {
        Assert.Equal("06/06", TicketNumberFormatter.FormatDisplayNumber(6, 6));
    }

    [Theory]
    [InlineData("0006/06", 6, 6, "06/06")]
    [InlineData("0001/06", 1, 6, "01/06")]
    [InlineData("0100/06", 100, 6, "100/06")]
    public void ResolveDisplayNumber_NormalizesLegacyStoredValues(
        string stored,
        int sequence,
        int month,
        string expected)
    {
        Assert.Equal(expected, TicketNumberFormatter.ResolveDisplayNumber(sequence, month, stored));
    }

    [Fact]
    public void NormalizeStoredDisplayNumber_ParsesLegacyWithoutSequenceFields()
    {
        Assert.Equal("06/06", TicketNumberFormatter.NormalizeStoredDisplayNumber("0006/06"));
    }

    [Fact]
    public void FormatInternalCode_KeepsFourDigitSequenceForStorage()
    {
        Assert.Equal("202606-0006", TicketNumberFormatter.FormatInternalCode(2026, 6, 6));
    }
}

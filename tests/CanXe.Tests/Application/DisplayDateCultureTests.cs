using System.Globalization;
using CanXe.Application.Services;

namespace CanXe.Tests.Application;

public sealed class DisplayDateCultureTests
{
    private static CultureInfo WindowsLikeCulture(string shortDate, string separator)
    {
        var culture = (CultureInfo)new CultureInfo("en-US").Clone();
        culture.DateTimeFormat.ShortDatePattern = shortDate;
        culture.DateTimeFormat.DateSeparator = separator;
        return culture;
    }

    [Theory]
    [InlineData("dd-MM-yy", "-")]
    [InlineData("M/d/yyyy", "/")]
    [InlineData("yyyy.MM.dd", ".")]
    public void CustomFormat_AlwaysUsesSlashesAndDayFirst(string shortDate, string separator)
    {
        var culture = DisplayDateCulture.Create(WindowsLikeCulture(shortDate, separator));
        var date = new DateTime(2026, 10, 27, 7, 5, 9);

        Assert.Equal("27/10/2026", date.ToString("dd/MM/yyyy", culture));
        Assert.Equal("27/10/2026 07:05:09", date.ToString("dd/MM/yyyy HH:mm:ss", culture));
        Assert.Equal("27/10/2026", date.ToString("d", culture));
    }

    [Fact]
    public void NumbersKeepTheMachineFormatting()
    {
        var baseCulture = new CultureInfo("en-US");

        var culture = DisplayDateCulture.Create(baseCulture);

        Assert.Equal(1234567.5m.ToString("N1", baseCulture), 1234567.5m.ToString("N1", culture));
    }

    [Fact]
    public void BaseCultureIsNotModified()
    {
        var baseCulture = new CultureInfo("en-US");
        var before = baseCulture.DateTimeFormat.ShortDatePattern;

        DisplayDateCulture.Create(baseCulture);

        Assert.Equal(before, baseCulture.DateTimeFormat.ShortDatePattern);
    }
}

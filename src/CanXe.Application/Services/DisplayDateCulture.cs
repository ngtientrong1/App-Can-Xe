using System.Globalization;

namespace CanXe.Application.Services;

/// <summary>
/// Dates are always shown day/month/year with "/" and times as HH:mm:ss, whatever the Windows regional format is.
/// A custom format such as "dd/MM/yyyy" otherwise takes its "/" from the machine's date separator (a short date
/// of dd-MM-yy turns it into dd-MM-yyyy). Numbers keep the machine's own formatting.
/// </summary>
public static class DisplayDateCulture
{
    public static CultureInfo Create(CultureInfo baseCulture)
    {
        var culture = (CultureInfo)baseCulture.Clone();
        var format = culture.DateTimeFormat;
        format.DateSeparator = "/";
        format.TimeSeparator = ":";
        format.ShortDatePattern = "dd/MM/yyyy";
        format.ShortTimePattern = "HH:mm";
        format.LongTimePattern = "HH:mm:ss";
        return culture;
    }

    /// <summary>Makes the display culture the default for every thread of the process.</summary>
    public static void Apply()
    {
        var culture = Create(CultureInfo.CurrentCulture);
        CultureInfo.DefaultThreadCurrentCulture = culture;
        CultureInfo.CurrentCulture = culture;
    }
}

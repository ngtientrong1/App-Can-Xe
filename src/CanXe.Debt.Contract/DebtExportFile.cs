using System.Security.Cryptography;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace CanXe.Debt.Contract;

public sealed record StationInfo(string Code, string Name);

/// <summary>One weigh ticket as the debt app needs it. Weights in kg, money in VND.</summary>
public sealed record ExportedTicket(
    string Code,
    string DisplayNumber,
    DateTimeOffset DateTime,
    string? Customer,
    string? LicensePlate,
    string? Cargo,
    decimal? GrossWeightKg,
    decimal? TareWeightKg,
    decimal? NetWeightKg,
    decimal? BillableWeightKg,
    decimal? UnitPrice,
    decimal? SplitFirstWeightKg,
    decimal? SecondUnitPrice,
    long? AmountVnd,
    bool IsDeleted,
    bool AwaitingSecondWeigh = false,
    decimal? FirstWeightKg = null,
    string? Notes = null);

/// <summary>
/// A "Cân Dịch Vụ" ticket only weighs a truck for someone else: no purchase, so it belongs in neither the debt file
/// nor the reports. Both apps use this one rule so they never disagree about it.
/// </summary>
public static class ServiceWeigh
{
    public const string CustomerName = "Cân Dịch Vụ";

    public static bool IsServiceCustomer(string? customerName)
    {
        if (string.IsNullOrWhiteSpace(customerName))
            return false;

        var decomposed = customerName.Normalize(NormalizationForm.FormD);
        var sb = new StringBuilder(decomposed.Length);
        var pendingSpace = false;
        foreach (var ch in decomposed)
        {
            if (System.Globalization.CharUnicodeInfo.GetUnicodeCategory(ch) == System.Globalization.UnicodeCategory.NonSpacingMark)
                continue;

            var c = ch is 'đ' or 'Đ' ? 'd' : char.ToLowerInvariant(ch);
            if (char.IsLetterOrDigit(c))
            {
                if (pendingSpace && sb.Length > 0)
                    sb.Append(' ');
                sb.Append(c);
                pendingSpace = false;
            }
            else
            {
                pendingSpace = true;
            }
        }

        return sb.ToString() == "can dich vu";
    }
}

public sealed record DebtExportFile(
    string Format,
    int Version,
    StationInfo Station,
    DateTimeOffset ExportedAt,
    IReadOnlyList<ExportedTicket> Tickets,
    string Checksum)
{
    public const string FormatId = "canxe-debt-export";
    public const int CurrentVersion = 1;

    [JsonIgnore]
    public bool IsChecksumValid =>
        string.Equals(Checksum, DebtExportSerializer.ComputeChecksum(Tickets), StringComparison.OrdinalIgnoreCase);

    public static DebtExportFile Create(StationInfo station, DateTimeOffset exportedAt, IReadOnlyList<ExportedTicket> tickets) =>
        new(FormatId, CurrentVersion, station, exportedAt, tickets, DebtExportSerializer.ComputeChecksum(tickets));
}

public static class DebtExportSerializer
{
    private static readonly JsonSerializerOptions CompactOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };

    private static readonly JsonSerializerOptions FileOptions = new(CompactOptions) { WriteIndented = true };

    public static string Serialize(DebtExportFile file) => JsonSerializer.Serialize(file, FileOptions);

    public static DebtExportFile? Deserialize(string json) => JsonSerializer.Deserialize<DebtExportFile>(json, CompactOptions);

    public static string ComputeChecksum(IReadOnlyList<ExportedTicket> tickets) =>
        Hash(JsonSerializer.Serialize(tickets, CompactOptions));

    /// <summary>Short fingerprint of one ticket, used to tell changed tickets from unchanged ones.</summary>
    public static string ComputeTicketHash(ExportedTicket ticket) =>
        Hash(JsonSerializer.Serialize(ticket, CompactOptions))[..16];

    public static string SuggestFileName(StationInfo station, DateTimeOffset exportedAt) =>
        $"CanXe-{station.Code}-{exportedAt:yyyyMMdd-HHmm}.json";

    private static string Hash(string text) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(text)));
}

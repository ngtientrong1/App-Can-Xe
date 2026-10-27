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

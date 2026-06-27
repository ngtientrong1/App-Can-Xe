using CanXe.Domain.Entities;

namespace CanXe.Application.Services;

public static class TicketEditAuditBuilder
{
    public static AuditLog? BuildChange(
        int ticketId,
        string fieldName,
        string? oldValue,
        string? newValue,
        string? reason,
        string? editedBy,
        bool isDeveloperOverride)
    {
        if (string.Equals(oldValue, newValue, StringComparison.Ordinal))
            return null;

        return new AuditLog
        {
            TicketId = ticketId,
            FieldName = fieldName,
            OldValue = oldValue,
            NewValue = newValue,
            Reason = reason,
            EditedAt = DateTimeOffset.Now,
            EditedBy = editedBy,
            IsDeveloperOverride = isDeveloperOverride
        };
    }

    public static string? FormatDecimal(decimal? value) =>
        value?.ToString("G29", System.Globalization.CultureInfo.InvariantCulture);

    public static string? FormatWeight(decimal? kg) =>
        kg?.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture);
}

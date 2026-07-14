using CanXe.Application.Interfaces;

using CanXe.Application.Mapping;

using CanXe.Application.Models;

using CanXe.Domain.Entities;

using CanXe.Domain.Services;



namespace CanXe.Infrastructure.Services;



public sealed class ReportService(

    IWeighTicketRepository tickets,

    IExcelReportExporter exporter) : IReportService

{

    public async Task<ReportResult> QueryAsync(ReportFilter filter, CancellationToken cancellationToken = default)

    {

        var (fromInclusive, toExclusive) = ReportDateRange.Normalize(filter);



        var ticketFilter = new WeighTicketFilter

        {

            CustomerName = filter.CustomerKeyword,

            LicensePlate = filter.LicensePlateKeyword,

            CargoTypeName = filter.CargoTypeKeyword,

            MaxResults = filter.MaxResults > 0 ? filter.MaxResults : 5000

        };



        var rows = await tickets.GetFilteredAsync(ticketFilter, cancellationToken);

        if (!filter.IncludeDeleted)

            rows = rows.Where(t => !t.IsDeleted).ToList();



        rows = rows

            .Where(t => ReportDateRange.MatchesTicketDate(t.TicketDateTime, fromInclusive, toExclusive))

            .Where(t => MatchesWorkflowFilter(t, filter.State))

            .ToList();



        var mapped = rows.Select(MapRow).ToList();



        return new ReportResult

        {

            Summary = BuildSummary(mapped),

            Rows = mapped

        };

    }



    public async Task<ExportResult> ExportExcelAsync(

        ReportFilter filter,

        string filePath,

        string? stationName,

        CancellationToken cancellationToken = default)

    {

        try

        {

            var report = await QueryAsync(filter, cancellationToken);

            exporter.Export(report, filter, filePath, stationName);

            return ExportResult.Succeeded(filePath);

        }

        catch (Exception ex)

        {

            return ExportResult.Failed(ex.Message);

        }

    }



    private static ReportRowDto MapRow(WeighTicket ticket)

    {

        var state = WeighTicketWorkflow.FromEventCount(ticket.Events.Count);

        return new ReportRowDto

        {

            TicketId = ticket.Id,

            TicketDateTime = ticket.TicketDateTime,

            DisplayNumber = ticket.DisplayNumber,

            WorkflowStatusText = WeighTicketWorkflow.ListColumnText(state),

            CustomerName = ticket.CustomerNameSnapshot,

            LicensePlate = ticket.LicensePlateSnapshot,

            CargoTypeName = ticket.CargoTypeNameSnapshot,

            GrossWeightKg = WeightStorageMapper.FromGrams(ticket.GrossWeightGrams),

            TareWeightKg = WeightStorageMapper.FromGrams(ticket.TareWeightGrams),

            NetWeightKg = WeightStorageMapper.FromGrams(ticket.NetWeightGrams),

            BillableWeightKg = WeightStorageMapper.FromGrams(ticket.BillableWeightGrams),

            UnitPriceVndPerKg = ticket.UnitPriceVndPerKg,

            TotalAmountVnd = ticket.TotalAmountVnd,

            Notes = ticket.Notes

        };

    }



    private static bool MatchesWorkflowFilter(WeighTicket ticket, ReportWorkflowFilter state)

    {

        var workflowState = WeighTicketWorkflow.FromEventCount(ticket.Events.Count);

        return state switch

        {

            ReportWorkflowFilter.All => true,

            ReportWorkflowFilter.Completed => workflowState == WeighTicketWorkflowState.Completed,

            ReportWorkflowFilter.AwaitingSecondWeigh =>

                workflowState == WeighTicketWorkflowState.AwaitingSecondWeigh,

            ReportWorkflowFilter.MissingPrice =>

                WeightStorageMapper.FromGrams(ticket.NetWeightGrams) is decimal net &&

                !WeightCalculator.HasBillableUnitPrice(ticket.UnitPriceVndPerKg),

            _ => true

        };

    }



    private static ReportSummaryDto BuildSummary(IReadOnlyList<ReportRowDto> rows) =>

        new()

        {

            TicketCount = rows.Count,

            TotalGrossWeightKg = rows.Where(r => r.GrossWeightKg.HasValue).Sum(r => r.GrossWeightKg!.Value),

            TotalTareWeightKg = rows.Where(r => r.TareWeightKg.HasValue).Sum(r => r.TareWeightKg!.Value),

            TotalNetWeightKg = rows.Where(r => r.NetWeightKg.HasValue).Sum(r => r.NetWeightKg!.Value),

            TotalBillableWeightKg = rows.Where(r => r.BillableWeightKg.HasValue).Sum(r => r.BillableWeightKg!.Value),

            TotalAmountVnd = rows.Where(r => r.TotalAmountVnd.HasValue).Sum(r => r.TotalAmountVnd!.Value),

            MissingPriceCount = rows.Count(r =>

                r.NetWeightKg.HasValue && !WeightCalculator.HasBillableUnitPrice(r.UnitPriceVndPerKg)),

            AwaitingSecondWeighCount = rows.Count(r =>

                r.WorkflowStatusText.Contains("CHỜ CÂN LẦN 2", StringComparison.Ordinal))

        };

}



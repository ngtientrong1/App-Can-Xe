using CanXe.Application.Interfaces;
using CanXe.Application.Models;
using CanXe.Application.Services;
using CanXe.Domain.Services;
using CanXe.Tests.Support;
using Microsoft.Extensions.DependencyInjection;

namespace CanXe.Tests.Application;

public class Phase2ETicketNumberPresentationTests
{
    [Fact]
    public async Task PreviewSaveAndList_UseSameDisplayFormatter()
    {
        await using var factory = new TestApplicationFactory();
        await factory.InitializeAsync();
        using var scope = factory.Provider.CreateScope();
        var service = scope.ServiceProvider.GetRequiredService<WeighTicketService>();

        var preview = await service.GetPreviewDisplayNumberAsync();
        Assert.Matches(@"^\d{2,}/\d{2}$", preview);

        var draft = new WeighTicketDraft();
        var scale = scope.ServiceProvider.GetRequiredService<IScaleService>();
        scale.SetManualMode(true);
        scale.SetManualWeightKg(5000m);
        await service.CaptureWeightAsync(draft, 1);

        var saved = await service.SaveAsync(draft);
        Assert.True(saved.Success, saved.ErrorMessage);

        var list = await service.GetFilteredAsync(new WeighTicketFilter());
        Assert.Equal(saved.SavedTicket!.DisplayNumber, list[0].DisplayNumber);
        Assert.DoesNotContain("000", list[0].DisplayNumber, StringComparison.Ordinal);

        var detail = await service.GetTicketDetailAsync(saved.SavedTicket.Id);
        Assert.Equal(list[0].DisplayNumber, detail.DisplayNumber);
    }

    [Fact]
    public async Task LegacyStoredDisplayNumber_IsNormalizedOnRead()
    {
        await using var factory = new TestApplicationFactory();
        await factory.InitializeAsync();
        using var scope = factory.Provider.CreateScope();
        var repository = scope.ServiceProvider.GetRequiredService<CanXe.Application.Interfaces.IWeighTicketRepository>();

        var ticket = await repository.AddAsync(new CanXe.Domain.Entities.WeighTicket
        {
            SequenceNumber = 6,
            TicketYear = 2026,
            TicketMonth = 6,
            InternalCode = "202606-0006",
            DisplayNumber = "0006/06",
            TicketDateTime = DateTimeOffset.Now,
            CreatedAt = DateTimeOffset.Now
        });

        var service = scope.ServiceProvider.GetRequiredService<WeighTicketService>();
        var detail = await service.GetTicketDetailAsync(ticket.Id);

        Assert.Equal("06/06", detail.DisplayNumber);
        Assert.Equal("0006/06", ticket.DisplayNumber);
    }
}

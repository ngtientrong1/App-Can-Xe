using CanXe.Application.Configuration;
using CanXe.Application.Interfaces;
using CanXe.Application.Models;
using CanXe.Application.Services;
using CanXe.Domain.Models;
using CanXe.Domain.Services;
using CanXe.Infrastructure.Data;
using CanXe.Infrastructure.Logging;
using CanXe.Infrastructure.Services;
using CanXe.Tests.Support;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace CanXe.Tests.Application;

[CollectionDefinition("Phase6AdminAuditLog")]
public sealed class Phase6AdminAuditLogCollection;

[Collection("Phase6AdminAuditLog")]
public sealed class Phase6Rc1AdminAuthTests
{
    [Fact]
    public void Startup_AdminLocked()
    {
        var auth = new AdminAuthorizationService(new AppSettings());
        Assert.False(auth.IsAdminUnlocked);
    }

    [Fact]
    public void WrongAdminPassword_DoesNotUnlock()
    {
        var auth = new AdminAuthorizationService(new AppSettings { AdminUnlockCode = "admin123" });
        var result = auth.TryUnlock("wrong");
        Assert.False(result.Success);
        Assert.False(auth.IsAdminUnlocked);
    }

    [Fact]
    public void CorrectAdminPassword_Unlocks()
    {
        var auth = new AdminAuthorizationService(new AppSettings { AdminUnlockCode = "admin123" });
        var result = auth.TryUnlock("admin123");
        Assert.True(result.Success);
        Assert.True(auth.IsAdminUnlocked);
    }

    [Fact]
    public void ManualLock_LocksAdmin()
    {
        var auth = new AdminAuthorizationService(new AppSettings());
        Assert.True(auth.TryUnlock("admin123").Success);
        auth.Lock("manual");
        Assert.False(auth.IsAdminUnlocked);
    }

    [Fact]
    public void Password_NotLogged()
    {
        var path = AdminAuditLogger.LogFilePath;
        var before = File.Exists(path) ? File.ReadAllText(path) : string.Empty;
        var auth = new AdminAuthorizationService(new AppSettings { AdminUnlockCode = "admin123" });
        auth.TryUnlock("admin123");
        auth.TryUnlock("secret-pass-value");
        var after = File.ReadAllText(path);
        var delta = after.Length >= before.Length ? after[before.Length..] : after;
        Assert.DoesNotContain("admin123", delta, StringComparison.Ordinal);
        Assert.DoesNotContain("secret-pass-value", delta, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Timeout_LocksAdmin()
    {
        using var auth = new AdminAuthorizationService(new AppSettings(), idleTimeout: TimeSpan.FromMilliseconds(80));
        Assert.True(auth.TryUnlock("admin123").Success);
        await Task.Delay(200);
        auth.CheckIdleTimeoutNow();
        Assert.False(auth.IsAdminUnlocked);
    }

    [Fact]
    public async Task Timeout_DoesNotLock_WhenActivityTouched()
    {
        using var auth = new AdminAuthorizationService(new AppSettings(), idleTimeout: TimeSpan.FromMilliseconds(150));
        Assert.True(auth.TryUnlock("admin123").Success);
        for (var i = 0; i < 4; i++)
        {
            await Task.Delay(40);
            auth.TouchAdminActivity();
        }

        auth.CheckIdleTimeoutNow();
        Assert.True(auth.IsAdminUnlocked);
    }
}

[Collection("Phase6AdminAuditLog")]
public sealed class Phase6Rc1PermissionTests
{
    [Theory]
    [InlineData(AdminPermission.CanManualWeigh)]
    [InlineData(AdminPermission.CanEditCompletedTicket)]
    [InlineData(AdminPermission.CanDeleteTicket)]
    [InlineData(AdminPermission.CanAddCatalogItem)]
    [InlineData(AdminPermission.CanEditCatalogItem)]
    [InlineData(AdminPermission.CanDeleteCatalogItem)]
    public void Operator_CannotUseAdminPermissions(AdminPermission permission)
    {
        var auth = new AdminAuthorizationService(new AppSettings());
        var permissions = new UserPermissionService(auth);
        Assert.False(permissions.HasPermission(permission));
        var gate = permissions.EnsurePermission(permission, permission.ToString());
        Assert.False(gate.Allowed);
    }

    [Fact]
    public void Admin_CanUseAdminPermissions()
    {
        var auth = new AdminAuthorizationService(new AppSettings());
        Assert.True(auth.TryUnlock("admin123").Success);
        var permissions = new UserPermissionService(auth);
        Assert.True(permissions.HasPermission(AdminPermission.CanManualWeigh));
        Assert.True(permissions.HasPermission(AdminPermission.CanAddCatalogItem));
        Assert.Equal(StationUserRole.Admin, permissions.CurrentRole);
    }

    [Fact]
    public void Backend_RejectsUnauthorizedCommand()
    {
        var auth = new AdminAuthorizationService(new AppSettings());
        var permissions = new UserPermissionService(auth);
        var gate = permissions.EnsurePermission(AdminPermission.CanDeleteTicket, "DELETE_TICKET");
        Assert.False(gate.Allowed);
        Assert.Contains("Admin", gate.ErrorMessage!, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void AdminUnlockSuccess_AndFail_Logged()
    {
        var path = AdminAuditLogger.LogFilePath;
        var before = File.Exists(path) ? File.ReadAllText(path) : string.Empty;
        var auth = new AdminAuthorizationService(new AppSettings());
        auth.TryUnlock("bad");
        auth.TryUnlock("admin123");
        auth.Lock("manual");
        var after = File.ReadAllText(path);
        var delta = after.Length >= before.Length ? after[before.Length..] : after;
        Assert.Contains("ADMIN_UNLOCK", delta, StringComparison.Ordinal);
        Assert.Contains("FAIL", delta, StringComparison.Ordinal);
        Assert.Contains("SUCCESS", delta, StringComparison.Ordinal);
        Assert.Contains("ADMIN_LOCK", delta, StringComparison.Ordinal);
    }

    [Fact]
    public void PermissionDenied_Logged()
    {
        var path = AdminAuditLogger.LogFilePath;
        var before = File.Exists(path) ? File.ReadAllText(path) : string.Empty;
        var permissions = new UserPermissionService(new AdminAuthorizationService(new AppSettings()));
        permissions.EnsurePermission(AdminPermission.CanManualWeigh, "MANUAL_WEIGH");
        var after = File.ReadAllText(path);
        var delta = after.Length >= before.Length ? after[before.Length..] : after;
        Assert.Contains("PERMISSION_DENIED", delta, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(AdminPermission.CanAddCatalogItem, "CATALOG_ADD")]
    [InlineData(AdminPermission.CanEditCatalogItem, "CATALOG_EDIT")]
    [InlineData(AdminPermission.CanDeleteCatalogItem, "CATALOG_DELETE")]
    public void Operator_CatalogDenied_HasAdminMessage(AdminPermission permission, string action)
    {
        var permissions = new UserPermissionService(new AdminAuthorizationService(new AppSettings()));
        var gate = permissions.EnsurePermission(permission, action);
        Assert.False(gate.Allowed);
        Assert.Contains("Admin", gate.ErrorMessage!, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData("EDIT_COMPLETED_TICKET", AdminPermission.CanEditCompletedTicket)]
    [InlineData("DELETE_TICKET", AdminPermission.CanDeleteTicket)]
    public void Operator_TicketAdminActions_DeniedAndLogged(string action, AdminPermission permission)
    {
        var path = AdminAuditLogger.LogFilePath;
        var before = File.Exists(path) ? File.ReadAllText(path) : string.Empty;
        var permissions = new UserPermissionService(new AdminAuthorizationService(new AppSettings()));
        var gate = permissions.EnsurePermission(permission, action);
        Assert.False(gate.Allowed);
        var after = File.ReadAllText(path);
        var delta = after.Length >= before.Length ? after[before.Length..] : after;
        Assert.Contains("PERMISSION_DENIED", delta, StringComparison.Ordinal);
        Assert.Contains(action, delta, StringComparison.Ordinal);
    }
}

[Collection("Phase6AdminAuditLog")]
public sealed class Phase6Rc1AuditLoggingTests
{
    [Theory]
    [InlineData("MANUAL_WEIGH_W1", 1)]
    [InlineData("MANUAL_WEIGH_W2", 2)]
    public void ManualWeigh_AuditWrite_HasExpectedFormat(string action, int sequence)
    {
        var path = AdminAuditLogger.LogFilePath;
        var before = File.Exists(path) ? File.ReadAllText(path) : string.Empty;
        AdminAuditLogger.Write(
            action,
            "SUCCESS",
            "Admin",
            ticketId: "42",
            reason: "COM lỗi",
            note: $"kg=9420;seq={sequence}");
        var after = File.ReadAllText(path);
        var delta = after.Length >= before.Length ? after[before.Length..] : after;
        Assert.Contains(action, delta, StringComparison.Ordinal);
        Assert.Contains("SUCCESS", delta, StringComparison.Ordinal);
        Assert.Contains("Admin", delta, StringComparison.Ordinal);
        Assert.Contains("42", delta, StringComparison.Ordinal);
        Assert.Contains("COM lỗi", delta, StringComparison.Ordinal);
        Assert.Contains("kg=9420", delta, StringComparison.Ordinal);
    }

    [Fact]
    public void Audit_DoesNotContainPassword()
    {
        var path = AdminAuditLogger.LogFilePath;
        var before = File.Exists(path) ? File.ReadAllText(path) : string.Empty;
        AdminAuditLogger.Write(
            "ADMIN_UNLOCK",
            "FAIL",
            "Operator",
            reason: "admin123",
            note: "password=secret-pass-value");
        var after = File.ReadAllText(path);
        var delta = after.Length >= before.Length ? after[before.Length..] : after;
        Assert.DoesNotContain("admin123", delta, StringComparison.Ordinal);
        Assert.DoesNotContain("secret-pass-value", delta, StringComparison.Ordinal);
        Assert.Contains("[redacted]", delta, StringComparison.Ordinal);
    }
}

public sealed class Phase6Rc1ManualSimulationInputTests
{
    [Theory]
    [InlineData("9420", 9420)]
    [InlineData("9.420", 9420)]
    [InlineData("9,420", 9420)]
    public void VietnameseWeightFormats_AreAccepted(string text, int expectedKg)
    {
        Assert.True(ManualSimulationInputHelper.TryParseKg(text, out var kg, out var error));
        Assert.Equal(expectedKg, kg);
        Assert.Null(error);
    }
}

[Collection("CanXeDatabase")]
public sealed class Phase6Rc1ManualWeighAndMigrationTests : IAsyncLifetime
{
    private TestApplicationFactory _factory = null!;

    public async Task InitializeAsync()
    {
        _factory = new TestApplicationFactory();
        await _factory.InitializeAsync();
    }

    public async Task DisposeAsync() => await _factory.DisposeAsync();

    [Fact]
    public async Task ManualW1_SetsWeight1TimestampAndCapturedFlag()
    {
        await using var scope = _factory.Provider.CreateAsyncScope();
        var service = scope.ServiceProvider.GetRequiredService<WeighTicketService>();
        var draft = new WeighTicketDraft { DraftCustomer = "Admin manual" };
        var result = await service.CaptureManualWeightAsync(draft, 1, 9420m, "COM lỗi", StationUserRole.Admin);
        Assert.True(result.Success, result.ErrorMessage);
        Assert.True(draft.HasCapturedWeight1);
        Assert.Equal(9420m, draft.DraftWeight1);
        Assert.NotNull(draft.DraftWeight1RecordedAt);
        Assert.Equal(WeighInputSource.Manual, draft.DraftWeight1InputSource);
        Assert.Equal("COM lỗi", draft.DraftWeight1ManualReason);
    }

    [Fact]
    public async Task ManualW2_SetsWeight2TimestampAndCapturedFlag()
    {
        await using var scope = _factory.Provider.CreateAsyncScope();
        var service = scope.ServiceProvider.GetRequiredService<WeighTicketService>();
        var draft = new WeighTicketDraft();
        Assert.True((await service.CaptureManualWeightAsync(draft, 1, 10000m, "r", StationUserRole.Admin)).Success);
        Assert.True((await service.CaptureManualWeightAsync(draft, 2, 4000m, "r2", StationUserRole.Admin)).Success);
        Assert.True(draft.HasCapturedWeight2);
        Assert.Equal(WeighInputSource.Manual, draft.DraftWeight2InputSource);
    }

    [Fact]
    public async Task ManualW1_Save_CreatesAwaitingSecondWeigh()
    {
        await using var scope = _factory.Provider.CreateAsyncScope();
        var service = scope.ServiceProvider.GetRequiredService<WeighTicketService>();
        var draft = new WeighTicketDraft { DraftCustomer = "Manual W1 only" };
        Assert.True((await service.CaptureManualWeightAsync(draft, 1, 18000m, "lỗi cân", StationUserRole.Admin)).Success);
        var save = await service.SaveAsync(draft);
        Assert.True(save.Success, save.ErrorMessage);
        Assert.Equal(WeighTicketWorkflowState.AwaitingSecondWeigh, save.WorkflowState);
    }

    [Fact]
    public async Task ManualW1W2_Save_CreatesCompletedTicket()
    {
        await using var scope = _factory.Provider.CreateAsyncScope();
        var service = scope.ServiceProvider.GetRequiredService<WeighTicketService>();
        var draft = new WeighTicketDraft { DraftCustomer = "Manual complete", DraftVehicle = "51Z-11111" };
        Assert.True((await service.CaptureManualWeightAsync(draft, 1, 18000m, "lỗi cân", StationUserRole.Admin)).Success);
        Assert.True((await service.CaptureManualWeightAsync(draft, 2, 9000m, "lỗi cân", StationUserRole.Admin)).Success);
        var save = await service.SaveAsync(draft);
        Assert.True(save.Success, save.ErrorMessage);
        Assert.Equal(WeighTicketWorkflowState.Completed, save.WorkflowState);
    }

    [Fact]
    public async Task ManualEvent_SavesInputSourceManual_AndReason()
    {
        await using var scope = _factory.Provider.CreateAsyncScope();
        var service = scope.ServiceProvider.GetRequiredService<WeighTicketService>();
        var tickets = scope.ServiceProvider.GetRequiredService<IWeighTicketRepository>();
        var draft = new WeighTicketDraft { DraftCustomer = "Manual source" };
        Assert.True((await service.CaptureManualWeightAsync(draft, 1, 10000m, "lý do rc1", StationUserRole.Admin)).Success);
        Assert.True((await service.CaptureManualWeightAsync(draft, 2, 4000m, "lý do rc1", StationUserRole.Admin)).Success);
        var save = await service.SaveAsync(draft);
        Assert.True(save.Success, save.ErrorMessage);

        var entity = await tickets.GetByIdWithEventsAsync(save.SavedTicket!.Id);
        Assert.NotNull(entity);
        var w1 = entity!.Events.Single(e => e.Sequence == 1);
        Assert.Equal(WeighInputSource.Manual, w1.InputSource);
        Assert.Equal("lý do rc1", w1.ManualReason);
        Assert.Equal(StationUserRole.Admin, w1.CreatedByRole);
    }

    [Fact]
    public async Task HardwareEvent_DefaultsToHardware()
    {
        await using var scope = _factory.Provider.CreateAsyncScope();
        var service = scope.ServiceProvider.GetRequiredService<WeighTicketService>();
        var tickets = scope.ServiceProvider.GetRequiredService<IWeighTicketRepository>();
        var scale = scope.ServiceProvider.GetRequiredService<IScaleService>();
        scale.SetManualMode(true);
        scale.SetManualWeightKg(12000m);
        var draft = new WeighTicketDraft { DraftCustomer = "Hardware source" };
        await service.CaptureWeightAsync(draft, 1);
        scale.SetManualWeightKg(5000m);
        await service.CaptureWeightAsync(draft, 2);
        var save = await service.SaveAsync(draft);
        Assert.True(save.Success, save.ErrorMessage);
        var entity = await tickets.GetByIdWithEventsAsync(save.SavedTicket!.Id);
        Assert.All(entity!.Events, e => Assert.Equal(WeighInputSource.Hardware, e.InputSource));
    }

    [Fact]
    public async Task ExistingDatabase_MigratesSafely_InputSourceColumns()
    {
        await using var scope = _factory.Provider.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<CanXeDbContext>();
        await DatabaseUpgrader.UpgradeAsync(db, _factory.DatabasePath);
        await DatabaseUpgrader.UpgradeAsync(db, _factory.DatabasePath);
    }

    [Fact]
    public async Task ManualWeight_RequiresNonNegativeNumber_ReasonOptional()
    {
        await using var scope = _factory.Provider.CreateAsyncScope();
        var service = scope.ServiceProvider.GetRequiredService<WeighTicketService>();
        var draft = new WeighTicketDraft();
        var neg = await service.CaptureManualWeightAsync(draft, 1, -1m, "x", StationUserRole.Admin);
        Assert.False(neg.Success);
        var noReason = await service.CaptureManualWeightAsync(draft, 1, 100m, "  ", StationUserRole.Admin);
        Assert.True(noReason.Success, noReason.ErrorMessage);
        Assert.Null(draft.DraftWeight1ManualReason);
    }

    [Fact]
    public async Task ManualReasonNullable_DoesNotFailSave()
    {
        await using var scope = _factory.Provider.CreateAsyncScope();
        var service = scope.ServiceProvider.GetRequiredService<WeighTicketService>();
        var draft = new WeighTicketDraft { DraftCustomer = "No reason" };
        Assert.True((await service.CaptureManualWeightAsync(draft, 1, 9000m, null, StationUserRole.Admin)).Success);
        Assert.True((await service.CaptureManualWeightAsync(draft, 2, 4000m, null, StationUserRole.Admin)).Success);
        var save = await service.SaveAsync(draft);
        Assert.True(save.Success, save.ErrorMessage);
    }

    [Fact]
    public async Task HardwareWeigh_StillWorks()
    {
        await using var scope = _factory.Provider.CreateAsyncScope();
        var service = scope.ServiceProvider.GetRequiredService<WeighTicketService>();
        var scale = scope.ServiceProvider.GetRequiredService<IScaleService>();
        scale.SetManualMode(true);
        var draft = new WeighTicketDraft { DraftCustomer = "HW still works" };
        scale.SetManualWeightKg(15000m);
        Assert.True((await service.CaptureWeightAsync(draft, 1)).Success);
        scale.SetManualWeightKg(7000m);
        Assert.True((await service.CaptureWeightAsync(draft, 2)).Success);
        var save = await service.SaveAsync(draft);
        Assert.True(save.Success, save.ErrorMessage);
    }

    [Fact]
    public async Task ExistingOldEvents_ReadAsHardware()
    {
        var dbPath = Path.Combine(Path.GetTempPath(), $"canxe-p6-legacy-{Guid.NewGuid():N}.db");
        var ticketId = await CreatePrePhase6DatabaseWithLegacyEventAsync(dbPath);

        await using var factory = new TestApplicationFactory(dbPath);
        await factory.InitializeAsync();

        await using var scope = factory.Provider.CreateAsyncScope();
        var tickets = scope.ServiceProvider.GetRequiredService<IWeighTicketRepository>();
        var entity = await tickets.GetByIdWithEventsAsync(ticketId);
        Assert.NotNull(entity);
        Assert.NotEmpty(entity!.Events);
        Assert.All(entity.Events, e => Assert.Equal(WeighInputSource.Hardware, e.InputSource));
        Assert.All(entity.Events, e => Assert.Equal(StationUserRole.Operator, e.CreatedByRole));
    }

    private static async Task<int> CreatePrePhase6DatabaseWithLegacyEventAsync(string dbPath)
    {
        await using var factory = new TestApplicationFactory(dbPath, deleteOnDispose: false);
        await factory.InitializeAsync();

        int ticketId;
        await using (var scope = factory.Provider.CreateAsyncScope())
        {
            var service = scope.ServiceProvider.GetRequiredService<WeighTicketService>();
            var scale = scope.ServiceProvider.GetRequiredService<IScaleService>();
            scale.SetManualMode(true);
            var draft = new WeighTicketDraft { DraftCustomer = "Legacy input source" };
            scale.SetManualWeightKg(12000m);
            await service.CaptureWeightAsync(draft, 1);
            var save = await service.SaveAsync(draft);
            Assert.True(save.Success, save.ErrorMessage);
            ticketId = save.SavedTicket!.Id;
        }

        await factory.DisposeAsync();

        await using var db = new CanXeDbContext(
            new DbContextOptionsBuilder<CanXeDbContext>().UseSqlite($"Data Source={dbPath}").Options);
        await db.Database.ExecuteSqlRawAsync(
            "DELETE FROM __EFMigrationsHistory WHERE MigrationId = {0}",
            DatabaseUpgrader.Phase6Rc1MigrationId);
        await db.Database.ExecuteSqlRawAsync("ALTER TABLE WeighEvents DROP COLUMN InputSource;");
        await db.Database.ExecuteSqlRawAsync("ALTER TABLE WeighEvents DROP COLUMN ManualReason;");
        await db.Database.ExecuteSqlRawAsync("ALTER TABLE WeighEvents DROP COLUMN CreatedByRole;");

        return ticketId;
    }
}

public sealed class Phase6Rc2InlineParseTests
{
    [Theory]
    [InlineData("9570", 9570)]
    [InlineData("9.570", 9570)]
    [InlineData("9,570", 9570)]
    [InlineData("9 570", 9570)]
    public void InlineWeight_AcceptsFormats(string text, int expected)
    {
        Assert.True(ManualSimulationInputHelper.TryParseKg(text, out var kg, out _));
        Assert.Equal(expected, kg);
    }

    [Theory]
    [InlineData("-1")]
    [InlineData("abc")]
    [InlineData("")]
    public void InlineWeight_RejectsInvalid(string text) =>
        Assert.False(ManualSimulationInputHelper.TryParseKg(text, out _, out _));
}

[Collection("Phase6AdminAuditLog")]
public sealed class Phase6Rc2PermissionInlineTests
{
    [Fact]
    public void AdminLocked_CannotEditWeightInline()
    {
        var auth = new AdminAuthorizationService(new AppSettings());
        var permissions = new UserPermissionService(auth);
        Assert.False(permissions.HasPermission(AdminPermission.CanManualWeigh));
    }

    [Fact]
    public void AdminUnlocked_CanEditWeightInline()
    {
        var auth = new AdminAuthorizationService(new AppSettings());
        Assert.True(auth.TryUnlock("admin123").Success);
        var permissions = new UserPermissionService(auth);
        Assert.True(permissions.HasPermission(AdminPermission.CanManualWeigh));
        Assert.True(permissions.HasPermission(AdminPermission.CanEditCompletedTicket));
    }

    [Fact]
    public void InlineManualDraftWeight_Logged()
    {
        var path = AdminAuditLogger.LogFilePath;
        var marker = "rc5-" + Guid.NewGuid().ToString("N");
        AdminAuditLogger.Write(
            "ADMIN_WEIGHT_EDIT",
            "SUCCESS",
            "Admin",
            ticketId: "draft",
            note: "weighNo=1/" + marker);

        // Read full file (not a byte-offset delta) so parallel appenders cannot hide the line.
        Assert.True(File.Exists(path), path);
        var after = File.ReadAllText(path);
        Assert.Contains("ADMIN_WEIGHT_EDIT", after, StringComparison.Ordinal);
        Assert.Contains(marker, after, StringComparison.Ordinal);
        Assert.DoesNotContain("admin123", after, StringComparison.Ordinal);
    }
}

[Collection("CanXeDatabase")]
public sealed class Phase6Rc2InlineAdminWeightEditTests : IAsyncLifetime
{
    private TestApplicationFactory _factory = null!;

    public async Task InitializeAsync()
    {
        _factory = new TestApplicationFactory();
        await _factory.InitializeAsync();
    }

    public async Task DisposeAsync() => await _factory.DisposeAsync();

    [Fact]
    public async Task AdminUnlocked_CanEditDraftWeight1Inline()
    {
        await using var scope = _factory.Provider.CreateAsyncScope();
        var service = scope.ServiceProvider.GetRequiredService<WeighTicketService>();
        var draft = new WeighTicketDraft();
        var result = await service.ApplyAdminInlineWeightAsync(draft, 1, 9570m, StationUserRole.Admin);
        Assert.True(result.Success, result.ErrorMessage);
        Assert.Equal(9570m, draft.DraftWeight1);
        Assert.True(draft.HasCapturedWeight1);
        Assert.Equal(WeighInputSource.Manual, draft.DraftWeight1InputSource);
        Assert.NotNull(draft.DraftWeight1RecordedAt);
    }

    [Fact]
    public async Task AdminUnlocked_CanEditDraftWeight2Inline()
    {
        await using var scope = _factory.Provider.CreateAsyncScope();
        var service = scope.ServiceProvider.GetRequiredService<WeighTicketService>();
        var draft = new WeighTicketDraft();
        Assert.True((await service.ApplyAdminInlineWeightAsync(draft, 1, 10000m, StationUserRole.Admin)).Success);
        Assert.True((await service.ApplyAdminInlineWeightAsync(draft, 2, 4200m, StationUserRole.Admin)).Success);
        Assert.Equal(WeighInputSource.Manual, draft.DraftWeight2InputSource);
    }

    [Fact]
    public async Task InlineWeight_InvalidInput_DoesNotChangeOldValue()
    {
        await using var scope = _factory.Provider.CreateAsyncScope();
        var service = scope.ServiceProvider.GetRequiredService<WeighTicketService>();
        var draft = new WeighTicketDraft();
        Assert.True((await service.ApplyAdminInlineWeightAsync(draft, 1, 8000m, StationUserRole.Admin)).Success);
        var before = draft.DraftWeight1;
        Assert.False((await service.ApplyAdminInlineWeightAsync(draft, 1, -5m, StationUserRole.Admin)).Success);
        Assert.Equal(before, draft.DraftWeight1);
    }

    [Fact]
    public async Task UpdateCompletedAfterWeightEdit_RecalculatesAndPreservesMetadata()
    {
        await using var scope = _factory.Provider.CreateAsyncScope();
        var service = scope.ServiceProvider.GetRequiredService<WeighTicketService>();
        var scale = scope.ServiceProvider.GetRequiredService<IScaleService>();
        scale.SetManualMode(true);
        scale.SetManualWeightKg(18000m);
        var draft = new WeighTicketDraft
        {
            DraftCustomer = "Rc2 Cust",
            DraftVehicle = "51Z-22222",
            DraftCargoType = "Sand",
            DraftUnitPrice = 1000m
        };
        Assert.True((await service.CaptureWeightAsync(draft, 1)).Success);
        scale.SetManualWeightKg(8000m);
        Assert.True((await service.CaptureWeightAsync(draft, 2)).Success);
        var save = await service.SaveAsync(draft);
        Assert.True(save.Success, save.ErrorMessage);

        var edit = await service.LoadTicketForEditAsync(save.SavedTicket!.Id);
        Assert.True((await service.ApplyAdminInlineWeightAsync(edit, 1, 20000m, StationUserRole.Admin, bypassSavedLock: true)).Success);
        edit.DeveloperWeightUnlockEnabled = true;
        edit.WeightOverrideReasonCode = WeightOverrideReasons.AdminInline;
        var update = await service.UpdateTicketAsync(edit, "admin");
        Assert.True(update.Success, update.ErrorMessage);
        Assert.Equal("Rc2 Cust", update.UpdatedTicket!.CustomerName);
        Assert.Equal("51Z-222.22", update.UpdatedTicket.LicensePlate);
        Assert.Equal(12000m, update.UpdatedTicket.NetWeightKg);
    }
}


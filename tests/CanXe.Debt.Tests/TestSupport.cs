using CanXe.Debt.Contract;
using CanXe.Debt.Data;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace CanXe.Debt.Tests;

public sealed class TestDb : IDisposable
{
    private readonly SqliteConnection _connection;

    public DebtDbContext Db { get; }

    public TestDb()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();
        Db = new DebtDbContext(new DbContextOptionsBuilder<DebtDbContext>().UseSqlite(_connection).Options);
        Db.Database.EnsureCreated();
    }

    public Season AddSeason(string name = "Mùa cà 2026", string start = "2026-10-01", string? end = null, bool closed = false)
    {
        var season = new Season
        {
            Name = name,
            StartDate = DateOnly.Parse(start),
            EndDate = end is null ? null : DateOnly.Parse(end),
            IsClosed = closed
        };
        Db.Seasons.Add(season);
        Db.SaveChanges();
        return season;
    }

    public Customer AddCustomer(string name)
    {
        var customer = new Customer { Name = name, NormalizedName = Services.NameNormalizer.Normalize(name) };
        Db.Customers.Add(customer);
        Db.SaveChanges();
        return customer;
    }

    public void Dispose()
    {
        Db.Dispose();
        _connection.Dispose();
    }
}

public static class Make
{
    public static ExportedTicket Ticket(
        string code, string date = "2026-10-20", string? customer = "Công Ty Đại Lợi", string cargo = "Rô Tươi",
        long? amount = 1_000_000, bool deleted = false, decimal? kg = 1000m, decimal? price = 1000m) =>
        new(code, "1/10", DateTimeOffset.Parse($"{date}T08:00:00+07:00"), customer, "82C-072.19", cargo,
            kg is { } g ? g + 5000 : null, kg is null ? null : 5000, kg, kg, price, null, null, amount, deleted);

    public static DebtExportFile File(string station, params ExportedTicket[] tickets) =>
        File(station, new DateTimeOffset(2026, 10, 26, 18, 0, 0, TimeSpan.FromHours(7)), tickets);

    public static DebtExportFile File(string station, DateTimeOffset exportedAt, params ExportedTicket[] tickets) =>
        DebtExportFile.Create(new StationInfo(station, station == "DAKHA" ? "Đắk Hà" : "Đắk Tô"), exportedAt, tickets);
}

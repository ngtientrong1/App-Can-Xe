using Microsoft.EntityFrameworkCore;

namespace CanXe.Debt.Data;

public sealed class DebtDbContext(DbContextOptions<DebtDbContext> options) : DbContext(options)
{
    public DbSet<Season> Seasons => Set<Season>();
    public DbSet<Customer> Customers => Set<Customer>();
    public DbSet<CustomerAlias> CustomerAliases => Set<CustomerAlias>();
    public DbSet<TicketRecord> Tickets => Set<TicketRecord>();
    public DbSet<Payment> Payments => Set<Payment>();
    public DbSet<ImportBatch> ImportBatches => Set<ImportBatch>();
    public DbSet<SettlementPeriod> Periods => Set<SettlementPeriod>();
    public DbSet<AppUser> Users => Set<AppUser>();
    public DbSet<AppSetting> Settings => Set<AppSetting>();

    protected override void OnModelCreating(ModelBuilder b)
    {
        b.Entity<Customer>().HasIndex(x => x.NormalizedName).IsUnique();

        b.Entity<CustomerAlias>().HasIndex(x => x.NormalizedName).IsUnique();
        b.Entity<CustomerAlias>().HasOne(x => x.Customer).WithMany().HasForeignKey(x => x.CustomerId);

        b.Entity<TicketRecord>().HasIndex(x => new { x.StationCode, x.Code }).IsUnique();
        b.Entity<TicketRecord>().HasIndex(x => x.TicketDate);
        b.Entity<TicketRecord>().HasIndex(x => x.CustomerId);
        b.Entity<TicketRecord>().HasOne(x => x.Customer).WithMany().HasForeignKey(x => x.CustomerId);
        b.Entity<TicketRecord>().Ignore(x => x.HasPriceSplit);
        b.Entity<TicketRecord>().Ignore(x => x.Stage);

        b.Entity<SettlementPeriod>().HasIndex(x => new { x.CustomerId, x.SeasonId, x.FromDate }).IsUnique();
        b.Entity<SettlementPeriod>().HasOne(x => x.Customer).WithMany().HasForeignKey(x => x.CustomerId);
        b.Entity<Payment>().HasIndex(x => x.PeriodId);

        b.Entity<Payment>().HasIndex(x => new { x.CustomerId, x.SeasonId });
        b.Entity<Payment>().HasOne(x => x.Customer).WithMany().HasForeignKey(x => x.CustomerId);
        b.Entity<Payment>().Ignore(x => x.IsVoided);

        b.Entity<AppUser>().Property(x => x.Username).UseCollation("NOCASE");
        b.Entity<AppUser>().HasIndex(x => x.Username).IsUnique();

        b.Entity<AppSetting>().HasKey(x => x.Key);
    }
}

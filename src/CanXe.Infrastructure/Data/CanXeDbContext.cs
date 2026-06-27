using CanXe.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace CanXe.Infrastructure.Data;

public sealed class CanXeDbContext : DbContext
{
    public CanXeDbContext(DbContextOptions<CanXeDbContext> options) : base(options) { }

    public DbSet<Customer> Customers => Set<Customer>();
    public DbSet<CargoType> CargoTypes => Set<CargoType>();
    public DbSet<Vehicle> Vehicles => Set<Vehicle>();
    public DbSet<WeighTicket> WeighTickets => Set<WeighTicket>();
    public DbSet<WeighEvent> WeighEvents => Set<WeighEvent>();
    public DbSet<TicketSequence> TicketSequences => Set<TicketSequence>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Customer>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Name).IsRequired();
            entity.Property(e => e.NormalizedName).IsRequired();
            entity.HasIndex(e => e.NormalizedName);
        });

        modelBuilder.Entity<CargoType>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Name).IsRequired();
            entity.Property(e => e.NormalizedName).IsRequired();
            entity.HasIndex(e => e.NormalizedName);
        });

        modelBuilder.Entity<Vehicle>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.PlateNumber).IsRequired();
            entity.Property(e => e.NormalizedPlateNumber).IsRequired();
            entity.HasIndex(e => e.NormalizedPlateNumber).IsUnique();
            entity.HasOne(e => e.LastCustomer).WithMany().HasForeignKey(e => e.LastCustomerId);
        });

        modelBuilder.Entity<WeighTicket>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.InternalCode).IsRequired();
            entity.Property(e => e.DisplayNumber).IsRequired();
            entity.HasIndex(e => e.InternalCode).IsUnique();
            entity.HasIndex(e => e.TicketDateTime);
            entity.HasIndex(e => e.VehicleId);
            entity.HasIndex(e => e.LicensePlateSnapshot);
            entity.HasIndex(e => e.CustomerId);
            entity.HasIndex(e => e.CargoTypeId);
            entity.HasIndex(e => new { e.CargoTypeNameSnapshot, e.CustomerNameSnapshot, e.UnitPriceVndPerKg });
            entity.HasOne(e => e.Customer).WithMany().HasForeignKey(e => e.CustomerId);
            entity.HasOne(e => e.Vehicle).WithMany().HasForeignKey(e => e.VehicleId);
            entity.HasOne(e => e.CargoType).WithMany().HasForeignKey(e => e.CargoTypeId);
        });

        modelBuilder.Entity<WeighEvent>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => new { e.WeighTicketId, e.Sequence }).IsUnique();
            entity.HasOne(e => e.WeighTicket).WithMany(t => t.Events).HasForeignKey(e => e.WeighTicketId);
        });

        modelBuilder.Entity<TicketSequence>(entity =>
        {
            entity.HasKey(e => new { e.Year, e.Month });
        });
    }
}

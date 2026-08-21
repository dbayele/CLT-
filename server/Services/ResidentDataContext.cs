using CltPlusPlus.Api.Models;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace CltPlusPlus.Api.Services;

public sealed class ResidentDataContext : IdentityDbContext<ResidentUser>
{
    public ResidentDataContext(DbContextOptions<ResidentDataContext> options) : base(options) { }

    public DbSet<ResidentVehicle> ResidentVehicles => Set<ResidentVehicle>();
    public DbSet<BusinessAccount> BusinessAccounts => Set<BusinessAccount>();
    public DbSet<TowingRequest> TowingRequests => Set<TowingRequest>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);
        builder.Entity<ResidentVehicle>(entity =>
        {
            entity.HasKey(x => x.Id);
            entity.Property(x => x.LicensePlate).HasMaxLength(20).IsRequired();
            entity.Property(x => x.PlateState).HasMaxLength(3).IsRequired();
            entity.Property(x => x.Vin).HasMaxLength(17);
            entity.HasIndex(x => new { x.ResidentUserId, x.PlateState, x.LicensePlate }).IsUnique();
            entity.HasOne<ResidentUser>().WithMany().HasForeignKey(x => x.ResidentUserId).OnDelete(DeleteBehavior.Cascade);
        });
        builder.Entity<BusinessAccount>(entity =>
        {
            entity.HasKey(x => x.Id);
            entity.HasIndex(x => x.ResidentUserId).IsUnique();
            entity.Property(x => x.LegalName).HasMaxLength(160).IsRequired();
            entity.Property(x => x.Email).HasMaxLength(254).IsRequired();
            entity.HasOne<ResidentUser>().WithOne().HasForeignKey<BusinessAccount>(x => x.ResidentUserId).OnDelete(DeleteBehavior.Cascade);
        });
        builder.Entity<TowingRequest>(entity =>
        {
            entity.HasKey(x => x.Id);
            entity.HasIndex(x => x.TrackingNumber).IsUnique();
            entity.Property(x => x.RequestType).HasMaxLength(30).IsRequired();
            entity.Property(x => x.LicensePlate).HasMaxLength(20).IsRequired();
            entity.Property(x => x.PlateState).HasMaxLength(3).IsRequired();
            entity.Property(x => x.Vin).HasMaxLength(17).IsRequired();
            entity.HasOne<BusinessAccount>().WithMany().HasForeignKey(x => x.BusinessAccountId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<ResidentUser>().WithMany().HasForeignKey(x => x.ResidentUserId).OnDelete(DeleteBehavior.Cascade);
        });
    }
}

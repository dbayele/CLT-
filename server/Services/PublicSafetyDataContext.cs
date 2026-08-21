using CltPlusPlus.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace CltPlusPlus.Api.Services;

// Physically separate public-safety database. No EF relationships are configured
// to civic/identity entities because those records live in a different database.
public sealed class PublicSafetyDataContext : DbContext
{
    public PublicSafetyDataContext(DbContextOptions<PublicSafetyDataContext> options) : base(options) { }

    public DbSet<TowingRequest> TowingRequests => Set<TowingRequest>();
    public DbSet<TowRelease> TowReleases => Set<TowRelease>();
    public DbSet<TowZone> TowZones => Set<TowZone>();
    public DbSet<ZoneWreckerCompany> ZoneWreckerCompanies => Set<ZoneWreckerCompany>();
    public DbSet<TowDispatchRequest> TowDispatchRequests => Set<TowDispatchRequest>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        builder.Entity<TowingRequest>(entity =>
        {
            entity.HasKey(x => x.Id);
            entity.HasIndex(x => x.TrackingNumber).IsUnique();
            entity.Property(x => x.RequestType).HasMaxLength(30).IsRequired();
            entity.Property(x => x.LicensePlate).HasMaxLength(20).IsRequired();
            entity.Property(x => x.PlateState).HasMaxLength(3).IsRequired();
            entity.Property(x => x.Vin).HasMaxLength(17).IsRequired();
            entity.Property(x => x.Status).HasMaxLength(60).IsRequired();
            entity.HasIndex(x => x.BusinessAccountId);
            entity.HasIndex(x => x.ResidentUserId);
        });

        builder.Entity<TowRelease>(entity =>
        {
            entity.HasKey(x => x.Id);
            entity.HasIndex(x => x.TowingRequestId);
            entity.HasIndex(x => x.BusinessAccountId);
            entity.HasIndex(x => x.ResidentUserId);
            entity.Property(x => x.AuthorizedBy).HasMaxLength(160).IsRequired();
            entity.Property(x => x.ReleasedToName).HasMaxLength(160).IsRequired();
        });

        builder.Entity<ZoneWreckerCompany>(entity =>
        {
            entity.HasKey(x => x.Id);
            entity.Property(x => x.CompanyName).HasMaxLength(180).IsRequired();
            entity.Property(x => x.ContactName).HasMaxLength(160).IsRequired();
            entity.Property(x => x.ContactEmail).HasMaxLength(254).IsRequired();
            entity.Property(x => x.ContactPhone).HasMaxLength(40).IsRequired();
            entity.HasIndex(x => x.CompanyName);
        });

        builder.Entity<TowZone>(entity =>
        {
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Name).HasMaxLength(120).IsRequired();
            entity.HasIndex(x => x.Name).IsUnique();
            entity.HasIndex(x => x.DefaultTowCompanyId);
            entity.HasOne<ZoneWreckerCompany>().WithMany().HasForeignKey(x => x.DefaultTowCompanyId).OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<TowDispatchRequest>(entity =>
        {
            entity.HasKey(x => x.Id);
            entity.HasIndex(x => x.TrackingNumber).IsUnique();
            entity.HasIndex(x => x.TowZoneId);
            entity.HasIndex(x => x.ZoneWreckerCompanyId);
            entity.Property(x => x.RequestedByEmployee).HasMaxLength(160).IsRequired();
            entity.Property(x => x.Status).HasMaxLength(40).IsRequired();
            entity.Property(x => x.NotificationStatus).HasMaxLength(40).IsRequired();
            entity.HasOne<TowZone>().WithMany().HasForeignKey(x => x.TowZoneId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<ZoneWreckerCompany>().WithMany().HasForeignKey(x => x.ZoneWreckerCompanyId).OnDelete(DeleteBehavior.Restrict);
        });
    }
}

using CltPlusPlus.Api.Models;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace CltPlusPlus.Api.Services;

// Civic/non-public-safety database: identity, resident profile data, saved vehicles,
// business accounts, permit applications, and rental-property registrations. Public-safety
// application records live in PublicSafetyDataContext and reference civic identities only by stable IDs.
public sealed class ResidentDataContext : IdentityDbContext<ResidentUser>
{
    public ResidentDataContext(DbContextOptions<ResidentDataContext> options) : base(options) { }

    public DbSet<ResidentVehicle> ResidentVehicles => Set<ResidentVehicle>();
    public DbSet<BusinessAccount> BusinessAccounts => Set<BusinessAccount>();
    public DbSet<BusinessPermitApplication> BusinessPermitApplications => Set<BusinessPermitApplication>();
    public DbSet<RentalPropertyRegistration> RentalPropertyRegistrations => Set<RentalPropertyRegistration>();

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
        builder.Entity<BusinessPermitApplication>(entity =>
        {
            entity.HasKey(x => x.Id);
            entity.HasIndex(x => x.TrackingNumber).IsUnique();
            entity.Property(x => x.PermitTypeId).HasMaxLength(80).IsRequired();
            entity.Property(x => x.PermitTitle).HasMaxLength(180).IsRequired();
            entity.Property(x => x.Department).HasMaxLength(120).IsRequired();
            entity.Property(x => x.Status).HasMaxLength(80).IsRequired();
            entity.HasOne<BusinessAccount>().WithMany().HasForeignKey(x => x.BusinessAccountId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<ResidentUser>().WithMany().HasForeignKey(x => x.ResidentUserId).OnDelete(DeleteBehavior.Cascade);
        });
        builder.Entity<RentalPropertyRegistration>(entity =>
        {
            entity.HasKey(x => x.Id);
            entity.HasIndex(x => x.TrackingNumber).IsUnique();
            entity.Property(x => x.ApplicantType).HasMaxLength(20).IsRequired();
            entity.Property(x => x.PropertyAddress).HasMaxLength(240).IsRequired();
            entity.Property(x => x.OwnerName).HasMaxLength(180).IsRequired();
            entity.Property(x => x.OwnerEmail).HasMaxLength(254).IsRequired();
            entity.Property(x => x.OwnerPhone).HasMaxLength(40).IsRequired();
            entity.Property(x => x.Status).HasMaxLength(80).IsRequired();
            entity.HasOne<ResidentUser>().WithMany().HasForeignKey(x => x.ResidentUserId).OnDelete(DeleteBehavior.Cascade);
            entity.HasOne<BusinessAccount>().WithMany().HasForeignKey(x => x.BusinessAccountId).OnDelete(DeleteBehavior.Restrict);
        });
    }
}

using CltPlusPlus.Api.Models;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace CltPlusPlus.Api.Services;

public sealed class ResidentDataContext : IdentityDbContext<ResidentUser>
{
    public ResidentDataContext(DbContextOptions<ResidentDataContext> options) : base(options) { }

    public DbSet<ResidentVehicle> ResidentVehicles => Set<ResidentVehicle>();

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
    }
}

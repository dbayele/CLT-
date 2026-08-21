using CltPlusPlus.Api.Models;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace CltPlusPlus.Api.Services;

public sealed class ResidentDataContext : IdentityDbContext<ResidentUser>
{
    public ResidentDataContext(DbContextOptions<ResidentDataContext> options) : base(options) { }
}

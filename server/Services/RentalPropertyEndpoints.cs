using System.Security.Claims;
using CltPlusPlus.Api.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace CltPlusPlus.Api.Services;

public static class RentalPropertyEndpoints
{
    public static void Map(WebApplication app)
    {
        app.MapGet("/api/account/rental-properties", async (ClaimsPrincipal principal, UserManager<ResidentUser> users, ResidentDataContext db) =>
        {
            if (principal.Identity?.IsAuthenticated != true) return Results.Unauthorized();
            var user = await users.GetUserAsync(principal); if (user is null) return Results.Unauthorized();
            var rows = await db.RentalPropertyRegistrations.Where(x => x.ResidentUserId == user.Id).OrderByDescending(x => x.CreatedAt).ToListAsync();
            return Results.Ok(rows);
        });

        app.MapPost("/api/account/rental-properties", async (RentalPropertyRegistrationInput input, ClaimsPrincipal principal, UserManager<ResidentUser> users, ResidentDataContext db, CensusGeocoder geocoder, CancellationToken ct) =>
        {
            if (principal.Identity?.IsAuthenticated != true) return Results.Unauthorized();
            var user = await users.GetUserAsync(principal); if (user is null) return Results.Unauthorized();
            var applicantType = input.ApplicantType.Equals("Business", StringComparison.OrdinalIgnoreCase) ? "Business" : "Resident";
            BusinessAccount? business = null;
            if (applicantType == "Business")
            {
                business = await db.BusinessAccounts.FirstOrDefaultAsync(x => x.ResidentUserId == user.Id, ct);
                if (business is null) return Results.BadRequest(new { error = "Create or link a business account before submitting as a business." });
            }
            if (string.IsNullOrWhiteSpace(input.PropertyAddress) || string.IsNullOrWhiteSpace(input.OwnerName) || string.IsNullOrWhiteSpace(input.OwnerMailingAddress) || string.IsNullOrWhiteSpace(input.OwnerEmail) || string.IsNullOrWhiteSpace(input.OwnerPhone))
                return Results.BadRequest(new { error = "Property address and owner contact information are required." });
            if (input.UnitCount < 1 || input.UnitCount > 10000) return Results.BadRequest(new { error = "Unit count must be between 1 and 10,000." });

            var validated = await geocoder.ValidateAsync(input.PropertyAddress, ct);
            if (!validated.Valid) return Results.BadRequest(new { error = validated.Warning ?? "Property address could not be validated." });

            var row = new RentalPropertyRegistration
            {
                TrackingNumber = $"RPR-{DateTime.UtcNow:yyMMdd}-{Random.Shared.Next(100000,999999)}",
                ResidentUserId = user.Id,
                BusinessAccountId = business?.Id,
                ApplicantType = applicantType,
                PropertyAddress = validated.NormalizedAddress ?? input.PropertyAddress.Trim(),
                ParcelNumber = Clean(input.ParcelNumber, 80),
                PropertyName = Clean(input.PropertyName, 180),
                UnitCount = input.UnitCount,
                OwnerName = input.OwnerName.Trim(),
                OwnerMailingAddress = input.OwnerMailingAddress.Trim(),
                OwnerEmail = input.OwnerEmail.Trim(),
                OwnerPhone = input.OwnerPhone.Trim(),
                LocalResponsiblePartyName = Clean(input.LocalResponsiblePartyName, 180),
                LocalResponsiblePartyEmail = Clean(input.LocalResponsiblePartyEmail, 254),
                LocalResponsiblePartyPhone = Clean(input.LocalResponsiblePartyPhone, 40),
                PropertyManagerName = Clean(input.PropertyManagerName, 180),
                PropertyManagerEmail = Clean(input.PropertyManagerEmail, 254),
                PropertyManagerPhone = Clean(input.PropertyManagerPhone, 40),
                RentalStatus = string.IsNullOrWhiteSpace(input.RentalStatus) ? "Active rental" : input.RentalStatus.Trim(),
                Notes = Clean(input.Notes, 4000),
                Status = "Submitted"
            };
            db.RentalPropertyRegistrations.Add(row); await db.SaveChangesAsync(ct);
            return Results.Created($"/api/account/rental-properties/{row.Id}", row);
        });
    }

    private static string? Clean(string? value, int max){var s=value?.Trim();return string.IsNullOrWhiteSpace(s)?null:s[..Math.Min(s.Length,max)];}
}

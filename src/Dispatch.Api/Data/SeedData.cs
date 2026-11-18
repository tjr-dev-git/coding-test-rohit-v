using Dispatch.Api.Domain;
using Microsoft.EntityFrameworkCore;

namespace Dispatch.Api.Data;

public static class SeedData
{
    public static async Task InitialiseAsync(DispatchDbContext dbContext, bool recreate, CancellationToken cancellationToken)
    {
        if (recreate)
        {
            await dbContext.Database.EnsureDeletedAsync(cancellationToken);
        }

        await dbContext.Database.EnsureCreatedAsync(cancellationToken);
        if (await dbContext.Sites.FindAsync(["IND-JSP"], cancellationToken) is not null)
        {
            return;
        }

        dbContext.Sites.AddRange(
            new Site { Id = "IND-JSP", Name = "Jaspur Materials Recovery Site", TimeZoneId = "Asia/Kolkata" },
            new Site { Id = "GBR-NTH", Name = "Northbridge Steel Services", TimeZoneId = "Europe/London" });

        dbContext.Equipment.AddRange(
            new Equipment { Id = "PC-17", SiteId = "IND-JSP", DisplayName = "Pot Carrier 17", IsAvailable = true },
            new Equipment { Id = "WL-04", SiteId = "IND-JSP", DisplayName = "Wheel Loader 04", IsAvailable = true },
            new Equipment { Id = "HT-09", SiteId = "IND-JSP", DisplayName = "Haul Truck 09", IsAvailable = false },
            new Equipment { Id = "PC-17", SiteId = "GBR-NTH", DisplayName = "Pot Carrier 17", IsAvailable = true },
            new Equipment { Id = "HT-22", SiteId = "GBR-NTH", DisplayName = "Haul Truck 22", IsAvailable = false });

        dbContext.Movements.AddRange(
            Movement("ind-before-day", "IND-JSP", "Steel slag", "Slag Pit East", "Wet Milling Feed Area", 82.5m, MovementPriority.Urgent, MovementStatus.Requested, null, "2026-08-17T00:29:00Z"),
            Movement("ind-0600", "IND-JSP", "Recovered ferrous metal", "Metal Recovery Bay", "Meltshop Return Stockpile", 24m, MovementPriority.Normal, MovementStatus.Assigned, "WL-04", "2026-08-17T00:30:00Z"),
            Movement("shared-001", "IND-JSP", "Steel slag", "Slag Pit East", "Wet Milling Feed Area", 80m, MovementPriority.Urgent, MovementStatus.Requested, null, "2026-08-17T00:31:00Z"),
            Movement("ind-active", "IND-JSP", "Processed aggregate with an intentionally long operational description", "Screening and Crushing Discharge Stockpile A", "Road Products Covered Storage Area 3", 110.75m, MovementPriority.Urgent, MovementStatus.InProgress, "PC-17", "2026-08-17T08:12:00Z"),
            Movement("ind-1759", "IND-JSP", "Scrap turnings", "Briquetting Plant", "Finished Product Dispatch", 15.25m, MovementPriority.Normal, MovementStatus.Completed, "WL-04", "2026-08-17T12:29:00Z"),
            Movement("ind-night", "IND-JSP", "Stainless slag", "Slag Cooling Area", "Recovery Plant Hopper", 94m, MovementPriority.Normal, MovementStatus.Requested, null, "2026-08-17T12:30:00Z"),
            Movement("ind-midnight", "IND-JSP", "Millscale", "Caster Collection Point", "Briquetting Feed Store", 38m, MovementPriority.Normal, MovementStatus.Cancelled, null, "2026-08-17T18:30:00Z"),
            Movement("shared-001", "GBR-NTH", "Steel slag", "Furnace Bay", "Cooling Pit 2", 76m, MovementPriority.Normal, MovementStatus.Requested, null, "2026-08-17T05:00:00Z"));

        await dbContext.SaveChangesAsync(cancellationToken);
        dbContext.ChangeTracker.Clear();
    }

    private static Movement Movement(
        string id,
        string siteId,
        string material,
        string source,
        string destination,
        decimal tonnes,
        MovementPriority priority,
        MovementStatus status,
        string? equipmentId,
        string requestedAt) => new()
        {
            Id = id,
            SiteId = siteId,
            MaterialType = material,
            SourceLocation = source,
            DestinationLocation = destination,
            EstimatedTonnes = tonnes,
            Priority = priority,
            Status = status,
            EquipmentId = equipmentId,
            RequestedAtUtc = DateTimeOffset.Parse(requestedAt),
            LastUpdatedAtUtc = DateTimeOffset.Parse(requestedAt).AddMinutes(4)
        };
}

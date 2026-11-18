using Dispatch.Api.Contracts;
using Dispatch.Api.Data;
using Dispatch.Api.Domain;
using Microsoft.EntityFrameworkCore;

namespace Dispatch.Api.Services;

public sealed class MovementService(DispatchDbContext dbContext, TimeProvider timeProvider) : IMovementService
{
    internal DateTimeOffset UtcNow => timeProvider.GetUtcNow();

    public async Task<IReadOnlyList<SiteDto>> GetSitesAsync(CancellationToken cancellationToken) =>
        await dbContext.Sites
            .OrderBy(x => x.Name)
            .Select(x => new SiteDto(x.Id, x.Name, x.TimeZoneId))
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<MovementDto>> GetMovementsAsync(
        string siteId,
        DateOnly shiftDate,
        ShiftKind shift,
        MovementStatus? status,
        CancellationToken cancellationToken)
    {
        var site = await dbContext.Sites
            .WithPartitionKey(siteId)
            .SingleOrDefaultAsync(x => x.Id == siteId, cancellationToken);

        if (site is null)
        {
            return [];
        }

        var (startUtc, endUtc) = GetShiftBounds(site.TimeZoneId, shiftDate, shift);
        var query = dbContext.Movements
            .WithPartitionKey(siteId)
            .Where(x => x.SiteId == siteId && x.RequestedAtUtc >= startUtc && x.RequestedAtUtc < endUtc);

        if (status is not null)
        {
            query = query.Where(x => x.Status == status);
        }

        var movements = await query.ToListAsync(cancellationToken);
        return movements
            .OrderByDescending(x => x.Priority)
            .ThenBy(x => x.RequestedAtUtc)
            .Select(x => x.ToDto())
            .ToList();
    }

    public async Task<IReadOnlyList<EquipmentDto>> GetEquipmentAsync(string siteId, CancellationToken cancellationToken) =>
        await dbContext.Equipment
            .WithPartitionKey(siteId)
            .Where(x => x.SiteId == siteId)
            .OrderBy(x => x.DisplayName)
            .Select(x => new EquipmentDto(x.Id, x.SiteId, x.DisplayName, x.IsAvailable))
            .ToListAsync(cancellationToken);

    public async Task<MutationResult> CreateAsync(
        string siteId,
        CreateMovementRequest request,
        CancellationToken cancellationToken)
    {
        var site = await dbContext.Sites
            .WithPartitionKey(siteId)
            .SingleOrDefaultAsync(x => x.Id == siteId, cancellationToken);

        if (site is null)
        {
            return MutationResult.NotFound($"Site '{siteId}' was not found.");
        }

        var errors = MovementRules.ValidateCreate(request);
        if (errors.Count > 0)
        {
            return MutationResult.Invalid(errors);
        }

        var now = UtcNow;
        var movement = new Movement
        {
            Id = Guid.NewGuid().ToString("N"),
            SiteId = siteId,
            MaterialType = request.MaterialType.Trim(),
            SourceLocation = request.SourceLocation.Trim(),
            DestinationLocation = request.DestinationLocation.Trim(),
            EstimatedTonnes = request.EstimatedTonnes,
            Priority = request.Priority,
            Status = MovementStatus.Requested,
            EquipmentId = null,
            RequestedAtUtc = now,
            LastUpdatedAtUtc = now
        };

        dbContext.Movements.Add(movement);
        await dbContext.SaveChangesAsync(cancellationToken);

        return MutationResult.Success(movement.ToDto());
    }

    public async Task<MutationResult> UpdateAsync(
        string siteId,
        string movementId,
        UpdateMovementRequest request,
        CancellationToken cancellationToken)
    {
        var movement = await dbContext.Movements
            .WithPartitionKey(siteId)
            .SingleOrDefaultAsync(x => x.Id == movementId && x.SiteId == siteId, cancellationToken);

        if (movement is null)
        {
            return MutationResult.NotFound(
                $"Movement '{movementId}' was not found at site '{siteId}'.");
        }

        if (string.IsNullOrWhiteSpace(request.ETag))
        {
            return MutationResult.Invalid(
                nameof(UpdateMovementRequest.ETag),
                "An ETag is required to update a movement.");
        }

        // Concurrency is checked before business validation so a stale client can
        // never overwrite a newer version, even if the rest of its request is invalid.
        if (!string.Equals(movement.ETag, request.ETag, StringComparison.Ordinal))
        {
            return MutationResult.Conflict(
                "This movement was updated by another dispatcher. Reload it and try again.",
                movement.ToDto());
        }

        if (request.Status is null)
        {
            return MutationResult.Invalid(
                nameof(UpdateMovementRequest.Status),
                "Status is required.");
        }

        var targetStatus = request.Status.Value;
        if (!Enum.IsDefined(targetStatus))
        {
            return MutationResult.Invalid(
                nameof(UpdateMovementRequest.Status),
                "Status must be a valid movement status.");
        }

        if (MovementRules.IsTerminal(movement.Status))
        {
            return MutationResult.Invalid(
                nameof(UpdateMovementRequest.Status),
                $"A {movement.Status} movement cannot be changed.");
        }

        if (!MovementRules.IsTransitionAllowed(movement.Status, targetStatus))
        {
            return MutationResult.Invalid(
                nameof(UpdateMovementRequest.Status),
                $"A movement cannot move from {movement.Status} to {targetStatus}.");
        }

        if (request.EquipmentId is not null && string.IsNullOrWhiteSpace(request.EquipmentId))
        {
            return MutationResult.Invalid(
                nameof(UpdateMovementRequest.EquipmentId),
                "Equipment ID cannot be empty.");
        }

        if (request.EquipmentId is not null)
        {
            var equipment = await dbContext.Equipment
                .WithPartitionKey(siteId)
                .SingleOrDefaultAsync(
                    x => x.Id == request.EquipmentId && x.SiteId == siteId,
                    cancellationToken);

            if (equipment is null)
            {
                return MutationResult.Invalid(
                    nameof(UpdateMovementRequest.EquipmentId),
                    $"Equipment '{request.EquipmentId}' was not found at this site.");
            }

            var equipmentChanged = !string.Equals(
                equipment.Id,
                movement.EquipmentId,
                StringComparison.Ordinal);
            if (equipmentChanged && !equipment.IsAvailable)
            {
                return MutationResult.Invalid(
                    nameof(UpdateMovementRequest.EquipmentId),
                    $"Equipment '{equipment.DisplayName}' is not available.");
            }
        }

        var effectiveEquipmentId = request.EquipmentId ?? movement.EquipmentId;
        if (MovementRules.RequiresEquipment(targetStatus) && effectiveEquipmentId is null)
        {
            return MutationResult.Invalid(
                nameof(UpdateMovementRequest.EquipmentId),
                $"Equipment is required when a movement is {targetStatus}.");
        }

        movement.Status = targetStatus;
        if (request.EquipmentId is not null)
        {
            movement.EquipmentId = request.EquipmentId;
        }
        movement.LastUpdatedAtUtc = UtcNow;

        // The Cosmos provider sends the original ETag as If-Match. It already
        // matches the request here; setting it explicitly keeps the intent clear.
        dbContext.Entry(movement).Property(x => x.ETag).OriginalValue = request.ETag;

        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            dbContext.ChangeTracker.Clear();
            var current = await dbContext.Movements
                .WithPartitionKey(siteId)
                .AsNoTracking()
                .SingleOrDefaultAsync(
                    x => x.Id == movementId && x.SiteId == siteId,
                    cancellationToken);

            return MutationResult.Conflict(
                "This movement was updated by another dispatcher. Reload it and try again.",
                current?.ToDto());
        }

        return MutationResult.Success(movement.ToDto());
    }

    internal static (DateTimeOffset StartUtc, DateTimeOffset EndUtc) GetShiftBounds(
        string timeZoneId,
        DateOnly shiftDate,
        ShiftKind shift)
    {
        var timeZone = TimeZoneInfo.FindSystemTimeZoneById(timeZoneId);
        var startHour = shift == ShiftKind.Day ? 6 : 18;
        var localStart = shiftDate.ToDateTime(new TimeOnly(startHour, 0), DateTimeKind.Unspecified);
        var localEnd = localStart.AddHours(12);
        var startUtc = new DateTimeOffset(TimeZoneInfo.ConvertTimeToUtc(localStart, timeZone), TimeSpan.Zero);
        var endUtc = new DateTimeOffset(TimeZoneInfo.ConvertTimeToUtc(localEnd, timeZone), TimeSpan.Zero);
        return (startUtc, endUtc);
    }
}

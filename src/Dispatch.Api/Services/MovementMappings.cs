using Dispatch.Api.Contracts;
using Dispatch.Api.Domain;

namespace Dispatch.Api.Services;

internal static class MovementMappings
{
    public static MovementDto ToDto(this Movement movement) => new(
        movement.Id,
        movement.SiteId,
        movement.MaterialType,
        movement.SourceLocation,
        movement.DestinationLocation,
        movement.EstimatedTonnes,
        movement.Priority,
        movement.Status,
        movement.EquipmentId,
        movement.RequestedAtUtc,
        movement.LastUpdatedAtUtc,
        movement.ETag);
}


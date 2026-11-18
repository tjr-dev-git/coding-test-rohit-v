using System.ComponentModel.DataAnnotations;
using Dispatch.Api.Domain;

namespace Dispatch.Api.Contracts;

public sealed record MovementDto(
    string Id,
    string SiteId,
    string MaterialType,
    string SourceLocation,
    string DestinationLocation,
    decimal EstimatedTonnes,
    MovementPriority Priority,
    MovementStatus Status,
    string? EquipmentId,
    DateTimeOffset RequestedAtUtc,
    DateTimeOffset LastUpdatedAtUtc,
    string? ETag);

public sealed record EquipmentDto(string Id, string SiteId, string DisplayName, bool IsAvailable);

public sealed record SiteDto(string Id, string Name, string TimeZoneId);

public sealed class CreateMovementRequest
{
    [Required, StringLength(100, MinimumLength = 2)]
    public string MaterialType { get; init; } = string.Empty;

    [Required, StringLength(120, MinimumLength = 2)]
    public string SourceLocation { get; init; } = string.Empty;

    [Required, StringLength(120, MinimumLength = 2)]
    public string DestinationLocation { get; init; } = string.Empty;

    // The service enforces the strict "greater than zero" rule; this attribute
    // only rejects clearly out-of-range values before model binding succeeds.
    [Range(typeof(decimal), "0", "500")]
    public decimal EstimatedTonnes { get; init; }

    public MovementPriority Priority { get; init; }
}

public sealed class UpdateMovementRequest
{
    [Required]
    public MovementStatus? Status { get; init; }

    public string? EquipmentId { get; init; }

    [Required]
    public string? ETag { get; init; }
}


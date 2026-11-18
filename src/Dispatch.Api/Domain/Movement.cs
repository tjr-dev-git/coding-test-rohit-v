namespace Dispatch.Api.Domain;

public sealed class Movement
{
    public required string Id { get; set; }
    public required string SiteId { get; set; }
    public required string MaterialType { get; set; }
    public required string SourceLocation { get; set; }
    public required string DestinationLocation { get; set; }
    public decimal EstimatedTonnes { get; set; }
    public MovementPriority Priority { get; set; }
    public MovementStatus Status { get; set; }
    public string? EquipmentId { get; set; }
    public DateTimeOffset RequestedAtUtc { get; set; }
    public DateTimeOffset LastUpdatedAtUtc { get; set; }
    public string? ETag { get; set; }
}


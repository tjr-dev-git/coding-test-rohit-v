namespace Dispatch.Api.Domain;

public sealed class Equipment
{
    public required string Id { get; set; }
    public required string SiteId { get; set; }
    public required string DisplayName { get; set; }
    public bool IsAvailable { get; set; }
}


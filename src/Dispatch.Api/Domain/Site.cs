namespace Dispatch.Api.Domain;

public sealed class Site
{
    public required string Id { get; set; }
    public required string Name { get; set; }
    public required string TimeZoneId { get; set; }
}


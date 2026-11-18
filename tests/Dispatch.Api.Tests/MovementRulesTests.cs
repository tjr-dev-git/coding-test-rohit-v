using Dispatch.Api.Contracts;
using Dispatch.Api.Domain;
using Dispatch.Api.Services;
using Xunit;

namespace Dispatch.Api.Tests;

public sealed class MovementRulesTests
{
    [Fact]
    public void ValidateCreate_accepts_valid_request()
    {
        var errors = MovementRules.ValidateCreate(NewRequest());

        Assert.Empty(errors);
    }

    [Theory]
    [InlineData("Same area", "same area")]
    [InlineData(" Same area ", "SAME AREA")]
    public void ValidateCreate_rejects_same_source_and_destination(string source, string destination)
    {
        var errors = MovementRules.ValidateCreate(NewRequest(source: source, destination: destination));

        Assert.Contains(errors, error => error.Field == "SourceLocation");
        Assert.Contains(errors, error => error.Field == "DestinationLocation");
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(500.01)]
    public void ValidateCreate_rejects_tonnes_outside_the_allowed_range(decimal tonnes)
    {
        var errors = MovementRules.ValidateCreate(NewRequest(tonnes: tonnes));

        Assert.Contains(errors, error => error.Field == "EstimatedTonnes");
    }

    [Theory]
    [InlineData(0.0001)]
    [InlineData(500)]
    public void ValidateCreate_accepts_tonnes_at_the_boundaries(decimal tonnes)
    {
        var errors = MovementRules.ValidateCreate(NewRequest(tonnes: tonnes));

        Assert.Empty(errors);
    }

    [Fact]
    public void ValidateCreate_rejects_undefined_priority()
    {
        var errors = MovementRules.ValidateCreate(NewRequest(priority: (MovementPriority)99));

        Assert.Contains(errors, error => error.Field == "Priority");
    }

    [Fact]
    public void ValidateCreate_rejects_whitespace_text()
    {
        var errors = MovementRules.ValidateCreate(NewRequest(materialType: "  ", source: " ", destination: "  "));

        Assert.Contains(errors, error => error.Field == "MaterialType");
        Assert.Contains(errors, error => error.Field == "SourceLocation");
        Assert.Contains(errors, error => error.Field == "DestinationLocation");
    }

    [Theory]
    [InlineData(MovementStatus.Completed)]
    [InlineData(MovementStatus.Cancelled)]
    public void IsTerminal_returns_true_for_terminal_statuses(MovementStatus status)
    {
        Assert.True(MovementRules.IsTerminal(status));
    }

    [Theory]
    [InlineData(MovementStatus.Requested)]
    [InlineData(MovementStatus.Assigned)]
    [InlineData(MovementStatus.InProgress)]
    public void IsTerminal_returns_false_for_active_statuses(MovementStatus status)
    {
        Assert.False(MovementRules.IsTerminal(status));
    }

    [Theory]
    [InlineData(MovementStatus.Requested, MovementStatus.Requested)]
    [InlineData(MovementStatus.Requested, MovementStatus.Assigned)]
    [InlineData(MovementStatus.Requested, MovementStatus.Cancelled)]
    [InlineData(MovementStatus.Assigned, MovementStatus.Assigned)]
    [InlineData(MovementStatus.Assigned, MovementStatus.InProgress)]
    [InlineData(MovementStatus.Assigned, MovementStatus.Cancelled)]
    [InlineData(MovementStatus.InProgress, MovementStatus.InProgress)]
    [InlineData(MovementStatus.InProgress, MovementStatus.Completed)]
    [InlineData(MovementStatus.InProgress, MovementStatus.Cancelled)]
    public void IsTransitionAllowed_accepts_supported_transitions(
        MovementStatus current,
        MovementStatus target)
    {
        Assert.True(MovementRules.IsTransitionAllowed(current, target));
    }

    [Theory]
    [InlineData(MovementStatus.Requested, MovementStatus.InProgress)]
    [InlineData(MovementStatus.Requested, MovementStatus.Completed)]
    [InlineData(MovementStatus.Assigned, MovementStatus.Requested)]
    [InlineData(MovementStatus.Assigned, MovementStatus.Completed)]
    [InlineData(MovementStatus.InProgress, MovementStatus.Requested)]
    [InlineData(MovementStatus.InProgress, MovementStatus.Assigned)]
    [InlineData(MovementStatus.Completed, MovementStatus.InProgress)]
    [InlineData(MovementStatus.Cancelled, MovementStatus.Requested)]
    [InlineData(MovementStatus.Requested, (MovementStatus)99)]
    public void IsTransitionAllowed_rejects_unsupported_transitions(
        MovementStatus current,
        MovementStatus target)
    {
        Assert.False(MovementRules.IsTransitionAllowed(current, target));
    }

    [Theory]
    [InlineData(MovementStatus.Assigned, true)]
    [InlineData(MovementStatus.InProgress, true)]
    [InlineData(MovementStatus.Requested, false)]
    [InlineData(MovementStatus.Completed, false)]
    [InlineData(MovementStatus.Cancelled, false)]
    public void RequiresEquipment_matches_the_status(MovementStatus status, bool expected)
    {
        Assert.Equal(expected, MovementRules.RequiresEquipment(status));
    }

    private static CreateMovementRequest NewRequest(
        string materialType = "Steel slag",
        string source = "Slag Pit East",
        string destination = "Recovery Plant",
        decimal tonnes = 42m,
        MovementPriority priority = MovementPriority.Urgent) =>
        new()
        {
            MaterialType = materialType,
            SourceLocation = source,
            DestinationLocation = destination,
            EstimatedTonnes = tonnes,
            Priority = priority
        };
}

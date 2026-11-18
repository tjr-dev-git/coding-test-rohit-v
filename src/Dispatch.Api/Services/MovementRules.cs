using Dispatch.Api.Contracts;
using Dispatch.Api.Domain;

namespace Dispatch.Api.Services;

internal static class MovementRules
{
    private const int MaterialTypeMinLength = 2;
    private const int MaterialTypeMaxLength = 100;
    private const int LocationMinLength = 2;
    private const int LocationMaxLength = 120;

    public static IReadOnlyList<MutationError> ValidateCreate(CreateMovementRequest request)
    {
        var errors = new List<MutationError>();
        var materialType = request.MaterialType?.Trim() ?? string.Empty;
        var source = request.SourceLocation?.Trim() ?? string.Empty;
        var destination = request.DestinationLocation?.Trim() ?? string.Empty;

        if (materialType.Length is < MaterialTypeMinLength or > MaterialTypeMaxLength)
        {
            errors.Add(new MutationError(
                nameof(CreateMovementRequest.MaterialType),
                $"Material type must be between {MaterialTypeMinLength} and {MaterialTypeMaxLength} characters."));
        }

        if (source.Length is < LocationMinLength or > LocationMaxLength)
        {
            errors.Add(new MutationError(
                nameof(CreateMovementRequest.SourceLocation),
                $"Source location must be between {LocationMinLength} and {LocationMaxLength} characters."));
        }

        if (destination.Length is < LocationMinLength or > LocationMaxLength)
        {
            errors.Add(new MutationError(
                nameof(CreateMovementRequest.DestinationLocation),
                $"Destination location must be between {LocationMinLength} and {LocationMaxLength} characters."));
        }

        if (source.Length > 0
            && destination.Length > 0
            && string.Equals(source, destination, StringComparison.OrdinalIgnoreCase))
        {
            errors.Add(new MutationError(
                nameof(CreateMovementRequest.SourceLocation),
                "Source and destination must be different."));
            errors.Add(new MutationError(
                nameof(CreateMovementRequest.DestinationLocation),
                "Source and destination must be different."));
        }

        if (request.EstimatedTonnes <= 0m || request.EstimatedTonnes > 500m)
        {
            errors.Add(new MutationError(
                nameof(CreateMovementRequest.EstimatedTonnes),
                "Estimated tonnes must be greater than 0 and no more than 500."));
        }

        if (!Enum.IsDefined(request.Priority))
        {
            errors.Add(new MutationError(
                nameof(CreateMovementRequest.Priority),
                "Priority must be Normal or Urgent."));
        }

        return errors;
    }

    public static bool IsTerminal(MovementStatus status) =>
        status is MovementStatus.Completed or MovementStatus.Cancelled;

    public static bool RequiresEquipment(MovementStatus status) =>
        status is MovementStatus.Assigned or MovementStatus.InProgress;

    public static bool IsTransitionAllowed(MovementStatus current, MovementStatus target) =>
        current switch
        {
            MovementStatus.Requested => target is MovementStatus.Requested
                or MovementStatus.Assigned
                or MovementStatus.Cancelled,
            MovementStatus.Assigned => target is MovementStatus.Assigned
                or MovementStatus.InProgress
                or MovementStatus.Cancelled,
            MovementStatus.InProgress => target is MovementStatus.InProgress
                or MovementStatus.Completed
                or MovementStatus.Cancelled,
            _ => false
        };
}

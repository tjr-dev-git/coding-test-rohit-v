using Dispatch.Api.Contracts;
using Dispatch.Api.Domain;

namespace Dispatch.Api.Services;

public enum MutationOutcome
{
    Success,
    Invalid,
    NotFound,
    Conflict
}

public sealed record MutationError(string Field, string Message);

public sealed record MutationResult(
    MutationOutcome Outcome,
    MovementDto? Movement = null,
    string? Error = null,
    IReadOnlyList<MutationError>? Errors = null,
    MovementDto? Current = null)
{
    public static MutationResult Success(MovementDto movement) => new(MutationOutcome.Success, movement);
    public static MutationResult Invalid(string error) => new(MutationOutcome.Invalid, Error: error);
    public static MutationResult Invalid(string field, string message) =>
        Invalid([new MutationError(field, message)]);
    public static MutationResult Invalid(IReadOnlyList<MutationError> errors) =>
        new(MutationOutcome.Invalid, Error: errors.FirstOrDefault()?.Message, Errors: errors);
    public static MutationResult NotFound(string error) => new(MutationOutcome.NotFound, Error: error);
    public static MutationResult Conflict(string error, MovementDto? current = null) =>
        new(MutationOutcome.Conflict, Error: error, Current: current);
}

public interface IMovementService
{
    Task<IReadOnlyList<SiteDto>> GetSitesAsync(CancellationToken cancellationToken);
    Task<IReadOnlyList<MovementDto>> GetMovementsAsync(
        string siteId,
        DateOnly shiftDate,
        ShiftKind shift,
        MovementStatus? status,
        CancellationToken cancellationToken);
    Task<IReadOnlyList<EquipmentDto>> GetEquipmentAsync(string siteId, CancellationToken cancellationToken);
    Task<MutationResult> CreateAsync(string siteId, CreateMovementRequest request, CancellationToken cancellationToken);
    Task<MutationResult> UpdateAsync(string siteId, string movementId, UpdateMovementRequest request, CancellationToken cancellationToken);
}

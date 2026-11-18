using Dispatch.Api.Contracts;
using Dispatch.Api.Domain;
using Dispatch.Api.Services;
using Microsoft.AspNetCore.Mvc;

namespace Dispatch.Api.Controllers;

[ApiController]
[Route("api/sites/{siteId}/movements")]
public sealed class MovementsController(IMovementService movementService) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType<IReadOnlyList<MovementDto>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<MovementDto>>> Get(
        string siteId,
        [FromQuery] DateOnly shiftDate,
        [FromQuery] ShiftKind shift,
        [FromQuery] MovementStatus? status,
        CancellationToken cancellationToken) =>
        Ok(await movementService.GetMovementsAsync(siteId, shiftDate, shift, status, cancellationToken));

    [HttpPost]
    [ProducesResponseType<MovementDto>(StatusCodes.Status201Created)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<MovementDto>> Create(
        string siteId,
        CreateMovementRequest request,
        CancellationToken cancellationToken)
    {
        var result = await movementService.CreateAsync(siteId, request, cancellationToken);
        return MapMutation(result, StatusCodes.Status201Created);
    }

    [HttpPatch("{movementId}")]
    [ProducesResponseType<MovementDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<MovementDto>> Update(
        string siteId,
        string movementId,
        UpdateMovementRequest request,
        CancellationToken cancellationToken)
    {
        var result = await movementService.UpdateAsync(
            siteId,
            movementId,
            request,
            cancellationToken);
        return MapMutation(result, StatusCodes.Status200OK);
    }

    private ActionResult<MovementDto> MapMutation(MutationResult result, int successStatusCode)
    {
        if (result.Outcome == MutationOutcome.Success)
        {
            if (result.Movement is null)
            {
                throw new InvalidOperationException("A successful mutation must include a movement.");
            }

            return StatusCode(successStatusCode, result.Movement);
        }

        if (result.Outcome == MutationOutcome.Invalid)
        {
            return BadRequest(new ValidationProblemDetails(ToValidationErrors(result))
            {
                Title = "The movement request is invalid.",
                Status = StatusCodes.Status400BadRequest
            });
        }

        var statusCode = result.Outcome == MutationOutcome.NotFound
            ? StatusCodes.Status404NotFound
            : StatusCodes.Status409Conflict;
        var problem = new ProblemDetails
        {
            Title = result.Outcome == MutationOutcome.NotFound
                ? "The requested resource was not found."
                : "Movement update conflict.",
            Detail = result.Error,
            Status = statusCode
        };

        if (result.Outcome == MutationOutcome.Conflict && result.Current is not null)
        {
            problem.Extensions["current"] = result.Current;
        }

        if (result.Outcome == MutationOutcome.NotFound)
        {
            return NotFound(problem);
        }

        return Conflict(problem);
    }

    private static Dictionary<string, string[]> ToValidationErrors(MutationResult result)
    {
        if (result.Errors is { Count: > 0 })
        {
            return result.Errors
                .GroupBy(error => error.Field, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(
                    group => group.Key,
                    group => group.Select(error => error.Message).ToArray(),
                    StringComparer.OrdinalIgnoreCase);
        }

        return new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase)
        {
            ["movement"] = [result.Error ?? "The movement request is invalid."]
        };
    }
}

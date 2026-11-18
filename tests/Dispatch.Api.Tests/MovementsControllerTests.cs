using Dispatch.Api.Contracts;
using Dispatch.Api.Controllers;
using Dispatch.Api.Domain;
using Dispatch.Api.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moq;
using Xunit;

namespace Dispatch.Api.Tests;

public sealed class MovementsControllerTests
{
    [Fact]
    public async Task Get_returns_service_results()
    {
        var expected = new MovementDto(
            "move-1",
            "IND-JSP",
            "Steel slag",
            "Slag Pit East",
            "Recovery Plant",
            42m,
            MovementPriority.Urgent,
            MovementStatus.Requested,
            null,
            DateTimeOffset.Parse("2026-08-17T07:00:00Z"),
            DateTimeOffset.Parse("2026-08-17T07:00:00Z"),
            "etag-1");
        var service = new Mock<IMovementService>(MockBehavior.Strict);
        service.Setup(x => x.GetMovementsAsync(
                "IND-JSP",
                new DateOnly(2026, 8, 17),
                ShiftKind.Day,
                null,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync([expected]);
        var controller = new MovementsController(service.Object);

        var result = await controller.Get(
            "IND-JSP",
            new DateOnly(2026, 8, 17),
            ShiftKind.Day,
            null,
            CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var movements = Assert.IsAssignableFrom<IReadOnlyList<MovementDto>>(ok.Value);
        Assert.Equal(expected, Assert.Single(movements));
    }

    [Fact]
    public async Task Create_returns_created_representation_when_service_succeeds()
    {
        var expected = NewMovementDto();
        var request = NewCreateRequest();
        var service = new Mock<IMovementService>(MockBehavior.Strict);
        service.Setup(x => x.CreateAsync("IND-JSP", request, It.IsAny<CancellationToken>()))
            .ReturnsAsync(MutationResult.Success(expected));
        var controller = new MovementsController(service.Object);

        var result = await controller.Create("IND-JSP", request, CancellationToken.None);

        var created = Assert.IsType<ObjectResult>(result.Result);
        Assert.Equal<int?>(StatusCodes.Status201Created, created.StatusCode);
        Assert.Equal(expected, Assert.IsType<MovementDto>(created.Value));
    }

    [Fact]
    public async Task Create_returns_field_validation_details_when_service_rejects_request()
    {
        var request = NewCreateRequest();
        var errors = new[]
        {
            new MutationError("DestinationLocation", "Source and destination must be different.")
        };
        var service = new Mock<IMovementService>(MockBehavior.Strict);
        service.Setup(x => x.CreateAsync("IND-JSP", request, It.IsAny<CancellationToken>()))
            .ReturnsAsync(MutationResult.Invalid(errors));
        var controller = new MovementsController(service.Object);

        var result = await controller.Create("IND-JSP", request, CancellationToken.None);

        var badRequest = Assert.IsType<BadRequestObjectResult>(result.Result);
        Assert.Equal<int?>(StatusCodes.Status400BadRequest, badRequest.StatusCode);
        var problem = Assert.IsType<ValidationProblemDetails>(badRequest.Value);
        Assert.Equal<int?>(StatusCodes.Status400BadRequest, problem.Status);
        Assert.Contains("DestinationLocation", problem.Errors.Keys);
        Assert.Contains(
            "Source and destination must be different.",
            problem.Errors["DestinationLocation"]);
    }

    [Fact]
    public async Task Create_returns_not_found_when_site_is_missing()
    {
        var request = NewCreateRequest();
        var service = new Mock<IMovementService>(MockBehavior.Strict);
        service.Setup(x => x.CreateAsync("MISSING", request, It.IsAny<CancellationToken>()))
            .ReturnsAsync(MutationResult.NotFound("Site 'MISSING' was not found."));
        var controller = new MovementsController(service.Object);

        var result = await controller.Create("MISSING", request, CancellationToken.None);

        var notFound = Assert.IsType<NotFoundObjectResult>(result.Result);
        Assert.Equal<int?>(StatusCodes.Status404NotFound, notFound.StatusCode);
        var problem = Assert.IsType<ProblemDetails>(notFound.Value);
        Assert.Equal<int?>(StatusCodes.Status404NotFound, problem.Status);
        Assert.Equal("Site 'MISSING' was not found.", problem.Detail);
    }

    [Fact]
    public async Task Create_returns_generic_validation_key_when_service_gives_no_field_errors()
    {
        var request = NewCreateRequest();
        var service = new Mock<IMovementService>(MockBehavior.Strict);
        service.Setup(x => x.CreateAsync("IND-JSP", request, It.IsAny<CancellationToken>()))
            .ReturnsAsync(MutationResult.Invalid("The request is invalid."));
        var controller = new MovementsController(service.Object);

        var result = await controller.Create("IND-JSP", request, CancellationToken.None);

        var badRequest = Assert.IsType<BadRequestObjectResult>(result.Result);
        var problem = Assert.IsType<ValidationProblemDetails>(badRequest.Value);
        Assert.Contains("movement", problem.Errors.Keys);
        Assert.Contains("The request is invalid.", problem.Errors["movement"]);
    }

    [Fact]
    public async Task Update_returns_updated_representation_when_service_succeeds()
    {
        var request = NewUpdateRequest();
        var expected = NewMovementDto() with
        {
            Status = MovementStatus.Assigned,
            EquipmentId = "PC-17",
            ETag = "etag-2"
        };
        var service = new Mock<IMovementService>(MockBehavior.Strict);
        service.Setup(x => x.UpdateAsync("IND-JSP", "move-1", request, It.IsAny<CancellationToken>()))
            .ReturnsAsync(MutationResult.Success(expected));
        var controller = new MovementsController(service.Object);

        var result = await controller.Update("IND-JSP", "move-1", request, CancellationToken.None);

        var ok = Assert.IsType<ObjectResult>(result.Result);
        Assert.Equal<int?>(StatusCodes.Status200OK, ok.StatusCode);
        Assert.Equal(expected, Assert.IsType<MovementDto>(ok.Value));
    }

    [Fact]
    public async Task Update_returns_field_validation_details_when_service_rejects_transition()
    {
        var request = new UpdateMovementRequest
        {
            Status = MovementStatus.Completed,
            EquipmentId = "PC-17",
            ETag = "etag-1"
        };
        var service = new Mock<IMovementService>(MockBehavior.Strict);
        service.Setup(x => x.UpdateAsync("IND-JSP", "move-1", request, It.IsAny<CancellationToken>()))
            .ReturnsAsync(MutationResult.Invalid(
                "Status",
                "A movement cannot move from Assigned to Completed."));
        var controller = new MovementsController(service.Object);

        var result = await controller.Update("IND-JSP", "move-1", request, CancellationToken.None);

        var badRequest = Assert.IsType<BadRequestObjectResult>(result.Result);
        var problem = Assert.IsType<ValidationProblemDetails>(badRequest.Value);
        Assert.Contains("Status", problem.Errors.Keys);
        Assert.Contains(
            "A movement cannot move from Assigned to Completed.",
            problem.Errors["Status"]);
    }

    [Fact]
    public async Task Update_returns_not_found_when_movement_is_missing()
    {
        var request = NewUpdateRequest();
        var service = new Mock<IMovementService>(MockBehavior.Strict);
        service.Setup(x => x.UpdateAsync("IND-JSP", "missing", request, It.IsAny<CancellationToken>()))
            .ReturnsAsync(MutationResult.NotFound(
                "Movement 'missing' was not found at site 'IND-JSP'."));
        var controller = new MovementsController(service.Object);

        var result = await controller.Update("IND-JSP", "missing", request, CancellationToken.None);

        var notFound = Assert.IsType<NotFoundObjectResult>(result.Result);
        Assert.Equal<int?>(StatusCodes.Status404NotFound, notFound.StatusCode);
        var problem = Assert.IsType<ProblemDetails>(notFound.Value);
        Assert.Equal("Movement 'missing' was not found at site 'IND-JSP'.", problem.Detail);
    }

    [Fact]
    public async Task Update_returns_conflict_with_current_representation_for_stale_etag()
    {
        var request = NewUpdateRequest();
        var current = NewMovementDto() with
        {
            Status = MovementStatus.InProgress,
            EquipmentId = "PC-18",
            ETag = "etag-newer"
        };
        var service = new Mock<IMovementService>(MockBehavior.Strict);
        service.Setup(x => x.UpdateAsync("IND-JSP", "move-1", request, It.IsAny<CancellationToken>()))
            .ReturnsAsync(MutationResult.Conflict(
                "This movement was updated by another dispatcher. Reload it and try again.",
                current));
        var controller = new MovementsController(service.Object);

        var result = await controller.Update("IND-JSP", "move-1", request, CancellationToken.None);

        var conflict = Assert.IsType<ConflictObjectResult>(result.Result);
        Assert.Equal<int?>(StatusCodes.Status409Conflict, conflict.StatusCode);
        var problem = Assert.IsType<ProblemDetails>(conflict.Value);
        Assert.Equal<int?>(StatusCodes.Status409Conflict, problem.Status);
        Assert.Equal(current, problem.Extensions["current"]);
    }

    private static MovementDto NewMovementDto() =>
        new(
            "move-created",
            "IND-JSP",
            "Steel slag",
            "Slag Pit East",
            "Recovery Plant",
            42m,
            MovementPriority.Urgent,
            MovementStatus.Requested,
            null,
            DateTimeOffset.Parse("2026-08-17T07:00:00Z"),
            DateTimeOffset.Parse("2026-08-17T07:00:00Z"),
            "etag-created");

    private static CreateMovementRequest NewCreateRequest() =>
        new()
        {
            MaterialType = "Steel slag",
            SourceLocation = "Slag Pit East",
            DestinationLocation = "Recovery Plant",
            EstimatedTonnes = 42m,
            Priority = MovementPriority.Urgent
        };

    private static UpdateMovementRequest NewUpdateRequest() =>
        new()
        {
            Status = MovementStatus.Assigned,
            EquipmentId = "PC-17",
            ETag = "etag-1"
        };
}

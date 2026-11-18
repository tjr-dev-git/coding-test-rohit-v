using Dispatch.Api.Contracts;
using Dispatch.Api.Services;
using Microsoft.AspNetCore.Mvc;

namespace Dispatch.Api.Controllers;

[ApiController]
[Route("api/sites")]
public sealed class SitesController(IMovementService movementService) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType<IReadOnlyList<SiteDto>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<SiteDto>>> Get(CancellationToken cancellationToken) =>
        Ok(await movementService.GetSitesAsync(cancellationToken));

    [HttpGet("{siteId}/equipment")]
    [ProducesResponseType<IReadOnlyList<EquipmentDto>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<EquipmentDto>>> GetEquipment(
        string siteId,
        CancellationToken cancellationToken) =>
        Ok(await movementService.GetEquipmentAsync(siteId, cancellationToken));
}


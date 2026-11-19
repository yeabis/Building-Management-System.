using BMS.API.Models;
using BMS.Application.Common;
using BMS.Application.DTOs;
using BMS.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BMS.API.Controllers;

[Route("api/units")]
public class UnitsController(IUnitService units) : ApiControllerBase
{

    [HttpGet]
    public async Task<ActionResult<ApiResponse<PagedResult<UnitDto>>>> GetAll([FromQuery] UnitQuery query, CancellationToken ct) =>
        Success(await units.GetUnitsAsync(query, ct));

    [HttpGet("floors")]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<FloorSummaryDto>>>> Floors(CancellationToken ct) =>
        Success(await units.GetFloorSummaryAsync(ct));

    [HttpGet("{id:int}")]
    public async Task<ActionResult<ApiResponse<UnitDto>>> GetById(int id, CancellationToken ct) =>
        Success(await units.GetByIdAsync(id, ct));

    [HttpPost, Authorize(Roles = Roles.WriteAccess)]
    public async Task<ActionResult<ApiResponse<UnitDto>>> Create([FromBody] UnitInputDto input, CancellationToken ct)
    {
        var unit = await units.CreateUnitAsync(input, ct);
        return CreatedAtAction(nameof(GetById), new { id = unit.Id }, ApiResponse.Ok(unit, "Unit created."));
    }

    [HttpPut("{id:int}"), Authorize(Roles = Roles.WriteAccess)]
    public async Task<ActionResult<ApiResponse<UnitDto>>> Update(int id, [FromBody] UnitInputDto input, CancellationToken ct) =>
        Success(await units.UpdateUnitAsync(id, input, ct), "Unit updated.");

    [HttpPut("{id:int}/status"), Authorize(Roles = Roles.WriteAccess)]
    public async Task<ActionResult<ApiResponse<UnitDto>>> ChangeStatus(int id, [FromBody] ChangeUnitStatusRequest request, CancellationToken ct) =>
        Success(await units.ChangeStatusAsync(id, request.Status, ct), "Status updated.");

    [HttpDelete("{id:int}"), Authorize(Roles = Roles.WriteAccess)]
    public async Task<ActionResult<ApiResponse<object?>>> Delete(int id, CancellationToken ct)
    {
        await units.DeleteUnitAsync(id, ct);
        return Success("Unit deleted.");
    }
}


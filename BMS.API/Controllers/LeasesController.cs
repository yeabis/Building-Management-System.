using BMS.API.Models;
using BMS.Application.Common;
using BMS.Application.DTOs;
using BMS.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BMS.API.Controllers;

[Route("api/leases")]
public class LeasesController(ILeaseService leases) : ApiControllerBase
{

    [HttpGet]
    public async Task<ActionResult<ApiResponse<PagedResult<LeaseDto>>>> GetAll([FromQuery] LeaseQuery query, CancellationToken ct) =>
        Success(await leases.GetLeasesAsync(query, ct));

    [HttpGet("expiring")]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<LeaseExpiryRow>>>> Expiring([FromQuery] int days = 30, CancellationToken ct = default) =>
        Success(await leases.GetExpiringLeasesAsync(Math.Clamp(days, 1, 365), ct));

    [HttpGet("{id:int}")]
    public async Task<ActionResult<ApiResponse<LeaseDto>>> GetById(int id, CancellationToken ct) =>
        Success(await leases.GetByIdAsync(id, ct));

    [HttpPost, Authorize(Roles = Roles.WriteAccess)]
    public async Task<ActionResult<ApiResponse<LeaseDto>>> Create([FromBody] LeaseInputDto input, CancellationToken ct)
    {
        var lease = await leases.CreateLeaseAsync(input, ct);
        return CreatedAtAction(nameof(GetById), new { id = lease.Id }, ApiResponse.Ok(lease, "Lease created."));
    }

    [HttpPut("{id:int}/renew"), Authorize(Roles = Roles.WriteAccess)]
    public async Task<ActionResult<ApiResponse<LeaseDto>>> Renew(int id, [FromBody] RenewLeaseRequest request, CancellationToken ct) =>
        Success(await leases.RenewLeaseAsync(id, request, ct), "Lease renewed.");

    [HttpPut("{id:int}/terminate"), Authorize(Roles = Roles.WriteAccess)]
    public async Task<ActionResult<ApiResponse<LeaseDto>>> Terminate(int id, [FromBody] TerminateLeaseRequest request, CancellationToken ct) =>
        Success(await leases.TerminateLeaseAsync(id, request, ct), "Lease terminated.");
}


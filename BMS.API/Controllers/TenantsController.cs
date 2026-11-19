using BMS.API.Models;
using BMS.Application.Common;
using BMS.Application.DTOs;
using BMS.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BMS.API.Controllers;

[Route("api/tenants")]
public class TenantsController(ITenantService tenants) : ApiControllerBase
{

    [HttpGet]
    public async Task<ActionResult<ApiResponse<PagedResult<TenantDto>>>> GetAll([FromQuery] TenantQuery query, CancellationToken ct) =>
        Success(await tenants.GetTenantsAsync(query, ct));

    [HttpGet("{id:int}")]
    public async Task<ActionResult<ApiResponse<TenantDto>>> GetById(int id, CancellationToken ct) =>
        Success(await tenants.GetByIdAsync(id, ct));

    [HttpPost, Authorize(Roles = Roles.WriteAccess)]
    public async Task<ActionResult<ApiResponse<TenantDto>>> Register([FromBody] TenantInputDto input, CancellationToken ct)
    {
        var tenant = await tenants.RegisterTenantAsync(input, ct);
        return CreatedAtAction(nameof(GetById), new { id = tenant.Id }, ApiResponse.Ok(tenant, "Tenant registered."));
    }

    [HttpPut("{id:int}"), Authorize(Roles = Roles.WriteAccess)]
    public async Task<ActionResult<ApiResponse<TenantDto>>> Update(int id, [FromBody] TenantInputDto input, CancellationToken ct) =>
        Success(await tenants.UpdateTenantAsync(id, input, ct), "Tenant updated.");

    [HttpPut("{id:int}/deactivate"), Authorize(Roles = Roles.WriteAccess)]
    public async Task<ActionResult<ApiResponse<object?>>> Deactivate(int id, CancellationToken ct)
    {
        await tenants.DeactivateTenantAsync(id, ct);
        return Success("Tenant deactivated.");
    }

    [HttpPost("{id:int}/documents"), Authorize(Roles = Roles.WriteAccess)]
    [Consumes("multipart/form-data")]
    [RequestSizeLimit(6 * 1024 * 1024)]
    public async Task<ActionResult<ApiResponse<LegalDocumentDto>>> UploadDocument(int id, [FromForm] DocumentUploadForm form, CancellationToken ct)
    {
        await using var stream = form.File.OpenReadStream();
        var doc = await tenants.UploadDocumentAsync(
            id, new DocumentUploadRequest(form.DocumentType, form.ExpiryDate), stream, form.File.FileName, form.File.Length, ct);
        return Success(doc, "Document uploaded.");
    }

    [HttpPut("{id:int}/documents/{documentId:int}/verify"), Authorize(Roles = Roles.WriteAccess)]
    public async Task<ActionResult<ApiResponse<LegalDocumentDto>>> VerifyDocument(int id, int documentId, CancellationToken ct) =>
        Success(await tenants.VerifyDocumentAsync(id, documentId, ct), "Document verified.");

    [HttpGet("{id:int}/documents/{documentId:int}/download")]
    public async Task<IActionResult> DownloadDocument(int id, int documentId, CancellationToken ct)
    {
        var file = await tenants.GetDocumentFileAsync(id, documentId, ct);
        return File(file.Content, file.ContentType, file.FileName);
    }
}


using BMS.API.Models;
using BMS.Application.Common;
using BMS.Application.DTOs;
using BMS.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BMS.API.Controllers;

[Route("api/payments")]
public class PaymentsController(IPaymentService payments) : ApiControllerBase
{

    [HttpGet]
    public async Task<ActionResult<ApiResponse<PagedResult<PaymentDto>>>> History([FromQuery] PaymentQuery query, CancellationToken ct) =>
        Success(await payments.GetPaymentHistoryAsync(query, ct));

    [HttpGet("outstanding")]
    public async Task<ActionResult<ApiResponse<PagedResult<InvoiceDto>>>> Outstanding([FromQuery] PageQuery query, CancellationToken ct) =>
        Success(await payments.GetOutstandingBalancesAsync(query, ct));

    [HttpPost, Authorize(Roles = Roles.WriteAccess)]
    public async Task<ActionResult<ApiResponse<PaymentDto>>> Record([FromBody] PaymentInputDto input, CancellationToken ct)
    {
        var payment = await payments.RecordPaymentAsync(input, ct);
        return StatusCode(StatusCodes.Status201Created, ApiResponse.Ok(payment, "Payment recorded."));
    }
}

[Route("api/invoices")]
public class InvoicesController(IInvoiceService invoices) : ApiControllerBase
{
    [HttpGet]
    public async Task<ActionResult<ApiResponse<PagedResult<InvoiceDto>>>> GetAll([FromQuery] InvoiceQuery query, CancellationToken ct) =>
        Success(await invoices.GetInvoicesAsync(query, ct));

    [HttpGet("unpaid")]
    public async Task<ActionResult<ApiResponse<PagedResult<InvoiceDto>>>> Unpaid([FromQuery] PageQuery query, CancellationToken ct) =>
        Success(await invoices.GetUnpaidInvoicesAsync(query, ct));

    [HttpPost("generate"), Authorize(Roles = Roles.WriteAccess)]
    public async Task<ActionResult<ApiResponse<GenerateInvoicesResult>>> Generate([FromBody] GenerateInvoicesRequest? request, CancellationToken ct)
    {
        var result = await invoices.GenerateInvoicesAsync(request, ct);
        return Success(result, result.Generated == 0 ? "All invoices for this period already exist." : $"{result.Generated} invoice(s) generated.");
    }

    [HttpPut("{id:int}/mark-paid"), Authorize(Roles = Roles.WriteAccess)]
    public async Task<ActionResult<ApiResponse<InvoiceDto>>> MarkPaid(int id, [FromBody] MarkPaidRequest? request, CancellationToken ct) =>
        Success(await invoices.MarkAsPaidAsync(id, request, ct), "Invoice marked as paid.");
}


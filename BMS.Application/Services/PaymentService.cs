using BMS.Application.Common;
using BMS.Application.Domain;
using BMS.Application.DTOs;
using BMS.Application.Interfaces;
using BMS.Application.Mapping;
using Microsoft.EntityFrameworkCore;

namespace BMS.Application.Services;

public class PaymentService(
    IRepository<Payment> payments,
    IRepository<Invoice> invoices,
    IInvoiceService invoiceService,
    IClock clock,
    IUnitOfWork uow) : IPaymentService
{
    public async Task<PaymentDto> RecordPaymentAsync(PaymentInputDto input, CancellationToken ct = default)
    {
        var today = clock.Today;
        if (input.PaymentDate > today) throw new BusinessRuleException("The payment date cannot be in the future.");

        var invoice = await invoices.QueryTracked().WithDetails().FirstOrDefaultAsync(i => i.Id == input.InvoiceId, ct)
                      ?? throw new NotFoundException("Invoice not found.");
        if (invoice.Status is InvoiceStatus.Cancelled or InvoiceStatus.Draft)
            throw new BusinessRuleException("Payments can only be recorded against issued invoices.");

        var paidSoFar = invoice.Payments.Sum(p => p.AmountPaid);
        var balance = invoice.AmountDue - paidSoFar;
        if (balance <= 0) throw new BusinessRuleException("This invoice is already fully paid.");
        if (input.AmountPaid > balance)
            throw new BusinessRuleException($"The amount exceeds the outstanding balance of ETB {balance:N2}.");

        var payment = new Payment
        {
            InvoiceId = invoice.Id,
            AmountPaid = input.AmountPaid,
            PaymentDate = input.PaymentDate,
            PaymentMethod = input.PaymentMethod,
            ReferenceNumber = input.ReferenceNumber.Trim(),
            Notes = string.IsNullOrWhiteSpace(input.Notes) ? null : input.Notes.Trim(),
            Invoice = invoice
        };
        await payments.AddAsync(payment, ct);

        invoice.Status = paidSoFar + input.AmountPaid >= invoice.AmountDue
            ? InvoiceStatus.Paid
            : invoice.DueDate < today ? InvoiceStatus.Overdue : InvoiceStatus.Issued;
        invoice.UpdatedAt = clock.UtcNow;

        await uow.SaveChangesAsync(ct);
        return payment.ToDto();
    }

    public Task<PagedResult<InvoiceDto>> GetOutstandingBalancesAsync(PageQuery query, CancellationToken ct = default) =>
        invoiceService.GetUnpaidInvoicesAsync(query, ct);

    public async Task<PagedResult<PaymentDto>> GetPaymentHistoryAsync(PaymentQuery q, CancellationToken ct = default)
    {
        var query = payments.Query().WithDetails();
        if (q.InvoiceId.HasValue)
        {
            var invoiceId = q.InvoiceId.Value;
            query = query.Where(p => p.InvoiceId == invoiceId);
        }
        if (q.TenantId.HasValue)
        {
            var tenantId = q.TenantId.Value;
            query = query.Where(p => p.Invoice.Lease.TenantId == tenantId);
        }

        var page = await query.OrderByDescending(p => p.PaymentDate).ThenByDescending(p => p.Id)
            .ToPagedAsync(q.Page, q.PageSize, ct);
        return page.Map(p => p.ToDto());
    }
}


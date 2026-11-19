using BMS.Application.Common;
using BMS.Application.Domain;
using BMS.Application.DTOs;
using BMS.Application.Interfaces;
using BMS.Application.Mapping;
using Microsoft.EntityFrameworkCore;

namespace BMS.Application.Services;

public class InvoiceService(
    IRepository<Invoice> invoices,
    IRepository<Lease> leases,
    IRepository<Payment> payments,
    ICurrentUser currentUser,
    IClock clock,
    IUnitOfWork uow) : IInvoiceService
{

    private const int DueDayOfMonth = 5;

    public async Task<GenerateInvoicesResult> GenerateInvoicesAsync(GenerateInvoicesRequest? request, CancellationToken ct = default)
    {
        var today = clock.Today;
        var year = request?.Year ?? today.Year;
        var month = request?.Month ?? today.Month;
        var periodStart = new DateOnly(year, month, 1);
        var periodEnd = periodStart.AddMonths(1).AddDays(-1);

        var activeLeases = await leases.Query()
            .Where(l => l.Status == LeaseStatus.Active && l.StartDate <= periodEnd && l.EndDate >= periodStart)
            .Select(l => new { l.Id, l.MonthlyRent })
            .ToListAsync(ct);

        var alreadyBilled = (await invoices.Query()
                .Where(i => i.PeriodYear == year && i.PeriodMonth == month)
                .Select(i => i.LeaseId).ToListAsync(ct))
            .ToHashSet();

        var dueDate = periodStart.AddDays(DueDayOfMonth - 1);
        var created = new List<Invoice>();
        foreach (var lease in activeLeases.Where(l => !alreadyBilled.Contains(l.Id)))
        {
            created.Add(new Invoice
            {
                LeaseId = lease.Id,
                InvoiceNumber = $"INV-{year}{month:D2}-{lease.Id:D4}",
                AmountDue = lease.MonthlyRent,
                IssueDate = periodStart,
                DueDate = dueDate,
                PeriodMonth = month,
                PeriodYear = year,
                Status = dueDate < today ? InvoiceStatus.Overdue : InvoiceStatus.Issued
            });
        }

        if (created.Count > 0)
        {
            await invoices.AddRangeAsync(created, ct);
            await uow.SaveChangesAsync(ct);
        }
        return new GenerateInvoicesResult(created.Count);
    }

    public async Task<PagedResult<InvoiceDto>> GetInvoicesAsync(InvoiceQuery q, CancellationToken ct = default)
    {
        var query = invoices.Query().WithDetails();
        if (q.Status.HasValue)
        {
            var status = q.Status.Value;
            query = query.Where(i => i.Status == status);
        }
        if (q.LeaseId.HasValue)
        {
            var leaseId = q.LeaseId.Value;
            query = query.Where(i => i.LeaseId == leaseId);
        }
        if (q.TenantId.HasValue)
        {
            var tenantId = q.TenantId.Value;
            query = query.Where(i => i.Lease.TenantId == tenantId);
        }

        var page = await query.OrderByDescending(i => i.PeriodYear).ThenByDescending(i => i.PeriodMonth).ThenBy(i => i.Id)
            .ToPagedAsync(q.Page, q.PageSize, ct);
        return page.Map(i => i.ToDto());
    }

    public async Task<PagedResult<InvoiceDto>> GetUnpaidInvoicesAsync(PageQuery q, CancellationToken ct = default)
    {
        await MarkOverdueAsync(ct);

        var page = await invoices.Query().WithDetails()
            .Where(i => (i.Status == InvoiceStatus.Issued || i.Status == InvoiceStatus.Overdue)
                        && i.AmountDue > i.Payments.Sum(p => p.AmountPaid))
            .OrderBy(i => i.DueDate).ThenBy(i => i.Id)
            .ToPagedAsync(q.Page, q.PageSize, ct);
        return page.Map(i => i.ToDto());
    }

    public async Task<InvoiceDto> MarkAsPaidAsync(int id, MarkPaidRequest? request, CancellationToken ct = default)
    {
        var invoice = await invoices.QueryTracked().WithDetails().FirstOrDefaultAsync(i => i.Id == id, ct)
                      ?? throw new NotFoundException("Invoice not found.");
        if (invoice.Status is InvoiceStatus.Cancelled or InvoiceStatus.Draft)
            throw new BusinessRuleException("Only issued invoices can be marked as paid.");

        var balance = invoice.AmountDue - invoice.Payments.Sum(p => p.AmountPaid);
        if (balance <= 0) throw new BusinessRuleException("This invoice is already fully paid.");

        var payment = new Payment
        {
            InvoiceId = invoice.Id,
            AmountPaid = balance,
            PaymentDate = clock.Today,
            PaymentMethod = request?.Method ?? PaymentMethod.Cash,
            ReferenceNumber = string.IsNullOrWhiteSpace(request?.ReferenceNumber)
                ? $"MANUAL-{invoice.InvoiceNumber}" : request!.ReferenceNumber!.Trim(),
            Notes = $"Marked as paid by {currentUser.UserName}",
            Invoice = invoice
        };
        await payments.AddAsync(payment, ct);
        invoice.Status = InvoiceStatus.Paid;
        invoice.UpdatedAt = clock.UtcNow;
        await uow.SaveChangesAsync(ct);

        return invoice.ToDto();
    }

    public async Task<int> MarkOverdueAsync(CancellationToken ct = default)
    {
        var today = clock.Today;
        var late = await invoices.QueryTracked()
            .Where(i => i.Status == InvoiceStatus.Issued && i.DueDate < today)
            .ToListAsync(ct);

        foreach (var invoice in late) invoice.Status = InvoiceStatus.Overdue;
        if (late.Count > 0) await uow.SaveChangesAsync(ct);
        return late.Count;
    }
}


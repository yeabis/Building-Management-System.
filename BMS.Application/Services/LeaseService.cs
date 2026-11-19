using BMS.Application.Common;
using BMS.Application.Domain;
using BMS.Application.DTOs;
using BMS.Application.Interfaces;
using BMS.Application.Mapping;
using Microsoft.EntityFrameworkCore;

namespace BMS.Application.Services;

public class LeaseService(
    IRepository<Lease> leases,
    IRepository<Unit> units,
    IRepository<Tenant> tenants,
    IRepository<Invoice> invoices,
    IClock clock,
    IUnitOfWork uow) : ILeaseService
{
    public async Task<PagedResult<LeaseDto>> GetLeasesAsync(LeaseQuery q, CancellationToken ct = default)
    {
        var query = leases.Query().WithDetails();

        if (q.Status.HasValue)
        {
            var status = q.Status.Value;
            query = query.Where(l => l.Status == status);
        }
        if (q.TenantId.HasValue)
        {
            var tenantId = q.TenantId.Value;
            query = query.Where(l => l.TenantId == tenantId);
        }
        if (q.UnitId.HasValue)
        {
            var unitId = q.UnitId.Value;
            query = query.Where(l => l.UnitId == unitId);
        }

        var page = await query.OrderByDescending(l => l.StartDate).ThenByDescending(l => l.Id)
            .ToPagedAsync(q.Page, q.PageSize, ct);
        return page.Map(l => l.ToDto());
    }

    public async Task<LeaseDto> GetByIdAsync(int id, CancellationToken ct = default)
    {
        var lease = await leases.Query().WithDetails().FirstOrDefaultAsync(l => l.Id == id, ct)
                    ?? throw new NotFoundException("Lease not found.");
        return lease.ToDto();
    }

    public async Task<LeaseDto> CreateLeaseAsync(LeaseInputDto input, CancellationToken ct = default)
    {
        if (input.EndDate <= input.StartDate) throw new BusinessRuleException("End date must be after the start date.");

        var tenant = await tenants.GetByIdAsync(input.TenantId, ct) ?? throw new NotFoundException("Tenant not found.");
        if (!tenant.IsActive) throw new BusinessRuleException("This tenant is deactivated and cannot sign a new lease.");

        var unit = await units.GetByIdAsync(input.UnitId, ct) ?? throw new NotFoundException("Unit not found.");
        if (unit.Status is not (UnitStatus.Available or UnitStatus.Reserved))
            throw new ConflictException($"Unit {unit.UnitNumber} is not available for lease (status: {unit.Status}).");
        if (await leases.AnyAsync(l => l.UnitId == unit.Id && l.Status == LeaseStatus.Active, ct))
            throw new ConflictException($"Unit {unit.UnitNumber} already has an active lease.");

        var lease = new Lease
        {
            UnitId = unit.Id,
            TenantId = tenant.Id,
            StartDate = input.StartDate,
            EndDate = input.EndDate,
            MonthlyRent = input.MonthlyRent,
            DepositAmount = input.DepositAmount,
            Status = LeaseStatus.Active,
            Unit = unit,
            Tenant = tenant
        };
        unit.Status = UnitStatus.Occupied;
        await leases.AddAsync(lease, ct);
        await uow.SaveChangesAsync(ct);
        return lease.ToDto();
    }

    public async Task<LeaseDto> RenewLeaseAsync(int id, RenewLeaseRequest request, CancellationToken ct = default)
    {
        var lease = await leases.QueryTracked().WithDetails().FirstOrDefaultAsync(l => l.Id == id, ct)
                    ?? throw new NotFoundException("Lease not found.");
        if (lease.Status is not (LeaseStatus.Active or LeaseStatus.PendingRenewal))
            throw new BusinessRuleException("Only active leases can be renewed.");
        if (request.NewEndDate <= lease.EndDate)
            throw new BusinessRuleException("The new end date must be after the current end date.");

        lease.EndDate = request.NewEndDate;
        lease.MonthlyRent = request.NewMonthlyRent;
        lease.Status = LeaseStatus.Active;
        await uow.SaveChangesAsync(ct);
        return lease.ToDto();
    }

    public async Task<LeaseDto> TerminateLeaseAsync(int id, TerminateLeaseRequest request, CancellationToken ct = default)
    {
        var lease = await leases.QueryTracked().WithDetails().FirstOrDefaultAsync(l => l.Id == id, ct)
                    ?? throw new NotFoundException("Lease not found.");
        if (lease.Status is not (LeaseStatus.Active or LeaseStatus.PendingRenewal))
            throw new BusinessRuleException("Only active leases can be terminated.");

        lease.Status = LeaseStatus.Terminated;
        lease.TerminationReason = request.Reason.Trim();
        if (lease.Unit.Status == UnitStatus.Occupied) lease.Unit.Status = UnitStatus.Available;

        var today = clock.Today;
        var future = await invoices.QueryTracked()
            .Where(i => i.LeaseId == id && i.IssueDate > today
                        && (i.Status == InvoiceStatus.Draft || i.Status == InvoiceStatus.Issued)
                        && !i.Payments.Any())
            .ToListAsync(ct);
        foreach (var invoice in future) invoice.Status = InvoiceStatus.Cancelled;

        await uow.SaveChangesAsync(ct);
        return lease.ToDto();
    }

    public async Task<IReadOnlyList<LeaseExpiryRow>> GetExpiringLeasesAsync(int days = 30, CancellationToken ct = default)
    {
        var today = clock.Today;
        var horizon = today.AddDays(days);
        var rows = await leases.Query()
            .Where(l => l.Status == LeaseStatus.Active && l.EndDate >= today && l.EndDate <= horizon)
            .OrderBy(l => l.EndDate)
            .Select(l => new { Tenant = l.Tenant.OrganizationName, Unit = l.Unit.UnitNumber, l.EndDate, l.MonthlyRent })
            .ToListAsync(ct);

        return rows.Select(r => new LeaseExpiryRow(r.Tenant, r.Unit, r.EndDate, r.MonthlyRent, r.EndDate.DayNumber - today.DayNumber))
            .ToList();
    }

    public async Task<int> ExpireEndedLeasesAsync(CancellationToken ct = default)
    {
        var today = clock.Today;
        var ended = await leases.QueryTracked().Include(l => l.Unit)
            .Where(l => l.Status == LeaseStatus.Active && l.EndDate < today)
            .ToListAsync(ct);

        foreach (var lease in ended)
        {
            lease.Status = LeaseStatus.Expired;
            if (lease.Unit.Status == UnitStatus.Occupied) lease.Unit.Status = UnitStatus.Available;
        }
        if (ended.Count > 0) await uow.SaveChangesAsync(ct);
        return ended.Count;
    }
}


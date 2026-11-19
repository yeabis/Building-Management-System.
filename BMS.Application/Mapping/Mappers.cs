using BMS.Application.Domain;
using BMS.Application.DTOs;
using Microsoft.EntityFrameworkCore;

namespace BMS.Application.Mapping;

public static class Mappers
{
    public static UnitDto ToDto(this Unit u) => new(
        u.Id, u.FloorNumber, u.UnitNumber, u.UnitType, u.AreaSqMeters, u.MonthlyRent, u.Status, u.Description,
        u.Leases.FirstOrDefault(l => l.Status == LeaseStatus.Active)?.Tenant?.OrganizationName);

    public static LegalDocumentDto ToDto(this LegalDocument d) =>
        new(d.Id, d.TenantId, d.DocumentType, d.FileName, d.UploadedAt, d.ExpiryDate, d.IsVerified);

    public static TenantDto ToDto(this Tenant t, bool includeDocuments = false) => new(
        t.Id, t.OrganizationName, t.TinNumber, t.Phone, t.BusinessType, t.ContactPersonName, t.ContactEmail, t.IsActive,
        t.Leases.Where(l => l.Status == LeaseStatus.Active).Select(l => l.Unit.UnitNumber).OrderBy(n => n).ToList(),
        includeDocuments ? t.Documents.OrderByDescending(d => d.UploadedAt).Select(d => d.ToDto()).ToList() : null);

    public static LeaseDto ToDto(this Lease l) => new(
        l.Id, l.UnitId, l.Unit.UnitNumber, l.TenantId, l.Tenant.OrganizationName, l.StartDate, l.EndDate,
        l.MonthlyRent, l.DepositAmount, l.Status, l.TerminationReason);

    public static InvoiceDto ToDto(this Invoice i)
    {
        var paid = i.Payments.Sum(p => p.AmountPaid);
        return new InvoiceDto(i.Id, i.LeaseId, i.InvoiceNumber, i.Lease.Tenant.OrganizationName, i.Lease.Unit.UnitNumber,
            i.AmountDue, paid, Math.Max(0, i.AmountDue - paid), i.DueDate, i.IssueDate, i.Status, i.PeriodMonth, i.PeriodYear);
    }

    public static PaymentDto ToDto(this Payment p) => new(
        p.Id, p.InvoiceId, p.Invoice.InvoiceNumber, p.Invoice.Lease.Tenant.OrganizationName, p.AmountPaid,
        p.PaymentDate, p.PaymentMethod, p.ReferenceNumber, p.Notes);

    public static NotificationDto ToDto(this Notification n) =>
        new(n.Id, n.Title, n.Message, n.NotificationType, n.IsRead, n.CreatedAt);

    public static IQueryable<Unit> WithTenant(this IQueryable<Unit> q) =>
        q.Include(u => u.Leases.Where(l => l.Status == LeaseStatus.Active)).ThenInclude(l => l.Tenant);

    public static IQueryable<Tenant> WithActiveUnits(this IQueryable<Tenant> q) =>
        q.Include(t => t.Leases.Where(l => l.Status == LeaseStatus.Active)).ThenInclude(l => l.Unit);

    public static IQueryable<Lease> WithDetails(this IQueryable<Lease> q) =>
        q.Include(l => l.Unit).Include(l => l.Tenant);

    public static IQueryable<Invoice> WithDetails(this IQueryable<Invoice> q) =>
        q.Include(i => i.Payments).Include(i => i.Lease).ThenInclude(l => l.Tenant)
         .Include(i => i.Lease).ThenInclude(l => l.Unit);

    public static IQueryable<Payment> WithDetails(this IQueryable<Payment> q) =>
        q.Include(p => p.Invoice).ThenInclude(i => i.Lease).ThenInclude(l => l.Tenant);
}


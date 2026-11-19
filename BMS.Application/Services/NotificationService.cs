using System.Globalization;
using BMS.Application.Common;
using BMS.Application.Domain;
using BMS.Application.DTOs;
using BMS.Application.Interfaces;
using BMS.Application.Mapping;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace BMS.Application.Services;

public class NotificationService(
    IRepository<Notification> notifications,
    IRepository<Invoice> invoices,
    IRepository<Lease> leases,
    IRepository<LegalDocument> documents,
    UserManager<AppUser> users,
    ICurrentUser currentUser,
    IClock clock,
    IUnitOfWork uow) : INotificationService
{
    private record Alert(string Key, string Title, string Message);

    public async Task<PagedResult<NotificationDto>> GetMyNotificationsAsync(PageQuery q, CancellationToken ct = default)
    {
        var userId = RequireUser();
        var page = await notifications.Query().Where(n => n.UserId == userId)
            .OrderByDescending(n => n.CreatedAt).ThenByDescending(n => n.Id)
            .ToPagedAsync(q.Page, q.PageSize, ct);
        return page.Map(n => n.ToDto());
    }

    public async Task<int> GetUnreadCountAsync(CancellationToken ct = default)
    {
        var userId = RequireUser();
        return await notifications.Query().CountAsync(n => n.UserId == userId && !n.IsRead, ct);
    }

    public async Task MarkAsReadAsync(int id, CancellationToken ct = default)
    {
        var userId = RequireUser();
        var n = await notifications.QueryTracked().FirstOrDefaultAsync(x => x.Id == id && x.UserId == userId, ct)
                ?? throw new NotFoundException("Notification not found.");
        if (n.IsRead) return;
        n.IsRead = true;
        await uow.SaveChangesAsync(ct);
    }

    public async Task MarkAllAsReadAsync(CancellationToken ct = default)
    {
        var userId = RequireUser();
        var unread = await notifications.QueryTracked().Where(n => n.UserId == userId && !n.IsRead).ToListAsync(ct);
        foreach (var n in unread) n.IsRead = true;
        if (unread.Count > 0) await uow.SaveChangesAsync(ct);
    }

    public async Task<int> SendPaymentDueAlertsAsync(CancellationToken ct = default)
    {
        var today = clock.Today;
        var horizon = today.AddDays(7);
        var rows = await invoices.Query()
            .Where(i => i.Status == InvoiceStatus.Issued && i.DueDate >= today && i.DueDate <= horizon)
            .Select(i => new
            {
                i.Id, i.InvoiceNumber, i.DueDate, i.AmountDue,
                Paid = i.Payments.Sum(p => p.AmountPaid),
                Tenant = i.Lease.Tenant.OrganizationName,
                Unit = i.Lease.Unit.UnitNumber
            })
            .ToListAsync(ct);

        var alerts = rows.Where(r => r.AmountDue > r.Paid).Select(r => new Alert(
            $"inv:{r.Id}:due",
            "Payment due soon",
            $"Invoice {r.InvoiceNumber} for {r.Tenant} (unit {r.Unit}) of ETB {(r.AmountDue - r.Paid):N2} is due on {Fmt(r.DueDate)}.")).ToList();
        return await PublishAsync(NotificationType.PaymentDue, alerts, ct);
    }

    public async Task<int> SendOverdueAlertsAsync(CancellationToken ct = default)
    {
        var today = clock.Today;
        var rows = await invoices.Query()
            .Where(i => (i.Status == InvoiceStatus.Issued || i.Status == InvoiceStatus.Overdue) && i.DueDate < today)
            .Select(i => new
            {
                i.Id, i.InvoiceNumber, i.DueDate, i.AmountDue,
                Paid = i.Payments.Sum(p => p.AmountPaid),
                Tenant = i.Lease.Tenant.OrganizationName,
                Unit = i.Lease.Unit.UnitNumber
            })
            .ToListAsync(ct);

        var alerts = new List<Alert>();
        foreach (var r in rows.Where(r => r.AmountDue > r.Paid))
        {
            var daysLate = today.DayNumber - r.DueDate.DayNumber;
            var milestone = 1 + 7 * ((daysLate - 1) / 7);
            alerts.Add(new Alert(
                $"inv:{r.Id}:overdue:{milestone}",
                "Payment overdue",
                $"Invoice {r.InvoiceNumber} for {r.Tenant} (unit {r.Unit}) is {daysLate} day(s) overdue. Balance: ETB {(r.AmountDue - r.Paid):N2}."));
        }
        return await PublishAsync(NotificationType.PaymentOverdue, alerts, ct);
    }

    public async Task<int> SendLeaseExpiryAlertsAsync(CancellationToken ct = default)
    {
        var today = clock.Today;
        var horizon = today.AddDays(30);
        var rows = await leases.Query()
            .Where(l => l.Status == LeaseStatus.Active && l.EndDate >= today && l.EndDate <= horizon)
            .Select(l => new { l.Id, l.EndDate, Tenant = l.Tenant.OrganizationName, Unit = l.Unit.UnitNumber })
            .ToListAsync(ct);

        var alerts = rows.Select(r =>
        {
            var daysLeft = r.EndDate.DayNumber - today.DayNumber;
            var milestone = daysLeft <= 7 ? 7 : 30;
            return new Alert(
                $"lease:{r.Id}:{milestone}",
                $"Lease expiring in {milestone} days or less",
                $"The lease of {r.Tenant} for unit {r.Unit} ends on {Fmt(r.EndDate)} ({daysLeft} day(s) left).");
        }).ToList();
        return await PublishAsync(NotificationType.LeaseExpiry, alerts, ct);
    }

    public async Task<int> SendDocumentExpiryAlertsAsync(CancellationToken ct = default)
    {
        var today = clock.Today;
        var horizon = today.AddDays(30);
        var rows = await documents.Query()
            .Where(d => d.ExpiryDate != null && d.ExpiryDate >= today && d.ExpiryDate <= horizon && d.Tenant.IsActive)
            .Select(d => new { d.Id, d.DocumentType, Expiry = d.ExpiryDate, Tenant = d.Tenant.OrganizationName })
            .ToListAsync(ct);

        var alerts = rows.Select(r => new Alert(
            $"doc:{r.Id}:30",
            "Legal document expiring",
            $"{r.Tenant}'s {r.DocumentType} expires on {Fmt(r.Expiry!.Value)}.")).ToList();
        return await PublishAsync(NotificationType.DocumentExpiry, alerts, ct);
    }

    private async Task<int> PublishAsync(NotificationType type, List<Alert> alerts, CancellationToken ct)
    {
        if (alerts.Count == 0) return 0;
        var recipients = await GetRecipientIdsAsync();
        if (recipients.Count == 0) return 0;

        var keys = alerts.Select(a => a.Key).ToList();
        var existing = (await notifications.Query()
                .Where(n => n.ReferenceKey != null && keys.Contains(n.ReferenceKey))
                .Select(n => new { n.UserId, n.ReferenceKey })
                .ToListAsync(ct))
            .Select(x => (x.UserId, x.ReferenceKey!))
            .ToHashSet();

        var toAdd = new List<Notification>();
        foreach (var alert in alerts)
        foreach (var userId in recipients)
        {
            if (existing.Contains((userId, alert.Key))) continue;
            toAdd.Add(new Notification
            {
                UserId = userId,
                Title = alert.Title,
                Message = alert.Message,
                NotificationType = type,
                ReferenceKey = alert.Key
            });
        }

        if (toAdd.Count == 0) return 0;
        await notifications.AddRangeAsync(toAdd, ct);
        await uow.SaveChangesAsync(ct);
        return toAdd.Count;
    }

    private async Task<List<string>> GetRecipientIdsAsync()
    {
        var admins = await users.GetUsersInRoleAsync(Roles.Admin);
        var managers = await users.GetUsersInRoleAsync(Roles.Manager);
        return admins.Concat(managers).Where(u => u.IsActive).Select(u => u.Id).Distinct().ToList();
    }

    private string RequireUser() =>
        currentUser.UserId ?? throw new UnauthorizedException("You must be signed in.");

    private static string Fmt(DateOnly d) => d.ToString("dd MMM yyyy", CultureInfo.InvariantCulture);
}


using System.Globalization;
using BMS.Application.Domain;
using BMS.Application.DTOs;
using BMS.Application.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace BMS.Application.Services;

public class DashboardService(
    IRepository<Unit> units,
    IRepository<Invoice> invoices,
    IRepository<Payment> payments,
    IClock clock) : IDashboardService
{
    public async Task<DashboardStatsDto> GetStatsAsync(CancellationToken ct = default)
    {
        var allUnits = await units.Query().Select(u => new { u.FloorNumber, u.Status }).ToListAsync(ct);
        var total = allUnits.Count;
        var occupied = allUnits.Count(u => u.Status == UnitStatus.Occupied);
        var vacant = allUnits.Count(u => u.Status == UnitStatus.Available);

        var totalRevenue = await payments.Query().SumAsync(p => (decimal?)p.AmountPaid, ct) ?? 0m;

        var open = await invoices.Query()
            .Where(i => i.Status == InvoiceStatus.Issued || i.Status == InvoiceStatus.Overdue)
            .Select(i => new { i.AmountDue, Paid = i.Payments.Sum(p => p.AmountPaid) })
            .ToListAsync(ct);
        var outstanding = open.Sum(i => Math.Max(0m, i.AmountDue - i.Paid));

        var today = clock.Today;
        var months = Enumerable.Range(0, 6).Select(i => today.AddMonths(-(5 - i))).Select(d => (d.Year, d.Month)).ToList();
        var startKey = months[0].Year * 12 + months[0].Month;
        var endKey = months[^1].Year * 12 + months[^1].Month;

        var billed = await invoices.Query()
            .Where(i => i.Status != InvoiceStatus.Cancelled && i.Status != InvoiceStatus.Draft
                        && i.PeriodYear * 12 + i.PeriodMonth >= startKey && i.PeriodYear * 12 + i.PeriodMonth <= endKey)
            .Select(i => new { i.PeriodYear, i.PeriodMonth, i.AmountDue, Paid = i.Payments.Sum(p => p.AmountPaid) })
            .ToListAsync(ct);

        var revenueByMonth = months.Select(m =>
        {
            var rows = billed.Where(b => b.PeriodYear == m.Year && b.PeriodMonth == m.Month).ToList();
            return new MonthlyRevenueDto(
                CultureInfo.InvariantCulture.DateTimeFormat.GetAbbreviatedMonthName(m.Month),
                rows.Sum(r => r.AmountDue), rows.Sum(r => r.Paid));
        }).ToList();

        var occupancyByFloor = allUnits.GroupBy(u => u.FloorNumber).OrderBy(g => g.Key)
            .Select(g =>
            {
                var occ = g.Count(u => u.Status == UnitStatus.Occupied);
                return new FloorOccupancyDto(g.Key, occ, g.Count() - occ);
            }).ToList();

        return new DashboardStatsDto(
            total == 0 ? 0 : Math.Round(occupied * 100m / total, 1),
            totalRevenue, outstanding, vacant, total, revenueByMonth, occupancyByFloor);
    }
}


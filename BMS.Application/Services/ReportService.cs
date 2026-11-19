using System.Globalization;
using BMS.Application.Common;
using BMS.Application.Domain;
using BMS.Application.DTOs;
using BMS.Application.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace BMS.Application.Services;

public class ReportService(
    IRepository<Unit> units,
    IRepository<Floor> floors,
    IRepository<Invoice> invoices,
    IRepository<LegalDocument> documents,
    ILeaseService leaseService,
    IExcelExporter excel,
    IClock clock) : IReportService
{
    private const string XlsxContentType = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";

    public async Task<ReportResult> OccupancyReportAsync(CancellationToken ct = default)
    {
        var floorNumbers = await floors.Query().OrderBy(f => f.FloorNumber).Select(f => f.FloorNumber).ToListAsync(ct);
        var all = await units.Query().Select(u => new { u.FloorNumber, u.Status }).ToListAsync(ct);

        var rows = floorNumbers.Select(n =>
        {
            var onFloor = all.Where(u => u.FloorNumber == n).ToList();
            var occupied = onFloor.Count(u => u.Status == UnitStatus.Occupied);
            var percent = onFloor.Count == 0 ? 0 : (int)Math.Round(occupied * 100.0 / onFloor.Count);
            return new OccupancyRow($"Floor {n}", onFloor.Count, occupied, onFloor.Count - occupied, percent);
        }).ToList();

        var total = rows.Sum(r => r.TotalUnits);
        var occ = rows.Sum(r => r.Occupied);
        return new ReportResult(rows.Cast<object>().ToList(),
        [
            new("Total units", total),
            new("Occupied", occ),
            new("Vacant", total - occ),
            new("Occupancy", $"{(total == 0 ? 0 : (int)Math.Round(occ * 100.0 / total))}%")
        ]);
    }

    public async Task<ReportResult> RevenueReportAsync(int? year, CancellationToken ct = default)
    {
        var y = year ?? clock.Today.Year;
        var data = await invoices.Query()
            .Where(i => i.PeriodYear == y && i.Status != InvoiceStatus.Cancelled && i.Status != InvoiceStatus.Draft)
            .Select(i => new { i.PeriodMonth, i.AmountDue, Paid = i.Payments.Sum(p => p.AmountPaid) })
            .ToListAsync(ct);

        var rows = data.GroupBy(d => d.PeriodMonth).OrderBy(g => g.Key).Select(g =>
        {
            var expected = g.Sum(x => x.AmountDue);
            var collected = g.Sum(x => x.Paid);
            return new RevenueRow(MonthName(g.Key), expected, collected, Rate(collected, expected));
        }).ToList();

        var totalExpected = rows.Sum(r => r.Expected);
        var totalCollected = rows.Sum(r => r.Collected);
        return new ReportResult(rows.Cast<object>().ToList(),
        [
            new("Expected (YTD)", totalExpected),
            new("Collected (YTD)", totalCollected),
            new("Collection rate", $"{Rate(totalCollected, totalExpected)}%")
        ]);
    }

    public async Task<ReportResult> ArrearsReportAsync(CancellationToken ct = default)
    {
        var today = clock.Today;
        var late = await invoices.Query()
            .Where(i => (i.Status == InvoiceStatus.Issued || i.Status == InvoiceStatus.Overdue) && i.DueDate < today)
            .Select(i => new
            {
                i.LeaseId,
                Tenant = i.Lease.Tenant.OrganizationName,
                Unit = i.Lease.Unit.UnitNumber,
                i.AmountDue,
                Paid = i.Payments.Sum(p => p.AmountPaid)
            })
            .ToListAsync(ct);

        var rows = late.Where(x => x.AmountDue > x.Paid)
            .GroupBy(x => new { x.LeaseId, x.Tenant, x.Unit })
            .Select(g => new ArrearsRow(g.Key.Tenant, g.Key.Unit, g.Count(), g.Sum(x => x.AmountDue - x.Paid)))
            .OrderByDescending(r => r.TotalOwed)
            .ToList();

        return new ReportResult(rows.Cast<object>().ToList(),
        [
            new("Tenants in arrears", rows.Select(r => r.TenantName).Distinct().Count()),
            new("Total owed", rows.Sum(r => r.TotalOwed))
        ]);
    }

    public async Task<ReportResult> LeaseExpiryReportAsync(int days, CancellationToken ct = default)
    {
        var rows = await leaseService.GetExpiringLeasesAsync(days, ct);
        return new ReportResult(rows.Cast<object>().ToList(), [new("Leases expiring", rows.Count)]);
    }

    public async Task<ReportResult> DocumentExpiryReportAsync(int days, CancellationToken ct = default)
    {
        var today = clock.Today;
        var horizon = today.AddDays(days);
        var data = await documents.Query()
            .Where(d => d.ExpiryDate != null && d.ExpiryDate >= today && d.ExpiryDate <= horizon && d.Tenant.IsActive)
            .OrderBy(d => d.ExpiryDate)
            .Select(d => new { Tenant = d.Tenant.OrganizationName, d.DocumentType, Expiry = d.ExpiryDate })
            .ToListAsync(ct);

        var rows = data.Select(d => new DocumentExpiryRow(d.Tenant, d.DocumentType, d.Expiry!.Value, d.Expiry.Value.DayNumber - today.DayNumber)).ToList();
        return new ReportResult(rows.Cast<object>().ToList(), [new("Documents expiring", rows.Count)]);
    }

    public async Task<ExportFile> ExportExcelAsync(string type, int? year, int? days, CancellationToken ct = default)
    {
        var (title, result) = type.ToLowerInvariant() switch
        {
            "occupancy" => ("Occupancy", await OccupancyReportAsync(ct)),
            "revenue" => ("Revenue", await RevenueReportAsync(year, ct)),
            "arrears" => ("Arrears", await ArrearsReportAsync(ct)),
            "lease-expiry" => ("Lease Expiry", await LeaseExpiryReportAsync(days ?? 30, ct)),
            "document-expiry" => ("Document Expiry", await DocumentExpiryReportAsync(days ?? 30, ct)),
            _ => throw new NotFoundException($"Unknown report type '{type}'.")
        };

        var bytes = excel.Export(title, result.Rows, result.Summary);
        var fileName = $"{type.ToLowerInvariant()}-report-{clock.Today:yyyyMMdd}.xlsx";
        return new ExportFile(bytes, fileName, XlsxContentType);
    }

    private static int Rate(decimal collected, decimal expected) =>
        expected <= 0 ? 0 : (int)Math.Round(collected * 100m / expected);

    private static string MonthName(int month) =>
        CultureInfo.InvariantCulture.DateTimeFormat.GetAbbreviatedMonthName(month);
}


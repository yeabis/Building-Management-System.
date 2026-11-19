using BMS.API.Models;
using BMS.Application.Common;
using BMS.Application.DTOs;
using BMS.Application.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace BMS.API.Controllers;

[Route("api/reports")]
public class ReportsController(IReportService reports) : ApiControllerBase
{
    [HttpGet("occupancy")]
    public async Task<ActionResult<ApiResponse<ReportResult>>> Occupancy(CancellationToken ct) =>
        Success(await reports.OccupancyReportAsync(ct));

    [HttpGet("revenue")]
    public async Task<ActionResult<ApiResponse<ReportResult>>> Revenue([FromQuery] int? year, CancellationToken ct) =>
        Success(await reports.RevenueReportAsync(year, ct));

    [HttpGet("arrears")]
    public async Task<ActionResult<ApiResponse<ReportResult>>> Arrears(CancellationToken ct) =>
        Success(await reports.ArrearsReportAsync(ct));

    [HttpGet("lease-expiry")]
    public async Task<ActionResult<ApiResponse<ReportResult>>> LeaseExpiry([FromQuery] int days = 30, CancellationToken ct = default) =>
        Success(await reports.LeaseExpiryReportAsync(Math.Clamp(days, 1, 365), ct));

    [HttpGet("document-expiry")]
    public async Task<ActionResult<ApiResponse<ReportResult>>> DocumentExpiry([FromQuery] int days = 30, CancellationToken ct = default) =>
        Success(await reports.DocumentExpiryReportAsync(Math.Clamp(days, 1, 365), ct));

    [HttpGet("export/{type}")]
    public async Task<IActionResult> Export(string type, [FromQuery] int? year, [FromQuery] int? days, CancellationToken ct)
    {
        var file = await reports.ExportExcelAsync(type, year, days is null ? null : Math.Clamp(days.Value, 1, 365), ct);
        return File(file.Content, file.ContentType, file.FileName);
    }
}

[Route("api/dashboard")]
public class DashboardController(IDashboardService dashboard) : ApiControllerBase
{

    [HttpGet]
    public async Task<ActionResult<ApiResponse<DashboardStatsDto>>> Get(CancellationToken ct) =>
        Success(await dashboard.GetStatsAsync(ct));
}

[Route("api/notifications")]
public class NotificationsController(INotificationService notifications) : ApiControllerBase
{

    [HttpGet]
    public async Task<ActionResult<ApiResponse<PagedResult<NotificationDto>>>> GetMine([FromQuery] PageQuery query, CancellationToken ct) =>
        Success(await notifications.GetMyNotificationsAsync(query, ct));

    [HttpGet("unread-count")]
    public async Task<ActionResult<ApiResponse<int>>> UnreadCount(CancellationToken ct) =>
        Success(await notifications.GetUnreadCountAsync(ct));

    [HttpPut("{id:int}/read")]
    public async Task<ActionResult<ApiResponse<object?>>> MarkRead(int id, CancellationToken ct)
    {
        await notifications.MarkAsReadAsync(id, ct);
        return Success();
    }

    [HttpPut("read-all")]
    public async Task<ActionResult<ApiResponse<object?>>> MarkAllRead(CancellationToken ct)
    {
        await notifications.MarkAllAsReadAsync(ct);
        return Success();
    }
}


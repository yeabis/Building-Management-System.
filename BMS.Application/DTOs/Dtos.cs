using BMS.Application.Common;
using BMS.Application.Domain;

namespace BMS.Application.DTOs;

public record LoginRequest(string Email, string Password);
public record RegisterRequest(string FullName, string Email, string Password);
public record RefreshTokenRequest(string RefreshToken);
public record AssignRoleRequest(string Email, string Role);
public record AuthUserDto(string Id, string FullName, string Email, string Role);
public record LoginResponse(string Token, string RefreshToken, DateTime ExpiresAt, AuthUserDto User);

public record UnitDto(int Id, int FloorNumber, string UnitNumber, UnitType UnitType, decimal AreaSqMeters,
    decimal MonthlyRent, UnitStatus Status, string? Description, string? TenantName);
public record UnitInputDto(int FloorNumber, string UnitNumber, UnitType UnitType, decimal AreaSqMeters,
    decimal MonthlyRent, UnitStatus Status, string? Description);
public record ChangeUnitStatusRequest(UnitStatus Status);
public record FloorSummaryDto(int FloorNumber, int TotalUnits, int Occupied, int Available, int UnderMaintenance,
    int Reserved, IReadOnlyList<UnitDto> Units);

public class UnitQuery : PageQuery
{
    public string? Search { get; set; }
    public int? Floor { get; set; }
    public UnitType? UnitType { get; set; }
    public UnitStatus? Status { get; set; }
}

public record LegalDocumentDto(int Id, int TenantId, DocumentType DocumentType, string FileName, DateTime UploadedAt,
    DateOnly? ExpiryDate, bool IsVerified);
public record TenantDto(int Id, string OrganizationName, string TinNumber, string Phone, BusinessType BusinessType,
    string ContactPersonName, string ContactEmail, bool IsActive, IReadOnlyList<string> ActiveUnits,
    IReadOnlyList<LegalDocumentDto>? Documents);
public record TenantInputDto(string OrganizationName, string TinNumber, string Phone, BusinessType BusinessType,
    string ContactPersonName, string ContactEmail);
public record DocumentUploadRequest(DocumentType DocumentType, DateOnly? ExpiryDate);
public record FileDownload(Stream Content, string FileName, string ContentType);

public class TenantQuery : PageQuery
{
    public string? Search { get; set; }
    public BusinessType? BusinessType { get; set; }
    public bool? IsActive { get; set; }
}

public record LeaseDto(int Id, int UnitId, string UnitNumber, int TenantId, string TenantName, DateOnly StartDate,
    DateOnly EndDate, decimal MonthlyRent, decimal DepositAmount, LeaseStatus Status, string? TerminationReason);
public record LeaseInputDto(int UnitId, int TenantId, DateOnly StartDate, DateOnly EndDate, decimal MonthlyRent, decimal DepositAmount);
public record RenewLeaseRequest(DateOnly NewEndDate, decimal NewMonthlyRent);
public record TerminateLeaseRequest(string Reason);
public record LeaseExpiryRow(string TenantName, string UnitNumber, DateOnly EndDate, decimal MonthlyRent, int DaysRemaining);

public class LeaseQuery : PageQuery
{
    public LeaseStatus? Status { get; set; }
    public int? TenantId { get; set; }
    public int? UnitId { get; set; }
}

public record InvoiceDto(int Id, int LeaseId, string InvoiceNumber, string TenantName, string UnitNumber,
    decimal AmountDue, decimal AmountPaid, decimal Balance, DateOnly DueDate, DateOnly IssueDate,
    InvoiceStatus Status, int PeriodMonth, int PeriodYear);
public record GenerateInvoicesRequest(int? Month, int? Year);
public record GenerateInvoicesResult(int Generated);
public record MarkPaidRequest(PaymentMethod? Method, string? ReferenceNumber);

public class InvoiceQuery : PageQuery
{
    public InvoiceStatus? Status { get; set; }
    public int? LeaseId { get; set; }
    public int? TenantId { get; set; }
}

public record PaymentDto(int Id, int InvoiceId, string InvoiceNumber, string TenantName, decimal AmountPaid,
    DateOnly PaymentDate, PaymentMethod PaymentMethod, string ReferenceNumber, string? Notes);
public record PaymentInputDto(int InvoiceId, decimal AmountPaid, DateOnly PaymentDate, PaymentMethod PaymentMethod,
    string ReferenceNumber, string? Notes);

public class PaymentQuery : PageQuery
{
    public int? InvoiceId { get; set; }
    public int? TenantId { get; set; }
}

public record NotificationDto(int Id, string Title, string Message, NotificationType NotificationType, bool IsRead, DateTime CreatedAt);

public record MonthlyRevenueDto(string Month, decimal Expected, decimal Collected);
public record FloorOccupancyDto(int Floor, int Occupied, int Vacant);
public record DashboardStatsDto(decimal OccupancyPercent, decimal TotalRevenue, decimal Outstanding, int VacantUnits,
    int TotalUnits, IReadOnlyList<MonthlyRevenueDto> RevenueByMonth, IReadOnlyList<FloorOccupancyDto> OccupancyByFloor);

public record SummaryItem(string Label, object Value);
public record ReportResult(IReadOnlyList<object> Rows, IReadOnlyList<SummaryItem> Summary);
public record OccupancyRow(string Floor, int TotalUnits, int Occupied, int Vacant, int OccupancyPercent);
public record RevenueRow(string Month, decimal Expected, decimal Collected, int CollectionRate);
public record ArrearsRow(string TenantName, string UnitNumber, int MonthsOverdue, decimal TotalOwed);
public record DocumentExpiryRow(string TenantName, DocumentType DocumentType, DateOnly ExpiryDate, int DaysRemaining);
public record ExportFile(byte[] Content, string FileName, string ContentType);


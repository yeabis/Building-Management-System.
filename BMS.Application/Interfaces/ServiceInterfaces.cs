using BMS.Application.Common;
using BMS.Application.Domain;
using BMS.Application.DTOs;

namespace BMS.Application.Interfaces;

public interface IUnitService
{
    Task<PagedResult<UnitDto>> GetUnitsAsync(UnitQuery query, CancellationToken ct = default);
    Task<UnitDto> GetByIdAsync(int id, CancellationToken ct = default);
    Task<UnitDto> CreateUnitAsync(UnitInputDto input, CancellationToken ct = default);
    Task<UnitDto> UpdateUnitAsync(int id, UnitInputDto input, CancellationToken ct = default);
    Task<UnitDto> ChangeStatusAsync(int id, UnitStatus status, CancellationToken ct = default);
    Task DeleteUnitAsync(int id, CancellationToken ct = default);
    Task<IReadOnlyList<FloorSummaryDto>> GetFloorSummaryAsync(CancellationToken ct = default);
}

public interface ITenantService
{
    Task<PagedResult<TenantDto>> GetTenantsAsync(TenantQuery query, CancellationToken ct = default);
    Task<TenantDto> GetByIdAsync(int id, CancellationToken ct = default);
    Task<TenantDto> RegisterTenantAsync(TenantInputDto input, CancellationToken ct = default);
    Task<TenantDto> UpdateTenantAsync(int id, TenantInputDto input, CancellationToken ct = default);
    Task DeactivateTenantAsync(int id, CancellationToken ct = default);
    Task<LegalDocumentDto> UploadDocumentAsync(int tenantId, DocumentUploadRequest request, Stream content,
        string fileName, long length, CancellationToken ct = default);
    Task<LegalDocumentDto> VerifyDocumentAsync(int tenantId, int documentId, CancellationToken ct = default);
    Task<FileDownload> GetDocumentFileAsync(int tenantId, int documentId, CancellationToken ct = default);
}

public interface ILeaseService
{
    Task<PagedResult<LeaseDto>> GetLeasesAsync(LeaseQuery query, CancellationToken ct = default);
    Task<LeaseDto> GetByIdAsync(int id, CancellationToken ct = default);
    Task<LeaseDto> CreateLeaseAsync(LeaseInputDto input, CancellationToken ct = default);
    Task<LeaseDto> RenewLeaseAsync(int id, RenewLeaseRequest request, CancellationToken ct = default);
    Task<LeaseDto> TerminateLeaseAsync(int id, TerminateLeaseRequest request, CancellationToken ct = default);
    Task<IReadOnlyList<LeaseExpiryRow>> GetExpiringLeasesAsync(int days = 30, CancellationToken ct = default);

    Task<int> ExpireEndedLeasesAsync(CancellationToken ct = default);
}

public interface IPaymentService
{
    Task<PaymentDto> RecordPaymentAsync(PaymentInputDto input, CancellationToken ct = default);
    Task<PagedResult<InvoiceDto>> GetOutstandingBalancesAsync(PageQuery query, CancellationToken ct = default);
    Task<PagedResult<PaymentDto>> GetPaymentHistoryAsync(PaymentQuery query, CancellationToken ct = default);
}

public interface IInvoiceService
{
    Task<GenerateInvoicesResult> GenerateInvoicesAsync(GenerateInvoicesRequest? request, CancellationToken ct = default);
    Task<PagedResult<InvoiceDto>> GetInvoicesAsync(InvoiceQuery query, CancellationToken ct = default);
    Task<PagedResult<InvoiceDto>> GetUnpaidInvoicesAsync(PageQuery query, CancellationToken ct = default);
    Task<InvoiceDto> MarkAsPaidAsync(int id, MarkPaidRequest? request, CancellationToken ct = default);

    Task<int> MarkOverdueAsync(CancellationToken ct = default);
}

public interface IReportService
{
    Task<ReportResult> OccupancyReportAsync(CancellationToken ct = default);
    Task<ReportResult> RevenueReportAsync(int? year, CancellationToken ct = default);
    Task<ReportResult> ArrearsReportAsync(CancellationToken ct = default);
    Task<ReportResult> LeaseExpiryReportAsync(int days, CancellationToken ct = default);
    Task<ReportResult> DocumentExpiryReportAsync(int days, CancellationToken ct = default);
    Task<ExportFile> ExportExcelAsync(string type, int? year, int? days, CancellationToken ct = default);
}

public interface INotificationService
{
    Task<PagedResult<NotificationDto>> GetMyNotificationsAsync(PageQuery query, CancellationToken ct = default);
    Task<int> GetUnreadCountAsync(CancellationToken ct = default);
    Task MarkAsReadAsync(int id, CancellationToken ct = default);
    Task MarkAllAsReadAsync(CancellationToken ct = default);
    Task<int> SendPaymentDueAlertsAsync(CancellationToken ct = default);
    Task<int> SendOverdueAlertsAsync(CancellationToken ct = default);
    Task<int> SendLeaseExpiryAlertsAsync(CancellationToken ct = default);
    Task<int> SendDocumentExpiryAlertsAsync(CancellationToken ct = default);
}

public interface IAuthService
{
    Task<AuthUserDto> RegisterAsync(RegisterRequest request, CancellationToken ct = default);
    Task<LoginResponse> LoginAsync(LoginRequest request, CancellationToken ct = default);
    Task<LoginResponse> RefreshTokenAsync(RefreshTokenRequest request, CancellationToken ct = default);
    Task<AuthUserDto> AssignRoleAsync(AssignRoleRequest request, CancellationToken ct = default);
}

public interface IDashboardService
{
    Task<DashboardStatsDto> GetStatsAsync(CancellationToken ct = default);
}


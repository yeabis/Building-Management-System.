using BMS.Application.Common;
using BMS.Application.Domain;
using BMS.Application.DTOs;
using BMS.Application.Interfaces;
using BMS.Application.Mapping;
using Microsoft.EntityFrameworkCore;

namespace BMS.Application.Services;

public class TenantService(
    IRepository<Tenant> tenants,
    IRepository<LegalDocument> documents,
    IRepository<Lease> leases,
    IFileStorage storage,
    IClock clock,
    IUnitOfWork uow) : ITenantService
{
    private static readonly string[] AllowedExtensions = [".pdf", ".png", ".jpg", ".jpeg"];
    private const long MaxFileBytes = 5 * 1024 * 1024;

    public async Task<PagedResult<TenantDto>> GetTenantsAsync(TenantQuery q, CancellationToken ct = default)
    {
        var query = tenants.Query().WithActiveUnits();

        if (!string.IsNullOrWhiteSpace(q.Search))
        {
            var s = q.Search.Trim();

            var matchingTypes = Enum.GetValues<BusinessType>()
                .Where(b => b.ToString().Contains(s, StringComparison.OrdinalIgnoreCase)).ToList();
            query = query.Where(t => t.OrganizationName.Contains(s) || t.TinNumber.Contains(s) || matchingTypes.Contains(t.BusinessType));
        }
        if (q.BusinessType.HasValue)
        {
            var type = q.BusinessType.Value;
            query = query.Where(t => t.BusinessType == type);
        }
        if (q.IsActive.HasValue)
        {
            var active = q.IsActive.Value;
            query = query.Where(t => t.IsActive == active);
        }

        var page = await query.OrderBy(t => t.OrganizationName).ToPagedAsync(q.Page, q.PageSize, ct);
        return page.Map(t => t.ToDto());
    }

    public async Task<TenantDto> GetByIdAsync(int id, CancellationToken ct = default)
    {
        var tenant = await tenants.Query().WithActiveUnits().Include(t => t.Documents)
                         .FirstOrDefaultAsync(t => t.Id == id, ct)
                     ?? throw new NotFoundException("Tenant not found.");
        return tenant.ToDto(includeDocuments: true);
    }

    public async Task<TenantDto> RegisterTenantAsync(TenantInputDto input, CancellationToken ct = default)
    {
        var tin = input.TinNumber.Trim();
        if (await tenants.AnyAsync(t => t.TinNumber == tin, ct))
            throw new ConflictException("A tenant with this TIN number already exists.");

        var tenant = new Tenant();
        Apply(tenant, input);
        await tenants.AddAsync(tenant, ct);
        await uow.SaveChangesAsync(ct);
        return tenant.ToDto();
    }

    public async Task<TenantDto> UpdateTenantAsync(int id, TenantInputDto input, CancellationToken ct = default)
    {
        var tenant = await tenants.GetByIdAsync(id, ct) ?? throw new NotFoundException("Tenant not found.");
        var tin = input.TinNumber.Trim();
        if (await tenants.AnyAsync(t => t.TinNumber == tin && t.Id != id, ct))
            throw new ConflictException("A tenant with this TIN number already exists.");

        Apply(tenant, input);
        await uow.SaveChangesAsync(ct);
        return await GetByIdAsync(id, ct);
    }

    public async Task DeactivateTenantAsync(int id, CancellationToken ct = default)
    {
        var tenant = await tenants.GetByIdAsync(id, ct) ?? throw new NotFoundException("Tenant not found.");
        if (await leases.AnyAsync(l => l.TenantId == id && l.Status == LeaseStatus.Active, ct))
            throw new ConflictException("Terminate this tenant's active leases before deactivating.");

        tenant.IsActive = false;
        await uow.SaveChangesAsync(ct);
    }

    public async Task<LegalDocumentDto> UploadDocumentAsync(int tenantId, DocumentUploadRequest request, Stream content,
        string fileName, long length, CancellationToken ct = default)
    {
        if (!await tenants.AnyAsync(t => t.Id == tenantId, ct)) throw new NotFoundException("Tenant not found.");

        var safeName = Path.GetFileName(fileName);
        var ext = Path.GetExtension(safeName).ToLowerInvariant();
        if (!AllowedExtensions.Contains(ext)) throw new BusinessRuleException("Only PDF, PNG and JPG files are allowed.");
        if (length <= 0) throw new BusinessRuleException("The uploaded file is empty.");
        if (length > MaxFileBytes) throw new BusinessRuleException("The file is larger than 5 MB.");
        if (request.ExpiryDate is { } expiry && expiry < clock.Today)
            throw new BusinessRuleException("The expiry date cannot be in the past.");

        var path = await storage.SaveAsync(content, ext, $"tenants/{tenantId}", ct);
        var doc = new LegalDocument
        {
            TenantId = tenantId,
            DocumentType = request.DocumentType,
            FileName = safeName.Length > 200 ? safeName[^200..] : safeName,
            FilePath = path,
            UploadedAt = clock.UtcNow,
            ExpiryDate = request.ExpiryDate,
            IsVerified = false
        };
        await documents.AddAsync(doc, ct);
        await uow.SaveChangesAsync(ct);
        return doc.ToDto();
    }

    public async Task<LegalDocumentDto> VerifyDocumentAsync(int tenantId, int documentId, CancellationToken ct = default)
    {
        var doc = await documents.QueryTracked().FirstOrDefaultAsync(d => d.Id == documentId && d.TenantId == tenantId, ct)
                  ?? throw new NotFoundException("Document not found.");
        doc.IsVerified = true;
        await uow.SaveChangesAsync(ct);
        return doc.ToDto();
    }

    public async Task<FileDownload> GetDocumentFileAsync(int tenantId, int documentId, CancellationToken ct = default)
    {
        var doc = await documents.Query().FirstOrDefaultAsync(d => d.Id == documentId && d.TenantId == tenantId, ct)
                  ?? throw new NotFoundException("Document not found.");
        if (!storage.Exists(doc.FilePath)) throw new NotFoundException("The file is no longer available on the server.");

        var contentType = Path.GetExtension(doc.FilePath).ToLowerInvariant() switch
        {
            ".pdf" => "application/pdf",
            ".png" => "image/png",
            ".jpg" or ".jpeg" => "image/jpeg",
            _ => "application/octet-stream"
        };
        return new FileDownload(storage.OpenRead(doc.FilePath), doc.FileName, contentType);
    }

    private static void Apply(Tenant t, TenantInputDto input)
    {
        t.OrganizationName = input.OrganizationName.Trim();
        t.TinNumber = input.TinNumber.Trim();
        t.Phone = input.Phone.Trim();
        t.BusinessType = input.BusinessType;
        t.ContactPersonName = input.ContactPersonName.Trim();
        t.ContactEmail = input.ContactEmail.Trim();
    }
}


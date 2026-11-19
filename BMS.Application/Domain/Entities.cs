using Microsoft.AspNetCore.Identity;

namespace BMS.Application.Domain;

public interface IAuditable
{
    DateTime CreatedAt { get; set; }
    DateTime? UpdatedAt { get; set; }
    string? CreatedBy { get; set; }
    string? UpdatedBy { get; set; }
}

public interface ISoftDelete
{
    bool IsDeleted { get; set; }
}

public abstract class BaseEntity : IAuditable
{
    public int Id { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public string? CreatedBy { get; set; }
    public string? UpdatedBy { get; set; }
}

public class AppUser : IdentityUser, IAuditable
{
    public string FullName { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;

    public string? RefreshTokenHash { get; set; }
    public DateTime? RefreshTokenExpiresAt { get; set; }

    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public string? CreatedBy { get; set; }
    public string? UpdatedBy { get; set; }

    public ICollection<Notification> Notifications { get; set; } = new List<Notification>();
}

public class Building : BaseEntity
{
    public string Name { get; set; } = string.Empty;
    public string? Address { get; set; }
    public ICollection<Floor> Floors { get; set; } = new List<Floor>();
}

public class Floor : BaseEntity
{
    public int BuildingId { get; set; }
    public Building Building { get; set; } = null!;
    public int FloorNumber { get; set; }
    public string Name { get; set; } = string.Empty;
    public ICollection<Unit> Units { get; set; } = new List<Unit>();
}

public class Unit : BaseEntity, ISoftDelete
{
    public int FloorId { get; set; }
    public Floor Floor { get; set; } = null!;
    public int FloorNumber { get; set; }
    public string UnitNumber { get; set; } = string.Empty;
    public UnitType UnitType { get; set; }
    public decimal AreaSqMeters { get; set; }
    public decimal MonthlyRent { get; set; }
    public UnitStatus Status { get; set; } = UnitStatus.Available;
    public string? Description { get; set; }
    public bool IsDeleted { get; set; }
    public ICollection<Lease> Leases { get; set; } = new List<Lease>();
}

public class Tenant : BaseEntity, ISoftDelete
{
    public string OrganizationName { get; set; } = string.Empty;
    public string TinNumber { get; set; } = string.Empty;
    public string Phone { get; set; } = string.Empty;
    public BusinessType BusinessType { get; set; }
    public string ContactPersonName { get; set; } = string.Empty;
    public string ContactEmail { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;
    public bool IsDeleted { get; set; }
    public ICollection<LegalDocument> Documents { get; set; } = new List<LegalDocument>();
    public ICollection<Lease> Leases { get; set; } = new List<Lease>();
}

public class LegalDocument : BaseEntity
{
    public int TenantId { get; set; }
    public Tenant Tenant { get; set; } = null!;
    public DocumentType DocumentType { get; set; }

    public string FileName { get; set; } = string.Empty;

    public string FilePath { get; set; } = string.Empty;
    public DateTime UploadedAt { get; set; }
    public DateOnly? ExpiryDate { get; set; }
    public bool IsVerified { get; set; }
}

public class Lease : BaseEntity, ISoftDelete
{
    public int UnitId { get; set; }
    public Unit Unit { get; set; } = null!;
    public int TenantId { get; set; }
    public Tenant Tenant { get; set; } = null!;
    public DateOnly StartDate { get; set; }
    public DateOnly EndDate { get; set; }
    public decimal MonthlyRent { get; set; }
    public decimal DepositAmount { get; set; }
    public LeaseStatus Status { get; set; } = LeaseStatus.Active;
    public string? TerminationReason { get; set; }
    public bool IsDeleted { get; set; }
    public ICollection<Invoice> Invoices { get; set; } = new List<Invoice>();
}

public class Invoice : BaseEntity
{
    public int LeaseId { get; set; }
    public Lease Lease { get; set; } = null!;
    public string InvoiceNumber { get; set; } = string.Empty;
    public decimal AmountDue { get; set; }
    public DateOnly DueDate { get; set; }
    public DateOnly IssueDate { get; set; }
    public InvoiceStatus Status { get; set; } = InvoiceStatus.Issued;
    public int PeriodMonth { get; set; }
    public int PeriodYear { get; set; }

    public byte[] RowVersion { get; set; } = Array.Empty<byte>();
    public ICollection<Payment> Payments { get; set; } = new List<Payment>();
}

public class Payment : BaseEntity
{
    public int InvoiceId { get; set; }
    public Invoice Invoice { get; set; } = null!;
    public decimal AmountPaid { get; set; }
    public DateOnly PaymentDate { get; set; }
    public PaymentMethod PaymentMethod { get; set; }
    public string ReferenceNumber { get; set; } = string.Empty;
    public string? Notes { get; set; }
}

public class Notification : BaseEntity
{
    public string UserId { get; set; } = string.Empty;
    public AppUser User { get; set; } = null!;
    public string Title { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public NotificationType NotificationType { get; set; }
    public bool IsRead { get; set; }

    public string? ReferenceKey { get; set; }
}


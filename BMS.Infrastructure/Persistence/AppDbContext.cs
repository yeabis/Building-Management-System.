using BMS.Application.Domain;
using BMS.Application.Interfaces;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace BMS.Infrastructure.Persistence;

public class AppDbContext : IdentityDbContext<AppUser>, IUnitOfWork
{
    private readonly ICurrentUser? _currentUser;
    private readonly IClock? _clock;

    public AppDbContext(DbContextOptions<AppDbContext> options, ICurrentUser? currentUser = null, IClock? clock = null)
        : base(options)
    {
        _currentUser = currentUser;
        _clock = clock;
    }

    public DbSet<Building> Buildings => Set<Building>();
    public DbSet<Floor> Floors => Set<Floor>();
    public DbSet<Unit> Units => Set<Unit>();
    public DbSet<Tenant> Tenants => Set<Tenant>();
    public DbSet<LegalDocument> LegalDocuments => Set<LegalDocument>();
    public DbSet<Lease> Leases => Set<Lease>();
    public DbSet<Invoice> Invoices => Set<Invoice>();
    public DbSet<Payment> Payments => Set<Payment>();
    public DbSet<Notification> Notifications => Set<Notification>();

    protected override void ConfigureConventions(ModelConfigurationBuilder cb)
    {
        cb.Properties<decimal>().HavePrecision(18, 2);

        cb.Properties<UnitType>().HaveConversion<string>().HaveMaxLength(20);
        cb.Properties<UnitStatus>().HaveConversion<string>().HaveMaxLength(30);
        cb.Properties<LeaseStatus>().HaveConversion<string>().HaveMaxLength(30);
        cb.Properties<InvoiceStatus>().HaveConversion<string>().HaveMaxLength(30);
        cb.Properties<PaymentMethod>().HaveConversion<string>().HaveMaxLength(30);
        cb.Properties<BusinessType>().HaveConversion<string>().HaveMaxLength(30);
        cb.Properties<DocumentType>().HaveConversion<string>().HaveMaxLength(30);
        cb.Properties<NotificationType>().HaveConversion<string>().HaveMaxLength(30);
    }

    protected override void OnModelCreating(ModelBuilder b)
    {
        base.OnModelCreating(b);

        b.Entity<AppUser>(e =>
        {
            e.Property(u => u.FullName).HasMaxLength(100).IsRequired();
            e.Property(u => u.RefreshTokenHash).HasMaxLength(128);
            e.Property(u => u.CreatedBy).HasMaxLength(256);
            e.Property(u => u.UpdatedBy).HasMaxLength(256);
            e.HasIndex(u => u.RefreshTokenHash);
        });

        b.Entity<Building>(e =>
        {
            e.Property(x => x.Name).HasMaxLength(150).IsRequired();
            e.Property(x => x.Address).HasMaxLength(300);
        });

        b.Entity<Floor>(e =>
        {
            e.Property(x => x.Name).HasMaxLength(50).IsRequired();
            e.HasOne(x => x.Building).WithMany(x => x.Floors).HasForeignKey(x => x.BuildingId)
                .OnDelete(DeleteBehavior.Restrict);
            e.HasIndex(x => new { x.BuildingId, x.FloorNumber }).IsUnique();
        });

        b.Entity<Unit>(e =>
        {
            e.HasQueryFilter(x => !x.IsDeleted);
            e.Property(x => x.UnitNumber).HasMaxLength(10).IsRequired();
            e.Property(x => x.Description).HasMaxLength(500);
            e.HasOne(x => x.Floor).WithMany(x => x.Units).HasForeignKey(x => x.FloorId)
                .OnDelete(DeleteBehavior.Restrict);
            e.HasIndex(x => x.UnitNumber).IsUnique().HasFilter("[IsDeleted] = 0");
            e.HasIndex(x => new { x.FloorNumber, x.Status });
        });

        b.Entity<Tenant>(e =>
        {
            e.HasQueryFilter(x => !x.IsDeleted);
            e.Property(x => x.OrganizationName).HasMaxLength(150).IsRequired();
            e.Property(x => x.TinNumber).HasMaxLength(20).IsRequired();
            e.Property(x => x.Phone).HasMaxLength(20).IsRequired();
            e.Property(x => x.ContactPersonName).HasMaxLength(100).IsRequired();
            e.Property(x => x.ContactEmail).HasMaxLength(256).IsRequired();
            e.HasIndex(x => x.TinNumber).IsUnique().HasFilter("[IsDeleted] = 0");
        });

        b.Entity<LegalDocument>(e =>
        {
            e.Property(x => x.FileName).HasMaxLength(200).IsRequired();
            e.Property(x => x.FilePath).HasMaxLength(400).IsRequired();
            e.HasOne(x => x.Tenant).WithMany(x => x.Documents).HasForeignKey(x => x.TenantId)
                .OnDelete(DeleteBehavior.Restrict);
            e.HasIndex(x => x.ExpiryDate);
        });

        b.Entity<Lease>(e =>
        {
            e.HasQueryFilter(x => !x.IsDeleted);
            e.Property(x => x.TerminationReason).HasMaxLength(500);
            e.HasOne(x => x.Unit).WithMany(x => x.Leases).HasForeignKey(x => x.UnitId)
                .OnDelete(DeleteBehavior.Restrict);
            e.HasOne(x => x.Tenant).WithMany(x => x.Leases).HasForeignKey(x => x.TenantId)
                .OnDelete(DeleteBehavior.Restrict);
            e.HasIndex(x => x.Status);
            e.HasIndex(x => x.EndDate);

            e.HasIndex(x => x.UnitId).IsUnique().HasFilter("[Status] = 'Active' AND [IsDeleted] = 0")
                .HasDatabaseName("UX_Leases_OneActivePerUnit");
        });

        b.Entity<Invoice>(e =>
        {
            e.Property(x => x.InvoiceNumber).HasMaxLength(30).IsRequired();
            e.Property(x => x.RowVersion).IsRowVersion();
            e.HasOne(x => x.Lease).WithMany(x => x.Invoices).HasForeignKey(x => x.LeaseId)
                .OnDelete(DeleteBehavior.Restrict);
            e.HasIndex(x => x.Status);
            e.HasIndex(x => x.DueDate);
            e.HasIndex(x => x.InvoiceNumber).IsUnique();

            e.HasIndex(x => new { x.LeaseId, x.PeriodYear, x.PeriodMonth }).IsUnique();
        });

        b.Entity<Payment>(e =>
        {
            e.Property(x => x.ReferenceNumber).HasMaxLength(100).IsRequired();
            e.Property(x => x.Notes).HasMaxLength(500);
            e.HasOne(x => x.Invoice).WithMany(x => x.Payments).HasForeignKey(x => x.InvoiceId)
                .OnDelete(DeleteBehavior.Restrict);
            e.HasIndex(x => x.PaymentDate);
        });

        b.Entity<Notification>(e =>
        {
            e.Property(x => x.Title).HasMaxLength(150).IsRequired();
            e.Property(x => x.Message).HasMaxLength(1000).IsRequired();
            e.Property(x => x.ReferenceKey).HasMaxLength(100);
            e.HasOne(x => x.User).WithMany(x => x.Notifications).HasForeignKey(x => x.UserId)
                .OnDelete(DeleteBehavior.Cascade);
            e.HasIndex(x => new { x.UserId, x.IsRead });
            e.HasIndex(x => x.ReferenceKey);
        });
    }

    public override Task<int> SaveChangesAsync(bool acceptAllChangesOnSuccess, CancellationToken cancellationToken = default)
    {
        ApplyAuditAndSoftDelete();
        return base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken);
    }

    public override int SaveChanges(bool acceptAllChangesOnSuccess)
    {
        ApplyAuditAndSoftDelete();
        return base.SaveChanges(acceptAllChangesOnSuccess);
    }

    private void ApplyAuditAndSoftDelete()
    {
        var now = _clock?.UtcNow ?? DateTime.UtcNow;
        var user = _currentUser?.UserName ?? "system";

        foreach (var entry in ChangeTracker.Entries().ToList())
        {

            if (entry.State == EntityState.Deleted && entry.Entity is ISoftDelete soft)
            {
                entry.State = EntityState.Modified;
                soft.IsDeleted = true;
            }

            if (entry.Entity is not IAuditable audit) continue;

            if (entry.State == EntityState.Added)
            {
                audit.CreatedAt = now;
                audit.CreatedBy = user;
            }
            else if (entry.State == EntityState.Modified)
            {
                audit.UpdatedAt = now;
                audit.UpdatedBy = user;
                entry.Property(nameof(IAuditable.CreatedAt)).IsModified = false;
                entry.Property(nameof(IAuditable.CreatedBy)).IsModified = false;
            }
        }
    }
}


using BMS.Application.Common;
using BMS.Application.Domain;
using BMS.Application.Interfaces;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace BMS.Infrastructure.Persistence;

public class DbInitializer(
    AppDbContext db,
    UserManager<AppUser> users,
    RoleManager<IdentityRole> roles,
    IClock clock,
    ILogger<DbInitializer> logger)
{
    public async Task RunAsync(CancellationToken ct = default)
    {
        if (db.Database.GetMigrations().Any())
        {
            await db.Database.MigrateAsync(ct);
        }
        else
        {

            logger.LogWarning("No EF Core migrations found. Using EnsureCreated(). Generate migrations for production.");
            await db.Database.EnsureCreatedAsync(ct);
        }

        await SeedUsersAsync();
        await SeedBuildingAsync(ct);
    }

    private async Task SeedUsersAsync()
    {
        foreach (var role in Roles.All)
            if (!await roles.RoleExistsAsync(role))
                await roles.CreateAsync(new IdentityRole(role));

        await EnsureUserAsync("Abenezer Yohannes", "admin@glory.et", "Admin@123", Roles.Admin);
        await EnsureUserAsync("Meron Tadesse", "manager@glory.et", "Manager@123", Roles.Manager);
        await EnsureUserAsync("Dawit Bekele", "viewer@glory.et", "Viewer@123", Roles.Viewer);
    }

    private async Task EnsureUserAsync(string fullName, string email, string password, string role)
    {
        if (await users.FindByEmailAsync(email) is not null) return;
        var user = new AppUser { UserName = email, Email = email, EmailConfirmed = true, FullName = fullName, IsActive = true };
        var result = await users.CreateAsync(user, password);
        if (!result.Succeeded)
        {
            logger.LogError("Could not seed user {Email}: {Errors}", email, string.Join("; ", result.Errors.Select(e => e.Description)));
            return;
        }
        await users.AddToRoleAsync(user, role);
    }

    private async Task SeedBuildingAsync(CancellationToken ct)
    {
        if (await db.Buildings.AnyAsync(ct)) return;

        var today = clock.Today;
        var thisMonth = new DateOnly(today.Year, today.Month, 1);

        var building = new Building { Name = "Glory Building", Address = "Bole Road, Addis Ababa" };
        var floors = Enumerable.Range(1, 7)
            .Select(n => new Floor { Building = building, FloorNumber = n, Name = $"Floor {n}" }).ToList();

        var units = new List<Unit>();
        foreach (var floor in floors)
        {
            for (var i = 1; i <= 6; i++)
            {
                var area = 38 + i * 11 + floor.FloorNumber * 4;
                var maintenance = (floor.FloorNumber == 1 && i >= 5) || (floor.FloorNumber == 4 && i == 2);
                var reserved = floor.FloorNumber == 6 && i == 6;
                var occupiedSlot = (floor.FloorNumber + i) % 3 != 0;
                var shop = floor.FloorNumber <= 2 && i <= 3;
                units.Add(new Unit
                {
                    Floor = floor,
                    FloorNumber = floor.FloorNumber,
                    UnitNumber = $"L{floor.FloorNumber}0{i}",
                    UnitType = shop ? UnitType.Shop : UnitType.Office,
                    AreaSqMeters = area,
                    MonthlyRent = Math.Round(area * (850m + floor.FloorNumber * 70m) / 100m) * 100m,
                    Status = maintenance ? UnitStatus.UnderMaintenance
                           : reserved ? UnitStatus.Reserved
                           : occupiedSlot ? UnitStatus.Occupied : UnitStatus.Available,
                    Description = shop ? "Street-facing retail space" : "Open-plan office with natural light"
                });
            }
        }

        var tenantSeed = new (string Name, BusinessType Type, string Contact, string Email)[]
        {
            ("Vanguard Media Group", BusinessType.Services, "Elena Rostova", "info@vanguardmedia.et"),
            ("Apex Architecture Studio", BusinessType.Services, "Samuel Girma", "hello@apexarch.et"),
            ("Helios Energy Corp", BusinessType.Office, "Kidist Alemu", "office@heliosenergy.et"),
            ("Sunrise Pharmacy", BusinessType.Healthcare, "Dr. Hanna Worku", "sunrise@pharmacy.et"),
            ("Habesha Coffee House", BusinessType.Restaurant, "Biruk Hailu", "biruk@habeshacoffee.et"),
            ("Nexus Tech Labs", BusinessType.Services, "Liya Tesfaye", "contact@nexustech.et"),
            ("Blue Nile Bookstore", BusinessType.Retail, "Yonas Desta", "shop@bluenile.et"),
            ("Zenith Kids Academy", BusinessType.Education, "Selam Mekonnen", "admin@zenithkids.et"),
            ("Urban Retail Flagship", BusinessType.Retail, "Tigist Abebe", "urban@retail.et"),
            ("Meskel Law Partners", BusinessType.Services, "Henok Assefa", "office@meskellaw.et")
        };
        var tenants = tenantSeed.Select((t, i) => new Tenant
        {
            OrganizationName = t.Name,
            TinNumber = (1000000001L + i * 7919L).ToString(),
            Phone = $"+2519{10000000 + i * 137}",
            BusinessType = t.Type,
            ContactPersonName = t.Contact,
            ContactEmail = t.Email,
            IsActive = true
        }).ToList();

        var documents = new List<LegalDocument>();
        DocumentType[] docTypes = [DocumentType.BusinessLicense, DocumentType.TradeLicense, DocumentType.TaxClearance];
        for (var i = 0; i < tenants.Count; i++)
        for (var j = 0; j < docTypes.Length; j++)
        {
            var expiring = (i + j) % 4 == 0;
            documents.Add(new LegalDocument
            {
                Tenant = tenants[i],
                DocumentType = docTypes[j],
                FileName = $"{tenants[i].OrganizationName.Split(' ')[0]}_{docTypes[j]}.pdf",
                FilePath = $"seed/{tenants[i].TinNumber}_{docTypes[j]}.pdf",
                UploadedAt = DateTime.UtcNow.AddMonths(-8 - j),
                ExpiryDate = expiring ? today.AddDays(6 + i * 3) : today.AddMonths(5 + j * 3),
                IsVerified = (i + j) % 5 != 0
            });
        }

        var leases = new List<Lease>();
        var occupied = units.Where(u => u.Status == UnitStatus.Occupied).ToList();
        for (var idx = 0; idx < occupied.Count; idx++)
        {
            var unit = occupied[idx];
            var start = today.AddMonths(-((idx % 9) + 1));
            var endingSoon = idx % 6 == 0;
            leases.Add(new Lease
            {
                Unit = unit,
                Tenant = tenants[idx % tenants.Count],
                StartDate = start,
                EndDate = endingSoon ? today.AddDays(8 + idx % 20) : start.AddMonths(12),
                MonthlyRent = unit.MonthlyRent,
                DepositAmount = unit.MonthlyRent * 3,
                Status = LeaseStatus.Active
            });
        }

        var invoices = new List<Invoice>();
        var payments = new List<Payment>();
        var refCounter = 1;
        for (var idx = 0; idx < leases.Count; idx++)
        {
            var lease = leases[idx];
            for (var back = 5; back >= 0; back--)
            {
                var period = thisMonth.AddMonths(-back);
                if (period < new DateOnly(lease.StartDate.Year, lease.StartDate.Month, 1)) continue;

                var due = period.AddDays(4);
                var invoice = new Invoice
                {
                    Lease = lease,
                    InvoiceNumber = $"INV-{period.Year}{period.Month:D2}-{idx + 1:D4}",
                    AmountDue = lease.MonthlyRent,
                    IssueDate = period,
                    DueDate = due,
                    PeriodMonth = period.Month,
                    PeriodYear = period.Year
                };

                decimal paid = 0;
                void Pay(decimal amount, DateOnly date, PaymentMethod method)
                {
                    if (date > today) date = today;
                    paid += amount;
                    payments.Add(new Payment
                    {
                        Invoice = invoice,
                        AmountPaid = amount,
                        PaymentDate = date,
                        PaymentMethod = method,
                        ReferenceNumber = $"TB-{period.Year}{period.Month:D2}-{refCounter++:D5}"
                    });
                }

                if (back >= 2) Pay(invoice.AmountDue, due.AddDays(-(idx % 4)), PaymentMethod.BankTransfer);
                else if (back == 1)
                {
                    if (idx % 4 != 0) Pay(invoice.AmountDue, due.AddDays(idx % 3), idx % 2 == 1 ? PaymentMethod.MobileMoney : PaymentMethod.BankTransfer);
                    else if (idx % 8 == 0) Pay(Math.Round(invoice.AmountDue / 2m), due.AddDays(2), PaymentMethod.Cash);
                }
                else if (idx % 5 == 0) Pay(invoice.AmountDue, today, PaymentMethod.MobileMoney);

                invoice.Status = paid >= invoice.AmountDue ? InvoiceStatus.Paid
                                : due < today ? InvoiceStatus.Overdue : InvoiceStatus.Issued;
                invoices.Add(invoice);
            }
        }

        db.Buildings.Add(building);
        db.Floors.AddRange(floors);
        db.Units.AddRange(units);
        db.Tenants.AddRange(tenants);
        db.LegalDocuments.AddRange(documents);
        db.Leases.AddRange(leases);
        db.Invoices.AddRange(invoices);
        db.Payments.AddRange(payments);
        await db.SaveChangesAsync(ct);

        await SeedNotificationsAsync(leases, ct);
        logger.LogInformation("Seeded demo data: {Units} units, {Tenants} tenants, {Leases} leases, {Invoices} invoices.",
            units.Count, tenants.Count, leases.Count, invoices.Count);
    }

    private async Task SeedNotificationsAsync(List<Lease> leases, CancellationToken ct)
    {
        var recipients = new[] { "admin@glory.et", "manager@glory.et" };
        var soon = leases.OrderBy(l => l.EndDate).First();
        foreach (var email in recipients)
        {
            var user = await users.FindByEmailAsync(email);
            if (user is null) continue;
            db.Notifications.AddRange(
                new Notification { UserId = user.Id, NotificationType = NotificationType.PaymentOverdue, Title = "Payment overdue", Message = "Some invoices are past their due date. Review the arrears report." },
                new Notification { UserId = user.Id, NotificationType = NotificationType.LeaseExpiry, Title = "Lease expiring soon", Message = $"The lease for unit {soon.Unit.UnitNumber} ends on {soon.EndDate:dd MMM yyyy}." },
                new Notification { UserId = user.Id, NotificationType = NotificationType.General, Title = "Welcome to Glory Building", Message = "Demo data has been loaded.", IsRead = true });
        }
        await db.SaveChangesAsync(ct);
    }
}


# Building Management System – Backend API

Multi-tenant commercial building rental management: units, tenants, leases, invoices & payments, reports (with Excel export), notifications, and JWT role-based security.

**Stack:** .NET 8 · ASP.NET Core Web API · EF Core 8 (Code-First) · SQL Server · ASP.NET Identity · FluentValidation · ClosedXML

## Solution layout

```
BMS.sln
├── BMS.API             Controllers, middleware, filters, Program.cs, background service
├── BMS.Application     Domain entities/enums, DTOs, service interfaces + implementations, validators, mapping
├── BMS.Infrastructure  AppDbContext, repositories/unit of work, seeding, JWT, file storage, Excel export
├── docs/               ERD (erd.mmd source, ERD.png, ERD.pdf)
└── postman/            BMS.postman_collection.json
```

**Note on layering:** the domain entities live in `BMS.Application/Domain` (not in a separate Domain project). Services depend on `IRepository<T>`/`IUnitOfWork` abstractions defined in Application, and Infrastructure implements them, so the dependency direction stays API → Infrastructure → Application and there are no circular references. Controllers contain no business logic and never return entities – only DTOs wrapped in `ApiResponse<T>`.

## Prerequisites

- .NET 8 SDK
- SQL Server: LocalDB (Windows, default) **or** Docker (`docker compose up -d`, see below)
- `dotnet tool install --global dotnet-ef` (for migrations)

## Run it

```bash
# 1. (optional, non-Windows) start SQL Server
docker compose up -d

# 2. create the initial migration (once)
dotnet ef migrations add InitialCreate -p BMS.Infrastructure -s BMS.API

# 3. run – the app applies migrations and seeds demo data on startup
dotnet run --project BMS.API
```

API: https://localhost:5001/api · Swagger: https://localhost:5001/swagger (enabled in Development).

If no migrations exist, startup falls back to `EnsureCreated()` so the app still runs; use migrations for anything beyond local demos. To apply manually: `dotnet ef database update -p BMS.Infrastructure -s BMS.API`.

### Configuration (`BMS.API/appsettings.json`, overridable by environment variables)

| Setting | Env var | Notes |
|---|---|---|
| Connection string | `ConnectionStrings__DefaultConnection` | Default is LocalDB. Docker example: `Server=localhost,1433;Database=BmsDb;User Id=sa;Password=Your_strong_Passw0rd;TrustServerCertificate=True` |
| JWT secret | `Jwt__Key` | **Dev-only value ships in appsettings – override in any real environment** (≥ 32 chars) |
| JWT lifetime | `Jwt__ExpiryMinutes`, `Jwt__RefreshTokenDays` | 60 min / 7 days |
| CORS | `Cors__Origins__0` | `http://localhost:4200` |
| Time zone | `Building__TimeZone` | `Africa/Addis_Ababa` – used for "today" in daily jobs |
| Uploads | `Storage__RootPath` | `uploads/` (git-ignored) |
| Daily jobs | `DailyJobs__Enabled`, `DailyJobs__StartupDelaySeconds` | |
| Swagger | `Swagger__Enabled` | Always on in Development |

### Seeded accounts (development)

| Role | Email | Password |
|---|---|---|
| Admin | admin@glory.et | Admin@123 |
| Manager | manager@glory.et | Manager@123 |
| Viewer | viewer@glory.et | Viewer@123 |

Seed data: 1 building, 7 floors × 6 units (42), 10 tenants with legal documents, ~28 leases, 6 months of invoices/payments, and sample notifications. Change or remove these credentials before deploying.

## Connect the Angular frontend

In `bms-frontend/src/environments/environment.ts` set:

```ts
apiUrl: 'https://localhost:5001/api',
useMock: false
```

then `ng serve` (http://localhost:4200 is allowed by CORS). Trust the dev certificate once with `dotnet dev-certs https --trust`.

## Security & roles

| Capability | Admin | Manager | Viewer |
|---|:-:|:-:|:-:|
| Read units, tenants, leases, invoices, payments, reports, dashboard | ✅ | ✅ | ✅ |
| Create/update/delete units, tenants, leases, payments, generate invoices, verify documents | ✅ | ✅ | ❌ |
| Assign roles | ✅ | ❌ | ❌ |

- JWT bearer; refresh tokens are stored hashed (SHA-256) and rotated on use.
- Self-registration always creates a **Viewer**; only an Admin can promote (the last Admin cannot be demoted).
- `/api/auth/*` is rate-limited (20 requests/min per IP).
- Passwords are hashed by ASP.NET Identity; uploads are stored under GUID file names (original names never used as paths).

## Key business rules

- A unit becomes **Occupied** only through an active lease; it returns to **Available** when the lease is terminated/expired. Units with lease history cannot be deleted (409).
- Only **one Active lease per unit** (enforced by service logic *and* a filtered unique index).
- Tenants with an active lease cannot be deactivated. TIN and unit number are unique among non-deleted rows.
- Invoices: one per lease per month (unique index → idempotent generation), due on the 5th. Partial payments are supported; status moves Issued → Paid / Overdue. Invoices use a `RowVersion` for optimistic concurrency.
- Soft delete on units, tenants and leases; audit fields (created/updated by & at) filled automatically.

## Daily background job

`DailyJobsBackgroundService` runs once at startup (after a short delay) and then every 24 hours:

1. Generate this month's invoices for active leases (idempotent)
2. Mark past-due invoices **Overdue**
3. Mark ended leases **Expired** and free their units
4. Create notifications: payment due (within 7 days), payment overdue, lease expiring (30- and 7-day milestones), document expiring (within 30 days). `Notification.ReferenceKey` prevents duplicates when the job re-runs.

## API overview

All responses use `{ "success": bool, "data": ..., "message": string?, "errors": [...]? }`. List endpoints return `{ items, totalCount, page, pageSize }` and accept `page`, `pageSize` (max 200) plus filters. Errors map to 400 (validation/business rule), 401, 404, 409.

| Area | Endpoints |
|---|---|
| Auth | `POST /auth/login`, `/auth/register`, `/auth/refresh`, `/auth/assign-role` |
| Units | `GET /units`, `/units/floors`, `/units/{id}` · `POST /units` · `PUT /units/{id}`, `/units/{id}/status` · `DELETE /units/{id}` |
| Tenants | `GET /tenants`, `/tenants/{id}` · `POST /tenants` · `PUT /tenants/{id}`, `/deactivate` · `POST /tenants/{id}/documents` · `PUT .../documents/{docId}/verify` · `GET .../documents/{docId}/download` |
| Leases | `GET /leases`, `/leases/expiring`, `/leases/{id}` · `POST /leases` · `PUT /leases/{id}/renew`, `/terminate` |
| Invoices | `GET /invoices`, `/invoices/unpaid` · `POST /invoices/generate` · `PUT /invoices/{id}/mark-paid` |
| Payments | `GET /payments`, `/payments/outstanding` · `POST /payments` |
| Reports | `GET /reports/occupancy | revenue | arrears | lease-expiry | document-expiry` · `GET /reports/export/{type}` (.xlsx) |
| Other | `GET /dashboard` · `GET /notifications`, `/notifications/unread-count` · `PUT /notifications/{id}/read`, `/notifications/read-all` |

Import `postman/BMS.postman_collection.json`, run **Auth → Login (Admin)** (stores the token), then call anything else.

## ERD

See `docs/ERD.png` / `docs/ERD.pdf` (Mermaid source: `docs/erd.mmd`).

## Assumptions / trade-offs

- Single building (seeded); `Floor` and `Building` tables exist so multi-building is an extension, not a rewrite.
- Local-disk file storage behind `IFileStorage` – swap for blob storage in production.
- Notifications go to all Admin/Manager users; no email/SMS delivery.
- Invoice due day (5th) and reminder windows are constants in the services; move to configuration if needed.

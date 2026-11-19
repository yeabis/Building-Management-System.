using BMS.Application.Common;
using BMS.Application.DTOs;
using FluentValidation;

namespace BMS.Application.Validators;

public class LoginRequestValidator : AbstractValidator<LoginRequest>
{
    public LoginRequestValidator()
    {
        RuleFor(x => x.Email).NotEmpty().EmailAddress().MaximumLength(256);
        RuleFor(x => x.Password).NotEmpty().MaximumLength(128);
    }
}

public class RegisterRequestValidator : AbstractValidator<RegisterRequest>
{
    public RegisterRequestValidator()
    {
        RuleFor(x => x.FullName).NotEmpty().MaximumLength(100);
        RuleFor(x => x.Email).NotEmpty().EmailAddress().MaximumLength(256);
        RuleFor(x => x.Password).NotEmpty().MinimumLength(8).MaximumLength(128)
            .WithMessage("Password must be 8 to 128 characters.");
    }
}

public class RefreshTokenRequestValidator : AbstractValidator<RefreshTokenRequest>
{
    public RefreshTokenRequestValidator() => RuleFor(x => x.RefreshToken).NotEmpty().MaximumLength(512);
}

public class AssignRoleRequestValidator : AbstractValidator<AssignRoleRequest>
{
    public AssignRoleRequestValidator()
    {
        RuleFor(x => x.Email).NotEmpty().EmailAddress();
        RuleFor(x => x.Role).NotEmpty().Must(r => Roles.All.Contains(r, StringComparer.OrdinalIgnoreCase))
            .WithMessage($"Role must be one of: {string.Join(", ", Roles.All)}.");
    }
}

public class UnitInputValidator : AbstractValidator<UnitInputDto>
{
    public UnitInputValidator()
    {
        RuleFor(x => x.UnitNumber).NotEmpty().MaximumLength(10)
            .Matches("^[A-Za-z0-9-]+$").WithMessage("Unit number may only contain letters, digits and dashes.");
        RuleFor(x => x.FloorNumber).InclusiveBetween(1, 100);
        RuleFor(x => x.AreaSqMeters).GreaterThan(0).LessThanOrEqualTo(100000);
        RuleFor(x => x.MonthlyRent).GreaterThanOrEqualTo(0).LessThanOrEqualTo(1_000_000_000m);
        RuleFor(x => x.Description).MaximumLength(500);
        RuleFor(x => x.UnitType).IsInEnum();
        RuleFor(x => x.Status).IsInEnum();
    }
}

public class ChangeUnitStatusValidator : AbstractValidator<ChangeUnitStatusRequest>
{
    public ChangeUnitStatusValidator() => RuleFor(x => x.Status).IsInEnum();
}

public class TenantInputValidator : AbstractValidator<TenantInputDto>
{
    public TenantInputValidator()
    {
        RuleFor(x => x.OrganizationName).NotEmpty().MinimumLength(2).MaximumLength(150);
        RuleFor(x => x.TinNumber).NotEmpty().Matches(@"^\d{10}$").WithMessage("TIN must be exactly 10 digits.");
        RuleFor(x => x.Phone).NotEmpty().Matches(@"^\+?[0-9\s-]{7,15}$").WithMessage("Enter a valid phone number.");
        RuleFor(x => x.BusinessType).IsInEnum();
        RuleFor(x => x.ContactPersonName).NotEmpty().MaximumLength(100);
        RuleFor(x => x.ContactEmail).NotEmpty().EmailAddress().MaximumLength(256);
    }
}

public class LeaseInputValidator : AbstractValidator<LeaseInputDto>
{
    public LeaseInputValidator()
    {
        RuleFor(x => x.UnitId).GreaterThan(0);
        RuleFor(x => x.TenantId).GreaterThan(0);
        RuleFor(x => x.StartDate).NotEqual(default(DateOnly)).WithMessage("Start date is required.");
        RuleFor(x => x.EndDate).GreaterThan(x => x.StartDate).WithMessage("End date must be after the start date.");
        RuleFor(x => x.MonthlyRent).GreaterThan(0).LessThanOrEqualTo(1_000_000_000m);
        RuleFor(x => x.DepositAmount).GreaterThanOrEqualTo(0).LessThanOrEqualTo(1_000_000_000m);
    }
}

public class RenewLeaseValidator : AbstractValidator<RenewLeaseRequest>
{
    public RenewLeaseValidator()
    {
        RuleFor(x => x.NewEndDate).NotEqual(default(DateOnly)).WithMessage("New end date is required.");
        RuleFor(x => x.NewMonthlyRent).GreaterThan(0).LessThanOrEqualTo(1_000_000_000m);
    }
}

public class TerminateLeaseValidator : AbstractValidator<TerminateLeaseRequest>
{
    public TerminateLeaseValidator() => RuleFor(x => x.Reason).NotEmpty().MinimumLength(5).MaximumLength(500);
}

public class PaymentInputValidator : AbstractValidator<PaymentInputDto>
{
    public PaymentInputValidator()
    {
        RuleFor(x => x.InvoiceId).GreaterThan(0);
        RuleFor(x => x.AmountPaid).GreaterThan(0).LessThanOrEqualTo(1_000_000_000m)
            .PrecisionScale(18, 2, false).WithMessage("Amount can have at most 2 decimal places.");
        RuleFor(x => x.PaymentDate).NotEqual(default(DateOnly)).WithMessage("Payment date is required.");
        RuleFor(x => x.PaymentMethod).IsInEnum();
        RuleFor(x => x.ReferenceNumber).NotEmpty().MinimumLength(3).MaximumLength(100);
        RuleFor(x => x.Notes).MaximumLength(500);
    }
}

public class GenerateInvoicesValidator : AbstractValidator<GenerateInvoicesRequest>
{
    public GenerateInvoicesValidator()
    {
        RuleFor(x => x.Month).InclusiveBetween(1, 12).When(x => x.Month.HasValue);
        RuleFor(x => x.Year).InclusiveBetween(2000, 2100).When(x => x.Year.HasValue);
    }
}


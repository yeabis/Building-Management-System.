using BMS.Application.Common;
using BMS.Application.Domain;
using BMS.Application.DTOs;
using BMS.Application.Interfaces;
using BMS.Application.Mapping;
using Microsoft.EntityFrameworkCore;

namespace BMS.Application.Services;

public class UnitService(
    IRepository<Unit> units,
    IRepository<Floor> floors,
    IRepository<Lease> leases,
    IUnitOfWork uow) : IUnitService
{
    public async Task<PagedResult<UnitDto>> GetUnitsAsync(UnitQuery q, CancellationToken ct = default)
    {
        var query = units.Query().WithTenant();

        if (!string.IsNullOrWhiteSpace(q.Search))
        {
            var s = q.Search.Trim();
            query = query.Where(u => u.UnitNumber.Contains(s) || (u.Description != null && u.Description.Contains(s)));
        }
        if (q.Floor.HasValue)
        {
            var floor = q.Floor.Value;
            query = query.Where(u => u.FloorNumber == floor);
        }
        if (q.UnitType.HasValue)
        {
            var type = q.UnitType.Value;
            query = query.Where(u => u.UnitType == type);
        }
        if (q.Status.HasValue)
        {
            var status = q.Status.Value;
            query = query.Where(u => u.Status == status);
        }

        var page = await query.OrderBy(u => u.FloorNumber).ThenBy(u => u.UnitNumber)
            .ToPagedAsync(q.Page, q.PageSize, ct);
        return page.Map(u => u.ToDto());
    }

    public async Task<UnitDto> GetByIdAsync(int id, CancellationToken ct = default)
    {
        var unit = await units.Query().WithTenant().FirstOrDefaultAsync(u => u.Id == id, ct)
                   ?? throw new NotFoundException("Unit not found.");
        return unit.ToDto();
    }

    public async Task<UnitDto> CreateUnitAsync(UnitInputDto input, CancellationToken ct = default)
    {
        if (input.Status == UnitStatus.Occupied)
            throw new BusinessRuleException("A unit becomes Occupied automatically when a lease is activated.");

        var floor = await floors.Query().FirstOrDefaultAsync(f => f.FloorNumber == input.FloorNumber, ct)
                    ?? throw new NotFoundException($"Floor {input.FloorNumber} does not exist in this building.");

        var number = input.UnitNumber.Trim();
        if (await units.AnyAsync(u => u.UnitNumber == number, ct))
            throw new ConflictException($"Unit {number} already exists.");

        var unit = new Unit
        {
            FloorId = floor.Id,
            FloorNumber = floor.FloorNumber,
            UnitNumber = number,
            UnitType = input.UnitType,
            AreaSqMeters = input.AreaSqMeters,
            MonthlyRent = input.MonthlyRent,
            Status = input.Status,
            Description = input.Description?.Trim()
        };
        await units.AddAsync(unit, ct);
        await uow.SaveChangesAsync(ct);
        return unit.ToDto();
    }

    public async Task<UnitDto> UpdateUnitAsync(int id, UnitInputDto input, CancellationToken ct = default)
    {
        var unit = await units.GetByIdAsync(id, ct) ?? throw new NotFoundException("Unit not found.");

        var hasActiveLease = await leases.AnyAsync(l => l.UnitId == id && l.Status == LeaseStatus.Active, ct);
        if (hasActiveLease && input.Status != UnitStatus.Occupied)
            throw new ConflictException("Terminate the active lease before changing this unit's status.");
        if (!hasActiveLease && input.Status == UnitStatus.Occupied)
            throw new BusinessRuleException("A unit becomes Occupied automatically when a lease is activated.");

        var number = input.UnitNumber.Trim();
        if (await units.AnyAsync(u => u.UnitNumber == number && u.Id != id, ct))
            throw new ConflictException($"Unit {number} already exists.");

        if (input.FloorNumber != unit.FloorNumber)
        {
            var floor = await floors.Query().FirstOrDefaultAsync(f => f.FloorNumber == input.FloorNumber, ct)
                        ?? throw new NotFoundException($"Floor {input.FloorNumber} does not exist in this building.");
            unit.FloorId = floor.Id;
            unit.FloorNumber = floor.FloorNumber;
        }

        unit.UnitNumber = number;
        unit.UnitType = input.UnitType;
        unit.AreaSqMeters = input.AreaSqMeters;
        unit.MonthlyRent = input.MonthlyRent;
        unit.Status = input.Status;
        unit.Description = input.Description?.Trim();
        await uow.SaveChangesAsync(ct);
        return await GetByIdAsync(id, ct);
    }

    public async Task<UnitDto> ChangeStatusAsync(int id, UnitStatus status, CancellationToken ct = default)
    {
        var unit = await units.GetByIdAsync(id, ct) ?? throw new NotFoundException("Unit not found.");
        var hasActiveLease = await leases.AnyAsync(l => l.UnitId == id && l.Status == LeaseStatus.Active, ct);

        if (hasActiveLease && status != UnitStatus.Occupied)
            throw new ConflictException("Terminate the active lease before changing this unit's status.");
        if (!hasActiveLease && status == UnitStatus.Occupied)
            throw new BusinessRuleException("A unit becomes Occupied automatically when a lease is activated.");

        unit.Status = status;
        await uow.SaveChangesAsync(ct);
        return await GetByIdAsync(id, ct);
    }

    public async Task DeleteUnitAsync(int id, CancellationToken ct = default)
    {
        var unit = await units.GetByIdAsync(id, ct) ?? throw new NotFoundException("Unit not found.");
        if (await leases.AnyAsync(l => l.UnitId == id, ct))
            throw new ConflictException("This unit has an active or historical lease and cannot be deleted.");

        units.Remove(unit);
        await uow.SaveChangesAsync(ct);
    }

    public async Task<IReadOnlyList<FloorSummaryDto>> GetFloorSummaryAsync(CancellationToken ct = default)
    {
        var floorNumbers = await floors.Query().OrderBy(f => f.FloorNumber).Select(f => f.FloorNumber).ToListAsync(ct);
        var all = await units.Query().WithTenant()
            .OrderBy(u => u.FloorNumber).ThenBy(u => u.UnitNumber).ToListAsync(ct);

        return floorNumbers.Select(n =>
        {
            var onFloor = all.Where(u => u.FloorNumber == n).ToList();
            int Count(UnitStatus s) => onFloor.Count(u => u.Status == s);
            return new FloorSummaryDto(n, onFloor.Count, Count(UnitStatus.Occupied), Count(UnitStatus.Available),
                Count(UnitStatus.UnderMaintenance), Count(UnitStatus.Reserved), onFloor.Select(u => u.ToDto()).ToList());
        }).ToList();
    }
}


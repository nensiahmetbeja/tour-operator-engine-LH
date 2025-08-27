using Lufthansa.Application.Services;
using Lufthansa.Application.TourOperators.DTOs;
using Lufthansa.Domain.Entities;
using Lufthansa.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Lufthansa.Infrastructure.Services;

public class TourOperatorService(ApplicationDbContext db) : ITourOperatorService
{
    public async Task<IReadOnlyList<TourOperatorDto>> GetAllAsync(CancellationToken ct = default) =>
        await db.TourOperators
            .AsNoTracking()
            .Select(t => new TourOperatorDto(t.Id, t.Name, t.Code, t.CreatedAt))
            .ToListAsync(ct);

    public async Task<TourOperatorDto?> GetByIdAsync(Guid id, CancellationToken ct = default) =>
        await db.TourOperators
            .AsNoTracking()
            .Where(t => t.Id == id)
            .Select(t => new TourOperatorDto(t.Id, t.Name, t.Code, t.CreatedAt))
            .FirstOrDefaultAsync(ct);

    public async Task<TourOperatorDto> CreateAsync(CreateTourOperatorRequest req, CancellationToken ct = default)
    {
        // basic uniqueness check (you also have a unique index on Code)
        var exists = await db.TourOperators.AnyAsync(t => t.Code == req.Code, ct);
        if (exists) throw new InvalidOperationException("Code already exists.");

        var entity = new TourOperator
        {
            Id = Guid.NewGuid(),
            Name = req.Name.Trim(),
            Code = req.Code.Trim().ToUpperInvariant(),
            CreatedAt = DateTime.UtcNow
        };
        db.TourOperators.Add(entity);
        await db.SaveChangesAsync(ct);

        return new TourOperatorDto(entity.Id, entity.Name, entity.Code, entity.CreatedAt);
    }

    public async Task<bool> DeleteAsync(Guid id, CancellationToken ct = default)
    {
        var e = await db.TourOperators.FindAsync([id], ct);
        if (e is null) return false;
        db.TourOperators.Remove(e);
        await db.SaveChangesAsync(ct);
        return true;
    }
}
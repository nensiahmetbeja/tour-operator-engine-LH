// using Lufthansa.Application.Services;
// using Lufthansa.Domain.Entities;
// using Lufthansa.Infrastructure.Persistence;
// using Microsoft.EntityFrameworkCore;
//
// namespace Lufthansa.Application.Services;
//
// public class TourOperatorService : ITourOperatorService
// {
//     private readonly ApplicationDbContext _db;
//
//     public TourOperatorService(ApplicationDbContext db)
//     {
//         _db = db;
//     }
//
//     public async Task<IEnumerable<TourOperator>> GetAllAsync()
//     {
//         return await _db.TourOperators.AsNoTracking().ToListAsync();
//     }
//
//     public async Task<TourOperator?> GetByIdAsync(Guid id)
//     {
//         return await _db.TourOperators.AsNoTracking()
//             .FirstOrDefaultAsync(t => t.Id == id);
//     }
//
//     public async Task<TourOperator> CreateAsync(string name, string code)
//     {
//         var op = new TourOperator
//         {
//             Name = name,
//             Code = code,
//             CreatedAt = DateTime.UtcNow
//         };
//         _db.TourOperators.Add(op);
//         await _db.SaveChangesAsync();
//         return op;
//     }
//
//     public async Task<bool> DeleteAsync(Guid id)
//     {
//         var entity = await _db.TourOperators.FindAsync(id);
//         if (entity == null) return false;
//         _db.TourOperators.Remove(entity);
//         await _db.SaveChangesAsync();
//         return true;
//     }
// }
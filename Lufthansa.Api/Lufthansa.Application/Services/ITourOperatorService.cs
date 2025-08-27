using Lufthansa.Application.TourOperators.DTOs;

namespace Lufthansa.Application.Services;

public interface ITourOperatorService
{
    Task<IReadOnlyList<TourOperatorDto>> GetAllAsync(CancellationToken ct = default);
    Task<TourOperatorDto?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<TourOperatorDto> CreateAsync(CreateTourOperatorRequest req, CancellationToken ct = default);
    Task<bool> DeleteAsync(Guid id, CancellationToken ct = default);
}
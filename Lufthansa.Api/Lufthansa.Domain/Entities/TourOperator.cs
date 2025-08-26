namespace Lufthansa.Domain.Entities;

public class TourOperator
{
    public Guid Id { get; set; }
    public string Name { get; set; } = default!;
    public string Code { get; set; } = default!;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
namespace Lufthansa.Domain.Entities;

public class Season
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid TourOperatorId { get; set; }
    public string Code { get; set; } = default!;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
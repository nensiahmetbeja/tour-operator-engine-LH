namespace Lufthansa.Domain.Entities;

public class DailyPricing
{
    public long Id { get; set; }
    public Guid TourOperatorId { get; set; }
    public Guid RouteId { get; set; }
    public Guid SeasonId { get; set; }
    public DateOnly Date { get; set; }
    public decimal EconomyPrice { get; set; }
    public decimal BusinessPrice { get; set; }
    public int EconomySeats { get; set; }
    public int BusinessSeats { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
using Lufthansa.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using EFCore.NamingConventions;
namespace Lufthansa.Infrastructure.Persistence;

public class ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : DbContext(options)
{
    public DbSet<TourOperator> TourOperators => Set<TourOperator>();
    public DbSet<DailyPricing> DailyPricings => Set<DailyPricing>();
    public DbSet<Route> Routes => Set<Route>();
    public DbSet<Season> Seasons => Set<Season>();

    protected override void OnModelCreating(ModelBuilder b)
    {
        // b.UseSnakeCaseNamingConvention(); // from EFCore.NamingConventions
        b.Entity<TourOperator>()
            .HasIndex(x => x.Code).IsUnique();
        
        b.Entity<Route>()
            .HasIndex(x => new { x.TourOperatorId, x.Code }).IsUnique();

        b.Entity<Season>()
            .HasIndex(x => new { x.TourOperatorId, x.Code }).IsUnique();

        b.Entity<DailyPricing>()
            .HasIndex(x => new { x.TourOperatorId, x.RouteId, x.SeasonId, x.Date }).IsUnique();

        base.OnModelCreating(b);
    }
}
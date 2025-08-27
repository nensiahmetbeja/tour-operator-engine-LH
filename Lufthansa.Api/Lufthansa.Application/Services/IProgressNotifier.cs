namespace Lufthansa.Application.Services;

public interface IProgressNotifier
{
    Task ReportAsync(
        string? connectionId,
        string stage,
        int? pct,
        string message,
        CancellationToken ct = default);
}
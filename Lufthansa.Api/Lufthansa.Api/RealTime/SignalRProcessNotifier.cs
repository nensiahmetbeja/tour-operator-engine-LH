using Lufthansa.Application.Services;
using Lufthansa.Api.Hubs;
using Microsoft.AspNetCore.SignalR;

namespace Lufthansa.Api.RealTime;

public class SignalRProgressNotifier : IProgressNotifier
{
    private readonly IHubContext<UploadHub> hub;

    public SignalRProgressNotifier(IHubContext<UploadHub> hub)
    {
        this.hub = hub;
    }

    public Task ReportAsync(
        string? connectionId,
        string stage,
        int? pct,
        string message,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(connectionId))
            return Task.CompletedTask;

        return hub.Clients.Client(connectionId)
            .SendAsync("progress", new { stage, pct, message }, ct);
    }
}
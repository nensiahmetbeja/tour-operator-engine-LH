using Lufthansa.Application.Services;
using Lufthansa.Api.Hubs;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging; 

namespace Lufthansa.Api.RealTime;

public class SignalRProgressNotifier : IProgressNotifier
{
    private readonly IHubContext<UploadHub> hub;
    private readonly ILogger logger;

    public SignalRProgressNotifier(IHubContext<UploadHub> hub, ILogger<SignalRProgressNotifier> logger) // added logger
    {
        this.hub = hub;
        this.logger = logger;
    }

    public async Task ReportAsync(
        string? connectionId,
        string stage,
        int? pct,
        string message,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(connectionId))
        {
            logger.LogDebug("SignalR progress skipped: empty connection id. Stage={Stage}, Pct={Pct}", stage, pct); // added
            return;
        }

        try
        {
            logger.LogDebug("SignalR progress sending: ConnectionId={ConnectionId}, Stage={Stage}, Pct={Pct}", connectionId, stage, pct); // added
            await hub.Clients.Client(connectionId)
                .SendAsync("progress", new { stage, pct, message }, ct);

            // Log at Information for key lifecycle stages
            if (stage is "validation_started" or "bulk_insert_started" or "bulk_insert_completed" or "done")
            {
                logger.LogInformation("SignalR progress sent: ConnectionId={ConnectionId}, Stage={Stage}, Pct={Pct}", connectionId, stage, pct); // added
            }
        }
        catch (OperationCanceledException)
        {
            logger.LogWarning("SignalR progress send canceled: ConnectionId={ConnectionId}, Stage={Stage}", connectionId, stage); // added
            throw;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "SignalR progress send failed: ConnectionId={ConnectionId}, Stage={Stage}, Pct={Pct}", connectionId, stage, pct); // added
            throw;
        }
    }
}
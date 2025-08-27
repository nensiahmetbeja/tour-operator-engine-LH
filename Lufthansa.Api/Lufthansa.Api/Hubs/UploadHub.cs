using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace Lufthansa.Api.Hubs;

[Authorize]
public class UploadHub : Hub
{
}

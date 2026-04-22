using Dashboard.BLL.Services;
using Dashboard.PL.Hubs;
using Microsoft.AspNetCore.SignalR;

namespace Dashboard.PL.Services;

/**
 * Enterprise Pulse Dispatcher: Optimized SignalR
 * Uses IHubContext for secure, authenticated delivery.
 */
public class DashboardUpdateService : IDashboardUpdateService
{
    private readonly IHubContext<SignalRHub> _hubContext;

    public DashboardUpdateService(IHubContext<SignalRHub> hubContext)
    {
        _hubContext = hubContext;
    }

    public async Task PushPulseAsync(string groupName, string methodName, object data)
    {
        // Broadcast via SignalR to the specific method (e.g. "DashboardUpdate" or "SystemPressure")
        await _hubContext.Clients.Group(groupName).SendAsync(methodName, data);
    }
}

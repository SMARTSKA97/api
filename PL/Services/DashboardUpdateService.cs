using Dashboard.BLL.Services;
using Dashboard.PL.Hubs;
using Dashboard.PL.Models;
using Microsoft.AspNetCore.SignalR;
using System.Collections.Concurrent;

namespace Dashboard.PL.Services;

/**
 * Enterprise Pulse Dispatcher: Optimized SignalR with Gap Detection
 * Uses Per-Group Sequence IDs and a thread-safe history buffer.
 */
public class DashboardUpdateService : IDashboardUpdateService
{
    private readonly IHubContext<SignalRHub> _hubContext;
    private readonly int _maxHistorySize;
    private readonly ConcurrentDictionary<string, long> _groupSequences = new();
    private readonly ConcurrentQueue<SequencedMessage> _history = new();

    public DashboardUpdateService(IHubContext<SignalRHub> hubContext, IConfiguration configuration)
    {
        _hubContext = hubContext;
        _maxHistorySize = configuration.GetValue<int>("SignalRSettings:MaxHistorySize", 1000);
    }

    public async Task PushPulseAsync(string groupName, string methodName, object data)
    {
        // 1. Increment Sequence for this specific group
        long sid = _groupSequences.AddOrUpdate(groupName, 1, (_, val) => val + 1);

        // 2. Wrap in Sequenced Envelope
        var pulse = new SequencedPulse
        {
            GroupName = groupName,
            sid = sid,
            m = methodName,
            d = data
        };

        // 3. Store in History (Non-blocking)
        _history.Enqueue(new SequencedMessage
        {
            SequenceId = sid,
            GroupName = groupName,
            Method = methodName,
            Data = data
        });

        // 4. Prune History if needed
        if (_history.Count > _maxHistorySize)
        {
            _history.TryDequeue(out _);
        }

        // 5. Broadcast via SignalR
        await _hubContext.Clients.Group(groupName).SendAsync(methodName, pulse);
    }

    public Task<IEnumerable<object>> GetMetricsGapAsync(string groupName, long lastId, long currentId)
    {
        // Filter history for the specific group and missing range
        var gap = _history
            .Where(m => m.GroupName == groupName && m.SequenceId > lastId && m.SequenceId <= currentId)
            .OrderBy(m => m.SequenceId)
            .Select(m => new SequencedPulse
            {
                GroupName = m.GroupName,
                sid = m.SequenceId,
                m = m.Method,
                d = m.Data
            })
            .Cast<object>();

        return Task.FromResult(gap);
    }
}

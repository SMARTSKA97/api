using System.Diagnostics;
using Dashboard.DAL;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Dashboard.BLL.Utilities;

public interface IResourceMonitor
{
    Task<(int cpu, int ram, int db)> GetMetricsAsync();
}

/**
 * Optimized Resource Monitor: Singleton Snapshot Pattern
 * Background thread refreshes vitals to prevent blocking hot paths
 * and connection pool exhaustion.
 */
public class ResourceMonitor : IResourceMonitor, IDisposable
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<ResourceMonitor> _logger;
    private readonly Process _currentProcess;
    private readonly System.Timers.Timer _refreshTimer;
    
    private int _cpu = 0;
    private int _ram = 0;
    private int _db = 0;
    
    private TimeSpan _lastCpuTime;
    private DateTime _lastSampleTime;

    public ResourceMonitor(IServiceProvider serviceProvider, ILogger<ResourceMonitor> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
        _currentProcess = Process.GetCurrentProcess();
        _lastCpuTime = _currentProcess.TotalProcessorTime;
        _lastSampleTime = DateTime.UtcNow;

        _refreshTimer = new System.Timers.Timer(2000); // Refresh every 2s
        _refreshTimer.Elapsed += async (s, e) => await RefreshMetricsAsync();
        _refreshTimer.Start();
        
        // Initial refresh
        _ = RefreshMetricsAsync();
    }

    public Task<(int cpu, int ram, int db)> GetMetricsAsync()
    {
        return Task.FromResult((_cpu, _ram, _db));
    }

    private async Task RefreshMetricsAsync()
    {
        try
        {
            // 1. CPU
            var currentCpuTime = _currentProcess.TotalProcessorTime;
            var currentTime = DateTime.UtcNow;
            
            var cpuUsedMs = (currentCpuTime - _lastCpuTime).TotalMilliseconds;
            var totalMs = (currentTime - _lastSampleTime).TotalMilliseconds;
            
            if (totalMs > 0)
            {
                _cpu = (int)((cpuUsedMs / (Environment.ProcessorCount * totalMs)) * 100);
                _cpu = Math.Clamp(_cpu, 0, 100);
            }

            _lastCpuTime = currentCpuTime;
            _lastSampleTime = currentTime;

            // 2. RAM
            _ram = (int)(_currentProcess.PrivateMemorySize64 / (1024 * 1024));

            // 3. DB Connections
            using var scope = _serviceProvider.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            
            var result = await context.ScalarResults
                .FromSqlRaw("SELECT count(*)::text as value FROM pg_stat_activity WHERE datname = current_database()")
                .FirstOrDefaultAsync();
            
            if (result != null && int.TryParse(result.Value, out var count))
            {
                _db = count;
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning("Resource Monitor Refresh Failed: {Msg}", ex.Message);
        }
    }

    public void Dispose()
    {
        _refreshTimer.Stop();
        _refreshTimer.Dispose();
        _currentProcess.Dispose();
    }
}

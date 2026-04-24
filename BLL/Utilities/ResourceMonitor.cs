using System.Diagnostics;
using Dashboard.DAL;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace Dashboard.BLL.Utilities;

public interface IResourceMonitor
{
    Task<(int cpu, int ram, int db)> GetMetricsAsync();
}

public class ResourceMonitor : IResourceMonitor
{
    private readonly AppDbContext _context;
    private readonly Process _currentProcess;

    public ResourceMonitor(AppDbContext context)
    {
        _context = context;
        _currentProcess = Process.GetCurrentProcess();
    }

    public async Task<(int cpu, int ram, int db)> GetMetricsAsync()
    {
        // CPU: Simple calculation of process processor time
        var startTime = DateTime.UtcNow;
        var startCpuUsage = _currentProcess.TotalProcessorTime;
        
        await Task.Delay(100); // Sample over 100ms
        
        var endTime = DateTime.UtcNow;
        var endCpuUsage = _currentProcess.TotalProcessorTime;
        
        var cpuUsedMs = (endCpuUsage - startCpuUsage).TotalMilliseconds;
        var totalMs = (endTime - startTime).TotalMilliseconds;
        var cpuPercentage = (int)((cpuUsedMs / (Environment.ProcessorCount * totalMs)) * 100);

        // RAM: Private memory in MB
        var ramMb = (int)(_currentProcess.PrivateMemorySize64 / (1024 * 1024));

        // DB: Active connections to our database
        int dbConnections = 0;
        try
        {
            var result = await _context.ScalarResults
                .FromSqlRaw("SELECT count(*)::text as value FROM pg_stat_activity WHERE datname = current_database()")
                .FirstOrDefaultAsync();
            
            if (result != null && int.TryParse(result.Value, out var count))
            {
                dbConnections = count;
            }
        }
        catch
        {
            // Fallback if query fails
            dbConnections = -1;
        }

        return (Math.Clamp(cpuPercentage, 0, 100), ramMb, dbConnections);
    }
}

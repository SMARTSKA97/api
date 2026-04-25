using System.Collections.Concurrent;
using System.Diagnostics;
using Dashboard.BLL.Utilities;
using Dashboard.BLL.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.DependencyInjection;

namespace Dashboard.BLL.Services;

public class LoadTestService : ILoadTestService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly IDashboardUpdateService _updateService;
    private readonly ILogger<LoadTestService> _logger;

    private CancellationTokenSource? _cts;
    private readonly ConcurrentQueue<long> _latencies = new();
    private readonly ConcurrentQueue<long> _dbTimes = new();
    private long _successCount = 0;
    private long _errorCount = 0;
    private int _concurrency = 0;
    private bool _isAutoScale = false;
    private string _status = "Stopped";

    private readonly System.Timers.Timer _reportTimer;

    public LoadTestService(
        IServiceProvider serviceProvider,
        IDashboardUpdateService updateService,
        ILogger<LoadTestService> logger)
    {
        _serviceProvider = serviceProvider;
        _updateService = updateService;
        _logger = logger;

        _reportTimer = new System.Timers.Timer(1000);
        _reportTimer.Elapsed += async (s, e) => await ReportMetricsAsync();
    }

    public async Task StartAsync(int concurrency, int intensity, bool autoScale = false)
    {
        if (_status == "Running") return;

        _cts = new CancellationTokenSource();
        _status = "Running";
        _concurrency = concurrency;
        _isAutoScale = autoScale;
        _successCount = 0;
        _errorCount = 0;
        
        // Clear history
        while (_latencies.TryDequeue(out _)) { }
        while (_dbTimes.TryDequeue(out _)) { }

        _logger.LogInformation("Starting Load Test Engine with {Concurrency} workers", concurrency);

        for (int i = 0; i < concurrency; i++)
        {
            _ = Task.Run(() => WorkerLoop(_cts.Token));
        }

        _reportTimer.Start();
    }

    public async Task StopAsync()
    {
        if (_status != "Running") return;

        _status = "Stopping";
        _cts?.Cancel();
        _reportTimer.Stop();
        
        // Final report with Stopped status
        _status = "Stopped";
        _concurrency = 0;
        await ReportMetricsAsync();
    }

    public LoadTestMetrics GetCurrentStatus()
    {
        return CalculateMetrics();
    }

    private async Task WorkerLoop(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            var swTotal = Stopwatch.StartNew();
            try
            {
                using var scope = _serviceProvider.CreateScope();
                var simulation = scope.ServiceProvider.GetRequiredService<ISimulationService>();
                
                var swDb = Stopwatch.StartNew();
                await simulation.RunCycleAsync(ct);
                swDb.Stop();
                
                _dbTimes.Enqueue(swDb.ElapsedMilliseconds);
                Interlocked.Increment(ref _successCount);
            }
            catch (OperationCanceledException) { } // Shutdown
            catch (Exception ex)
            {
                _logger.LogError(ex, "Load worker failed");
                Interlocked.Increment(ref _errorCount);
            }
            finally
            {
                swTotal.Stop();
                _latencies.Enqueue(swTotal.ElapsedMilliseconds);
            }

            // Small cooldown to prevent thread starvation if needed
            await Task.Delay(10, ct);
        }
    }

    private async Task ReportMetricsAsync()
    {
        var metrics = CalculateMetrics();
        
        using var scope = _serviceProvider.CreateScope();
        var monitor = scope.ServiceProvider.GetRequiredService<IResourceMonitor>();
        var (cpu, ram, db) = await monitor.GetMetricsAsync();

        var pulse = new EnginePulse
        {
            Metrics = metrics,
            Vitals = new EngineVitals
            {
                Cpu = cpu,
                Ram = ram,
                DbConnections = db
            }
        };

        try
        {
            await _updateService.PushPulseAsync("Engine:Admin", "EngineUpdate", pulse);

            // Auto-Scale logic
            if (_isAutoScale && _status == "Running" && metrics.AvgLatency < 200 && metrics.Bottleneck == "None")
            {
                // Increase by 10% or at least 10 workers
                int increase = Math.Max(10, (int)(_concurrency * 0.1));
                
                // Limit to 5000 workers to prevent extreme starvation
                if (_concurrency + increase <= 5000)
                {
                    _logger.LogInformation("Auto-scaling: Adding {Increase} workers. New total: {Total}", increase, _concurrency + increase);
                    for (int i = 0; i < increase; i++)
                    {
                        _ = Task.Run(() => WorkerLoop(_cts!.Token));
                    }
                    _concurrency += increase;
                }
            }
        }
        catch (OperationCanceledException) { }
        catch (ObjectDisposedException) { }
        catch (Exception ex)
        {
            _logger.LogWarning("Engine Report Failed: {Msg}", ex.Message);
        }
    }

    private LoadTestMetrics CalculateMetrics()
    {
        // Calculate RPS based on the last second (approximate from queue size change or just throughput)
        // For simplicity, we'll use a snapshot of recent latencies
        var recentLatencies = _latencies.TakeLast(Math.Max(1, _concurrency * 2)).ToList();
        var recentDbTimes = _dbTimes.TakeLast(Math.Max(1, _concurrency * 2)).ToList();

        double avgLatency = recentLatencies.Any() ? recentLatencies.Average() : 0;
        double avgDb = recentDbTimes.Any() ? recentDbTimes.Average() : 0;

        // Prune queues to keep memory stable
        while (_latencies.Count > 1000) _latencies.TryDequeue(out _);
        while (_dbTimes.Count > 1000) _dbTimes.TryDequeue(out _);

        string bottleneck = "None";
        if (avgDb > avgLatency * 0.7) bottleneck = "DB";
        else if (avgLatency > 500) bottleneck = "API";

        return new LoadTestMetrics
        {
            Status = _status,
            Rps = recentLatencies.Count, // Transactions processed in the sample period
            AvgLatency = avgLatency,
            DbTimeMs = avgDb,
            ApiTimeMs = avgLatency - avgDb,
            SuccessCount = _successCount,
            ErrorCount = _errorCount,
            ActiveWorkers = _concurrency,
            Bottleneck = bottleneck
        };
    }
}

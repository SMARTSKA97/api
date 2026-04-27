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
    
    // Persistent counters (Static to survive DI resets if process stays alive)
    private static long _totalSuccess = 0;
    private static long _totalError = 0;
    
    private long _lastReportedTotal = 0;
    private int _concurrency = 0;
    private bool _isAutoScale = false;
    private string _status = "Stopped";

    private readonly System.Timers.Timer _reportTimer;
    private readonly Stopwatch _reportStopwatch = new();
    
    private LoadTestMetrics _cachedMetrics = new();

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
        
        // Reset counters only on explicit Start
        Interlocked.Exchange(ref _totalSuccess, 0);
        Interlocked.Exchange(ref _totalError, 0);
        _lastReportedTotal = 0;
        
        // Clear history
        while (_latencies.TryDequeue(out _)) { }
        while (_dbTimes.TryDequeue(out _)) { }

        _logger.LogInformation("Starting Load Test Engine with {Concurrency} workers", concurrency);

        for (int i = 0; i < concurrency; i++)
        {
            _ = Task.Run(() => WorkerLoop(_cts.Token));
        }

        _reportStopwatch.Restart();
        _reportTimer.Start();
    }

    public async Task StopAsync()
    {
        if (_status != "Running" && _status != "Stopping") return;

        _status = "Stopped";
        _cts?.Cancel();
        _reportTimer.Stop();
        _reportStopwatch.Stop();
        
        _concurrency = 0;
        await ReportMetricsAsync();
    }

    public LoadTestMetrics GetCurrentStatus()
    {
        return _cachedMetrics;
    }

    private async Task WorkerLoop(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            try
            {
                using var scope = _serviceProvider.CreateScope();
                var simulation = scope.ServiceProvider.GetRequiredService<ISimulationService>();
                
                // Run multiple cycles per scope for efficiency
                for (int i = 0; i < 10; i++)
                {
                    if (ct.IsCancellationRequested) break;

                    var swTotal = Stopwatch.StartNew();
                    try
                    {
                        var swDb = Stopwatch.StartNew();
                        await simulation.RunCycleAsync(ct);
                        swDb.Stop();
                        
                        _dbTimes.Enqueue(swDb.ElapsedMilliseconds);
                        Interlocked.Increment(ref _totalSuccess);
                    }
                    catch (OperationCanceledException) { break; }
                    catch (Exception)
                    {
                        Interlocked.Increment(ref _totalError);
                    }
                    finally
                    {
                        swTotal.Stop();
                        _latencies.Enqueue(swTotal.ElapsedMilliseconds);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning("Worker loop scope error: {Msg}", ex.Message);
                await Task.Delay(1000, ct);
            }

            await Task.Delay(10, ct);
        }
    }

    private async Task ReportMetricsAsync()
    {
        // 1. Recalculate Metrics (Thread-safe snapshot)
        _cachedMetrics = RecalculateMetricsInternal();
        
        // 2. Get Resource Vitals
        using var scope = _serviceProvider.CreateScope();
        var monitor = scope.ServiceProvider.GetRequiredService<IResourceMonitor>();
        var (cpu, ram, db) = await monitor.GetMetricsAsync();

        var pulse = new EnginePulse
        {
            metrics = _cachedMetrics,
            vitals = new EngineVitals
            {
                cpu = cpu,
                ram = ram,
                dbConn = db
            }
        };

        try
        {
            await _updateService.PushPulseAsync("Engine:Admin", "EngineUpdate", pulse);

            // Auto-Scale logic (POC scale limits)
            if (_isAutoScale && _status == "Running" && _cachedMetrics.avgLatency < 250 && _cachedMetrics.bottleneck == "None")
            {
                int increase = Math.Max(5, (int)(_concurrency * 0.1));
                if (_concurrency + increase <= 1000) 
                {
                    for (int i = 0; i < increase; i++)
                    {
                        _ = Task.Run(() => WorkerLoop(_cts!.Token));
                    }
                    _concurrency += increase;
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning("Engine Report Failed: {Msg}", ex.Message);
        }
    }

    private LoadTestMetrics RecalculateMetricsInternal()
    {
        // Calculate Real RPS (Delta based on actual wall-clock time)
        long currentTotal = Interlocked.Read(ref _totalSuccess) + Interlocked.Read(ref _totalError);
        double elapsedSeconds = _reportStopwatch.Elapsed.TotalSeconds;
        _reportStopwatch.Restart();

        long delta = currentTotal - _lastReportedTotal;
        _lastReportedTotal = currentTotal;

        int rpsValue = (int)(delta / (elapsedSeconds > 0 ? elapsedSeconds : 1));

        // Sample Latencies (Keep it lean)
        var recentLatencies = _latencies.TakeLast(200).ToList();
        var recentDbTimes = _dbTimes.TakeLast(200).ToList();

        double avgLatency = recentLatencies.Any() ? recentLatencies.Average() : 0;
        double avgDb = recentDbTimes.Any() ? recentDbTimes.Average() : 0;

        // Prune Queues
        while (_latencies.Count > 500) _latencies.TryDequeue(out _);
        while (_dbTimes.Count > 500) _dbTimes.TryDequeue(out _);

        // Bottleneck Analysis
        string bottleneck = "None";
        if (avgDb > avgLatency * 0.75 && avgLatency > 50) bottleneck = "DB";
        else if (avgLatency > 500) bottleneck = "API";

        return new LoadTestMetrics
        {
            status = _status,
            rps = rpsValue,
            avgLatency = avgLatency,
            dbTimeMs = avgDb,
            apiTimeMs = Math.Max(0, avgLatency - avgDb),
            successCount = Interlocked.Read(ref _totalSuccess),
            errorCount = Interlocked.Read(ref _totalError),
            activeWorkers = _concurrency,
            bottleneck = bottleneck
        };
    }
}

using System.Collections.Concurrent;
using System.Threading.Channels;
using Dashboard.BLL.Services;
using Dashboard.DAL.Repositories;
using Dashboard.PL.Models;
using Dashboard.PL.Services;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace Dashboard.PL.Workers;

public class DashboardAggregationWorker : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<DashboardAggregationWorker> _logger;
    private readonly string _connectionString;
    private readonly int _debounceMs;
    private readonly int _jitterMs;
    private int _adaptiveDebounceMs;

    // Pulse Coalescing Buffer: Stores unique targets to fetch in the next flush cycle
    // Key format: "SCOPE:FY:DDO:USER:TYPE"
    private readonly ConcurrentDictionary<string, string> _pulseBuffer = new();
    private bool _isRevivalPending = false;

    public DashboardAggregationWorker(
        IServiceProvider serviceProvider,
        ILogger<DashboardAggregationWorker> logger,
        IConfiguration configuration)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
        _connectionString = configuration.GetConnectionString("DefaultConnection") ?? "";
        _debounceMs = configuration.GetValue<int>("SignalRSettings:PulseDebounceMs", 500);
        _jitterMs = configuration.GetValue<int>("SignalRSettings:JitterMaxMs", 30);
        _adaptiveDebounceMs = _debounceMs;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Enterprise Pulse Engine (POC) starting...");
        
        var maintenanceTask = PeriodicMaintenanceAsync(stoppingToken);
        var listenTask = ListenForUpdates(stoppingToken);
        var flushTask = FlushBufferLoopAsync(stoppingToken);
        var heartbeatTask = HeartbeatLoopAsync(stoppingToken);

        await Task.WhenAll(maintenanceTask, listenTask, flushTask, heartbeatTask);
    }

    private async Task ListenForUpdates(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var conn = new NpgsqlConnection(_connectionString);
                await conn.OpenAsync(stoppingToken);
                
                using (var cmd = new NpgsqlCommand("LISTEN dash_updates", conn))
                {
                    await cmd.ExecuteNonQueryAsync(stoppingToken);
                }
                
                _logger.LogInformation("PG_LISTEN active (dash_updates)");
                
                // Revival Logic: Mark for full sync on next flush
                _isRevivalPending = true;

                conn.Notification += (o, e) => _pulseBuffer.TryAdd(e.Payload, e.Payload);

                while (!stoppingToken.IsCancellationRequested) 
                    await conn.WaitAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogWarning("PG_LISTEN connection lost. Retrying in 5s... {Msg}", ex.Message);
                await Task.Delay(5000, stoppingToken);
            }
        }
    }

    private async Task FlushBufferLoopAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            await Task.Delay(_adaptiveDebounceMs, stoppingToken);

            if (_pulseBuffer.IsEmpty && !_isRevivalPending) continue;

            // Extract and Clear Buffer
            var payloads = _pulseBuffer.Keys.ToList();
            _pulseBuffer.Clear();

            try
            {
                await ProcessCalescedPayloads(payloads, stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Pulse Flush Failed!");
            }
        }
    }

    private async Task ProcessCalescedPayloads(List<string> payloads, CancellationToken ct)
    {
        using var scope = _serviceProvider.CreateScope();
        var repo = scope.ServiceProvider.GetRequiredService<IDashboardRepository>();
        var svc = scope.ServiceProvider.GetRequiredService<IDashboardUpdateService>();
        var monitor = scope.ServiceProvider.GetRequiredService<Dashboard.BLL.Utilities.IResourceMonitor>();
        var (cpu, ram, db) = await monitor.GetMetricsAsync();
        
        // ADAPTIVE DEBOUNCE: Protect hub from storm during high CPU
        _adaptiveDebounceMs = cpu > 80 ? _debounceMs * 3 : (cpu > 50 ? _debounceMs * 2 : _debounceMs);

        var today = DateTime.UtcNow.Date;

        // Group by unique (FY, DDO, User) to fetch only once per cycle
        var targets = payloads.Select(p => p.Split(':'))
            .Where(p => p.Length >= 6)
            .GroupBy(p => new { FY = int.Parse(p[0]), DDO = p[2], User = p[4] })
            .ToList();

        foreach (var target in targets)
        {
            // SURGICAL PK SNAPSHOT: Ultra-fast row lookup for Today
            var (adminM, appM, opM) = await repo.GetSurgicalSnapshotAsync(target.Key.FY, target.Key.DDO, target.Key.User, today);

            // GHOSTING STAGGER: Small jitter to prevent SignalR storm
            await Task.Delay(new Random().Next(0, _jitterMs), ct);

            // Broadcast to Surgical Groups (Simplified Flattened Target)
            var ev = target.Last()[^1]; 
            
            await svc.PushPulseAsync("Dashboard:Admin", "DashboardUpdate", MapSurgicalPulse("Admin", ev, adminM));
            await svc.PushPulseAsync($"Dashboard:DDO:{target.Key.DDO}", "DashboardUpdate", MapSurgicalPulse("Approver", ev, appM));
            await svc.PushPulseAsync($"Dashboard:DDO:{target.Key.DDO}:OP:{target.Key.User}", "DashboardUpdate", MapSurgicalPulse("Operator", ev, opM));

            // Target: SystemPressure (Exclusive to Admin and Approver DDO)
            if (ev == "BILL_GEN" || ev == "FTO_RCVD" || ev == "BILL_FWD_APP" || ev == "BILL_FWD_TRZ")
            {
                int load = (cpu + (db * 5)) / 2; // Synthetic load indicator
                var pressurePulse = MapSurgicalPulse("Admin", ev, adminM, load, cpu, ram, db);
                
                await svc.PushPulseAsync("Pressure:Admin", "SystemPressure", pressurePulse);
                
                // Office-wide visibility for DDO staff (Approver and Operators)
                var ddoGroup = $"Pressure:DDO:{target.Key.DDO}";
                await svc.PushPulseAsync(ddoGroup, "SystemPressure", MapSurgicalPulse("Approver", ev, appM, load));
                await svc.PushPulseAsync(ddoGroup, "SystemPressure", MapSurgicalPulse(target.Key.User, ev, opM, load));
            }
        }

        _isRevivalPending = false;
    }

    private object MapSurgicalPulse(string scope, string ev, DashboardMetrics m, int? load = null, int? cpu = null, int? ram = null, int? db = null)
    {
        if (load.HasValue)
        {
            var p = new PressurePulseMetrics { sl = load.Value, sc = scope, c = cpu, m = ram, d = db };
            switch (ev)
            {
                case "FTO_RCVD": p.rf = m.ReceivedFto; break;
                case "BILL_GEN": p.pf = m.ProcessedFto; p.gb = m.GeneratedBills; break;
                case "BILL_FWD_APP": p.ar = m.ReceivedByApprover; break;
                case "BILL_REJ": p.rb = m.RejectedByApprover; break;
                case "BILL_FWD_TRZ": p.ft = m.ForwardedToTreasury; break;
            }
            return p;
        }
        else
        {
            var p = new BasePulseMetrics();
            switch (ev)
            {
                case "FTO_RCVD": p.rf = m.ReceivedFto; break;
                case "BILL_GEN": p.pf = m.ProcessedFto; p.gb = m.GeneratedBills; break;
                case "BILL_FWD_APP": p.ar = m.ReceivedByApprover; break;
                case "BILL_REJ": p.rb = m.RejectedByApprover; break;
                case "BILL_FWD_TRZ": p.ft = m.ForwardedToTreasury; break;
            }
            return p;
        }
    }

    private async Task PeriodicMaintenanceAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            await CheckMidnightHardeningAsync();
            await Task.Delay(TimeSpan.FromHours(1), stoppingToken);
        }
    }

    private async Task CheckMidnightHardeningAsync()
    {
        using var scope = _serviceProvider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<Dashboard.DAL.AppDbContext>();
        try 
        {
            var metaResult = await db.ScalarResults.FromSqlInterpolated($"SELECT last_processed_date::text AS value FROM dashboard.sync_metadata LIMIT 1").FirstOrDefaultAsync();
            if (metaResult == null) return;
            DateTime lastH = DateTime.Parse(metaResult.Value ?? "2000-01-01");
            if (lastH < DateTime.UtcNow.Date.AddDays(-1)) 
            {
                await db.Database.ExecuteSqlInterpolatedAsync($"CALL dashboard.sp_harden_summary_tables()");
                
                // Auto-Partitioning: Ensure partitions for current and next FY exist
                int currentFy = 2000 + (DateTime.UtcNow.Year % 100);
                if (DateTime.UtcNow.Month >= 4) currentFy = (currentFy * 100) + (currentFy + 1);
                else currentFy = ((currentFy - 1) * 100) + currentFy;
                
                int nextFy = ((currentFy % 100) * 100) + ((currentFy % 100) + 1);
                
                await db.Database.ExecuteSqlInterpolatedAsync($"SELECT dashboard.fn_create_ledger_partitions({currentFy})");
                await db.Database.ExecuteSqlInterpolatedAsync($"SELECT dashboard.fn_create_ledger_partitions({nextFy})");
            }
        }
        catch (Exception ex) { _logger.LogError(ex, "Infrastructure Check Failed!"); }
    }
    private async Task HeartbeatLoopAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            await Task.Delay(3000, stoppingToken);

            using var scope = _serviceProvider.CreateScope();
            var monitor = scope.ServiceProvider.GetRequiredService<Dashboard.BLL.Utilities.IResourceMonitor>();
            var svc = scope.ServiceProvider.GetRequiredService<IDashboardUpdateService>();
            
            try
            {
                var (cpu, ram, db) = await monitor.GetMetricsAsync();
                int load = (cpu + (db * 5)) / 2;

                var pulse = new PressurePulseMetrics 
                { 
                    sc = "Admin", 
                    sl = load, 
                    c = cpu, 
                    m = ram, 
                    d = db 
                };

                await svc.PushPulseAsync("Pressure:Admin", "SystemPressure", pulse);
                
                // Broadcast resources to DDO offices (POC: DDO001)
                pulse.sc = "Approver";
                await svc.PushPulseAsync("Pressure:DDO:DDO001", "SystemPressure", pulse);
            }
            catch (Exception ex)
            {
                _logger.LogWarning("Heartbeat Failed: {Msg}", ex.Message);
            }
        }
    }
}

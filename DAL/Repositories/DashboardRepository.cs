using Dashboard.DAL.Models;
using Microsoft.EntityFrameworkCore;

namespace Dashboard.DAL.Repositories;

public interface IDashboardRepository
{
    Task<DashboardMetrics> GetAdminMetricsAsync(int fy, DateTime start, DateTime end);
    Task<DashboardMetrics> GetApproverMetricsAsync(int fy, string ddoCode, DateTime start, DateTime end);
    Task<DashboardMetrics> GetOperatorMetricsAsync(int fy, string ddoCode, string userId, DateTime start, DateTime end);
    Task<(DashboardMetrics Admin, DashboardMetrics App, DashboardMetrics Op)> GetSurgicalSnapshotAsync(int fy, string ddoCode, string userId, DateTime date);
    Task<string> GetDashboardStatusAsync();
    Task<IEnumerable<DashboardMetrics>> GetComparisonMetricsAsync(int fy, string ddoCode, string userId, DateTime start, DateTime end);
    Task<IEnumerable<DashboardMetrics>> GetComparisonSmartMetricsAsync(int fy, string ddoCode, string userId, string rangeType, DateTime start, DateTime end);
    Task RefreshBaselineAsync(string userId, bool isAuto);
    Task<IEnumerable<BatchDashboardMetrics>> GetBatchSurgicalSnapshotsAsync(IEnumerable<DashboardTarget> targets, DateTime date);
}

public record DashboardTarget(int FY, string DdoCode, string UserId);

public record BatchDashboardMetrics(
    DashboardTarget Target,
    DashboardMetrics Admin,
    DashboardMetrics Approver,
    DashboardMetrics Operator
);

public class DashboardRepository : IDashboardRepository
{
    private readonly AppDbContext _db;

    public DashboardRepository(AppDbContext db)
    {
        _db = db;
    }

    public async Task<string> GetDashboardStatusAsync()
    {
        var result = await _db.ScalarResults
            .FromSqlInterpolated($"SELECT dashboard.fn_get_refresh_status()::text AS value")
            .FirstOrDefaultAsync();
        return result?.Value ?? "{}";
    }

    public async Task RefreshBaselineAsync(string userId, bool isAuto)
    {
        await _db.Database.ExecuteSqlInterpolatedAsync($"CALL dashboard.sp_refresh_dashboard_baseline({userId}, {isAuto})");
    }

    public async Task<IEnumerable<DashboardMetrics>> GetComparisonMetricsAsync(int fy, string ddoCode, string userId, DateTime start, DateTime end)
    {
        var adminM = await GetAdminMetricsAsync(fy, start, end);
        adminM.Context = "Admin";
        
        var appM = await GetApproverMetricsAsync(fy, ddoCode, start, end);
        appM.Context = "Approver";
        
        var opM = await GetOperatorMetricsAsync(fy, ddoCode, userId, start, end);
        opM.Context = "Operator";
        
        return new List<DashboardMetrics> { adminM, appM, opM };
    }

    public async Task<DashboardMetrics> GetAdminMetricsAsync(int fy, DateTime start, DateTime end)
    {
        bool isFullYear = (start.Month == 4 && start.Day == 1 && end.Month == 3 && end.Day == 31);
        bool isHistoricalOnly = end < DateTime.UtcNow.Date;

        if (isFullYear)
        {
            var summary = await _db.Set<FySummaryAdmin>().AsNoTracking().FirstOrDefaultAsync(x => x.FinancialYear == fy);
            if (summary != null) return Map(summary);
        }

        return await _db.Set<DailyLedgerAdmin>().AsNoTracking()
            .Where(x => x.FinancialYear == fy && x.LedgerDate >= start.Date && x.LedgerDate <= end.Date)
            .GroupBy(x => x.FinancialYear)
            .Select(g => new DashboardMetrics {
                ReceivedFto = g.Sum(x => x.ReceivedFto),
                ProcessedFto = g.Sum(x => x.ProcessedFto),
                GeneratedBills = g.Sum(x => x.GeneratedBills),
                ForwardedToTreasury = g.Sum(x => x.ForwardedToTreasury),
                ReceivedByApprover = g.Sum(x => x.ReceivedByApprover),
                RejectedByApprover = g.Sum(x => x.RejectedByApprover)
            }).FirstOrDefaultAsync() ?? new DashboardMetrics();
    }

    public async Task<DashboardMetrics> GetApproverMetricsAsync(int fy, string ddoCode, DateTime start, DateTime end)
    {
        return await _db.Set<DailyLedgerApprover>().AsNoTracking()
            .Where(x => x.FinancialYear == fy && x.DdoCode == ddoCode && x.LedgerDate >= start.Date && x.LedgerDate <= end.Date)
            .GroupBy(x => x.DdoCode)
            .Select(g => new DashboardMetrics {
                ReceivedFto = g.Sum(x => x.ReceivedFto),
                ProcessedFto = g.Sum(x => x.ProcessedFto),
                GeneratedBills = g.Sum(x => x.GeneratedBills),
                ForwardedToTreasury = g.Sum(x => x.ForwardedToTreasury),
                ReceivedByApprover = g.Sum(x => x.ReceivedByApprover),
                RejectedByApprover = g.Sum(x => x.RejectedByApprover)
            }).FirstOrDefaultAsync() ?? new DashboardMetrics();
    }

    public async Task<DashboardMetrics> GetOperatorMetricsAsync(int fy, string ddoCode, string userId, DateTime start, DateTime end)
    {
        return await _db.Set<DailyLedgerOperator>().AsNoTracking()
            .Where(x => x.FinancialYear == fy && x.DdoCode == ddoCode && x.UserId == userId && x.LedgerDate >= start.Date && x.LedgerDate <= end.Date)
            .GroupBy(x => x.UserId)
            .Select(g => new DashboardMetrics {
                ReceivedFto = g.Sum(x => x.ReceivedFto),
                ProcessedFto = g.Sum(x => x.ProcessedFto),
                GeneratedBills = g.Sum(x => x.GeneratedBills),
                ForwardedToTreasury = g.Sum(x => x.ForwardedToTreasury),
                ReceivedByApprover = g.Sum(x => x.ReceivedByApprover),
                RejectedByApprover = g.Sum(x => x.RejectedByApprover)
            }).FirstOrDefaultAsync() ?? new DashboardMetrics();
    }

    public async Task<IEnumerable<DashboardMetrics>> GetComparisonSmartMetricsAsync(int fy, string ddoCode, string userId, string rangeType, DateTime start, DateTime end)
    {
        var today = DateTime.UtcNow.Date;

        // 1. ADMIN
        DashboardMetrics admin;
        if (rangeType == "FinancialYear")
        {
            var summary = await _db.Set<FySummaryAdmin>().AsNoTracking().FirstOrDefaultAsync(x => x.FinancialYear == fy);
            var liveToday = await _db.Set<DailyLedgerAdmin>().AsNoTracking().FirstOrDefaultAsync(x => x.FinancialYear == fy && x.LedgerDate == today);
            admin = summary != null ? Map(summary) : new DashboardMetrics();
            if (liveToday != null) Merge(admin, liveToday);
        }
        else
        {
            var query = _db.Set<DailyLedgerAdmin>().AsNoTracking()
                .Where(x => x.FinancialYear == fy && x.LedgerDate >= start.Date && x.LedgerDate <= end.Date);
            
            admin = await query.GroupBy(x => x.FinancialYear)
                .Select(g => new DashboardMetrics {
                    ReceivedFto = g.Sum(x => x.ReceivedFto),
                    ProcessedFto = g.Sum(x => x.ProcessedFto),
                    GeneratedBills = g.Sum(x => x.GeneratedBills),
                    ForwardedToTreasury = g.Sum(x => x.ForwardedToTreasury),
                    ReceivedByApprover = g.Sum(x => x.ReceivedByApprover),
                    RejectedByApprover = g.Sum(x => x.RejectedByApprover)
                }).FirstOrDefaultAsync() ?? new DashboardMetrics();

            // If today is in range, isolate today's values
            if (today >= start.Date && today <= end.Date)
            {
                var live = await _db.Set<DailyLedgerAdmin>().AsNoTracking()
                    .FirstOrDefaultAsync(x => x.FinancialYear == fy && x.LedgerDate == today);
                if (live != null)
                {
                    admin.TodayReceivedFto = live.ReceivedFto;
                    admin.TodayProcessedFto = live.ProcessedFto;
                    admin.TodayGeneratedBills = live.GeneratedBills;
                    admin.TodayForwardedToTreasury = live.ForwardedToTreasury;
                    admin.TodayReceivedByApprover = live.ReceivedByApprover;
                    admin.TodayRejectedByApprover = live.RejectedByApprover;
                }
            }
        }
        admin.Context = "Admin";

        // 2. APPROVER
        DashboardMetrics app;
        if (rangeType == "FinancialYear")
        {
            var summary = await _db.Set<FySummaryApprover>().AsNoTracking().FirstOrDefaultAsync(x => x.FinancialYear == fy && x.DdoCode == ddoCode);
            var liveToday = await _db.Set<DailyLedgerApprover>().AsNoTracking().FirstOrDefaultAsync(x => x.FinancialYear == fy && x.DdoCode == ddoCode && x.LedgerDate == today);
            app = summary != null ? Map(summary) : new DashboardMetrics();
            if (liveToday != null) Merge(app, liveToday);
        }
        else
        {
            var query = _db.Set<DailyLedgerApprover>().AsNoTracking()
                .Where(x => x.FinancialYear == fy && x.DdoCode == ddoCode && x.LedgerDate >= start.Date && x.LedgerDate <= end.Date);
                
            app = await query.GroupBy(x => x.DdoCode)
                .Select(g => new DashboardMetrics {
                    ReceivedFto = g.Sum(x => x.ReceivedFto),
                    ProcessedFto = g.Sum(x => x.ProcessedFto),
                    GeneratedBills = g.Sum(x => x.GeneratedBills),
                    ForwardedToTreasury = g.Sum(x => x.ForwardedToTreasury),
                    ReceivedByApprover = g.Sum(x => x.ReceivedByApprover),
                    RejectedByApprover = g.Sum(x => x.RejectedByApprover)
                }).FirstOrDefaultAsync() ?? new DashboardMetrics();

            if (today >= start.Date && today <= end.Date)
            {
                var live = await _db.Set<DailyLedgerApprover>().AsNoTracking()
                    .FirstOrDefaultAsync(x => x.FinancialYear == fy && x.DdoCode == ddoCode && x.LedgerDate == today);
                if (live != null)
                {
                    app.TodayReceivedFto = live.ReceivedFto;
                    app.TodayProcessedFto = live.ProcessedFto;
                    app.TodayGeneratedBills = live.GeneratedBills;
                    app.TodayForwardedToTreasury = live.ForwardedToTreasury;
                    app.TodayReceivedByApprover = live.ReceivedByApprover;
                    app.TodayRejectedByApprover = live.RejectedByApprover;
                }
            }
        }
        app.Context = "Approver";

        // 3. OPERATOR
        DashboardMetrics op;
        if (rangeType == "FinancialYear")
        {
            var summary = await _db.Set<FySummaryOperator>().AsNoTracking().FirstOrDefaultAsync(x => x.FinancialYear == fy && x.DdoCode == ddoCode && x.UserId == userId);
            var liveToday = await _db.Set<DailyLedgerOperator>().AsNoTracking().FirstOrDefaultAsync(x => x.FinancialYear == fy && x.DdoCode == ddoCode && x.UserId == userId && x.LedgerDate == today);
            op = summary != null ? Map(summary) : new DashboardMetrics();
            if (liveToday != null) Merge(op, liveToday);
        }
        else
        {
            var query = _db.Set<DailyLedgerOperator>().AsNoTracking()
                .Where(x => x.FinancialYear == fy && x.DdoCode == ddoCode && x.UserId == userId && x.LedgerDate >= start.Date && x.LedgerDate <= end.Date);

            op = await query.GroupBy(x => x.UserId)
                .Select(g => new DashboardMetrics {
                    ReceivedFto = g.Sum(x => x.ReceivedFto),
                    ProcessedFto = g.Sum(x => x.ProcessedFto),
                    GeneratedBills = g.Sum(x => x.GeneratedBills),
                    ForwardedToTreasury = g.Sum(x => x.ForwardedToTreasury),
                    ReceivedByApprover = g.Sum(x => x.ReceivedByApprover),
                    RejectedByApprover = g.Sum(x => x.RejectedByApprover)
                }).FirstOrDefaultAsync() ?? new DashboardMetrics();

            if (today >= start.Date && today <= end.Date)
            {
                var live = await _db.Set<DailyLedgerOperator>().AsNoTracking()
                    .FirstOrDefaultAsync(x => x.FinancialYear == fy && x.DdoCode == ddoCode && x.UserId == userId && x.LedgerDate == today);
                if (live != null)
                {
                    op.TodayReceivedFto = live.ReceivedFto;
                    op.TodayProcessedFto = live.ProcessedFto;
                    op.TodayGeneratedBills = live.GeneratedBills;
                    op.TodayForwardedToTreasury = live.ForwardedToTreasury;
                    op.TodayReceivedByApprover = live.ReceivedByApprover;
                    op.TodayRejectedByApprover = live.RejectedByApprover;
                }
            }
        }
        op.Context = "Operator";

        return new List<DashboardMetrics> { admin, app, op };
    }

    private void Merge(DashboardMetrics m, DailyLedgerBase live)
    {
        m.ReceivedFto += live.ReceivedFto;
        m.ProcessedFto += live.ProcessedFto;
        m.GeneratedBills += live.GeneratedBills;
        m.ForwardedToTreasury += live.ForwardedToTreasury;
        m.ReceivedByApprover += live.ReceivedByApprover;
        m.RejectedByApprover += live.RejectedByApprover;

        // Capture the "Today" portion separately
        m.TodayReceivedFto = live.ReceivedFto;
        m.TodayProcessedFto = live.ProcessedFto;
        m.TodayGeneratedBills = live.GeneratedBills;
        m.TodayForwardedToTreasury = live.ForwardedToTreasury;
        m.TodayReceivedByApprover = live.ReceivedByApprover;
        m.TodayRejectedByApprover = live.RejectedByApprover;
    }

    public async Task<(DashboardMetrics Admin, DashboardMetrics App, DashboardMetrics Op)> GetSurgicalSnapshotAsync(int fy, string ddoCode, string userId, DateTime date)
    {
        // SURGICAL PK LOOKUP: Optimized for high-density pulsing
        var admin = await _db.Set<DailyLedgerAdmin>().AsNoTracking().FirstOrDefaultAsync(x => x.FinancialYear == fy && x.LedgerDate == date.Date);
        var app = await _db.Set<DailyLedgerApprover>().AsNoTracking().FirstOrDefaultAsync(x => x.FinancialYear == fy && x.DdoCode == ddoCode && x.LedgerDate == date.Date);
        var op = await _db.Set<DailyLedgerOperator>().AsNoTracking().FirstOrDefaultAsync(x => x.FinancialYear == fy && x.DdoCode == ddoCode && x.UserId == userId && x.LedgerDate == date.Date);

        return (
            admin != null ? Map(admin) : new DashboardMetrics(),
            app != null ? Map(app) : new DashboardMetrics(),
            op != null ? Map(op) : new DashboardMetrics()
        );
    }

    public async Task<IEnumerable<BatchDashboardMetrics>> GetBatchSurgicalSnapshotsAsync(IEnumerable<DashboardTarget> targets, DateTime date)
    {
        var distinctFys = targets.Select(t => t.FY).Distinct().ToList();
        var distinctDdos = targets.Select(t => t.DdoCode).Distinct().ToList();
        var distinctUsers = targets.Select(t => t.UserId).Distinct().ToList();

        var admins = await _db.Set<DailyLedgerAdmin>().AsNoTracking()
            .Where(x => x.LedgerDate == date.Date && distinctFys.Contains(x.FinancialYear))
            .ToListAsync();

        var approvers = await _db.Set<DailyLedgerApprover>().AsNoTracking()
            .Where(x => x.LedgerDate == date.Date && distinctDdos.Contains(x.DdoCode) && distinctFys.Contains(x.FinancialYear))
            .ToListAsync();

        var operators = await _db.Set<DailyLedgerOperator>().AsNoTracking()
            .Where(x => x.LedgerDate == date.Date && distinctUsers.Contains(x.UserId) && distinctDdos.Contains(x.DdoCode) && distinctFys.Contains(x.FinancialYear))
            .ToListAsync();

        var result = new List<BatchDashboardMetrics>();
        foreach (var target in targets)
        {
            var admin = admins.FirstOrDefault(x => x.FinancialYear == target.FY);
            var app = approvers.FirstOrDefault(x => x.FinancialYear == target.FY && x.DdoCode == target.DdoCode);
            var op = operators.FirstOrDefault(x => x.FinancialYear == target.FY && x.DdoCode == target.DdoCode && x.UserId == target.UserId);

            result.Add(new BatchDashboardMetrics(
                target,
                admin != null ? Map(admin) : new DashboardMetrics(),
                app != null ? Map(app) : new DashboardMetrics(),
                op != null ? Map(op) : new DashboardMetrics()
            ));
        }

        return result;
    }

    private DashboardMetrics Map(DailyLedgerBase s) => new() {
        ReceivedFto = s.ReceivedFto, ProcessedFto = s.ProcessedFto, GeneratedBills = s.GeneratedBills,
        ForwardedToTreasury = s.ForwardedToTreasury, ReceivedByApprover = s.ReceivedByApprover, RejectedByApprover = s.RejectedByApprover,
        Context = s is DailyLedgerAdmin ? "Admin" : s is DailyLedgerApprover ? "Approver" : "Operator"
    };

    private DashboardMetrics Map(FySummaryAdmin s) => new() {
        ReceivedFto = s.ReceivedFto, ProcessedFto = s.ProcessedFto, GeneratedBills = s.GeneratedBills,
        ForwardedToTreasury = s.ForwardedToTreasury, ReceivedByApprover = s.ReceivedByApprover, RejectedByApprover = s.RejectedByApprover,
        Context = "Admin"
    };

    private DashboardMetrics Map(FySummaryApprover s) => new() {
        ReceivedFto = s.ReceivedFto, ProcessedFto = s.ProcessedFto, GeneratedBills = s.GeneratedBills,
        ForwardedToTreasury = s.ForwardedToTreasury, ReceivedByApprover = s.ReceivedByApprover, RejectedByApprover = s.RejectedByApprover,
        Context = "Approver"
    };

    private DashboardMetrics Map(FySummaryOperator s) => new() {
        ReceivedFto = s.ReceivedFto, ProcessedFto = s.ProcessedFto, GeneratedBills = s.GeneratedBills,
        ForwardedToTreasury = s.ForwardedToTreasury, ReceivedByApprover = s.ReceivedByApprover, RejectedByApprover = s.RejectedByApprover,
        Context = "Operator"
    };
}

public class DashboardMetrics
{
    public int ReceivedFto { get; set; }
    public int ProcessedFto { get; set; }
    public int GeneratedBills { get; set; }
    public int ForwardedToTreasury { get; set; }
    public int ReceivedByApprover { get; set; }
    public int RejectedByApprover { get; set; }
    public string Context { get; set; } = "";
    
    // Today's contribution (used for real-time additive merge in frontend)
    public int TodayReceivedFto { get; set; }
    public int TodayProcessedFto { get; set; }
    public int TodayGeneratedBills { get; set; }
    public int TodayForwardedToTreasury { get; set; }
    public int TodayReceivedByApprover { get; set; }
    public int TodayRejectedByApprover { get; set; }
}

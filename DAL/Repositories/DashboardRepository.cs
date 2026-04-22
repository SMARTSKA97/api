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
    Task RefreshBaselineAsync(string userId, bool isAuto);
}

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

        if (isFullYear || isHistoricalOnly)
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
        bool isHistoricalOnly = end < DateTime.UtcNow.Date;
        if (isHistoricalOnly)
        {
            var summary = await _db.Set<FySummaryApprover>().AsNoTracking().FirstOrDefaultAsync(x => x.FinancialYear == fy && x.DdoCode == ddoCode);
            if (summary != null) return Map(summary);
        }

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
        bool isHistoricalOnly = end < DateTime.UtcNow.Date;
        if (isHistoricalOnly)
        {
            var summary = await _db.Set<FySummaryOperator>().AsNoTracking().FirstOrDefaultAsync(x => x.FinancialYear == fy && x.DdoCode == ddoCode && x.UserId == userId);
            if (summary != null) return Map(summary);
        }

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
}

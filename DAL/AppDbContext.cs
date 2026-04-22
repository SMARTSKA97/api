using Microsoft.EntityFrameworkCore;
using Dashboard.DAL.Models;

namespace Dashboard.DAL;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    public DbSet<Ddo> Ddos { get; set; }
    public DbSet<User> Users { get; set; }
    public DbSet<FtoList> Ftos { get; set; }
    public DbSet<BillList> Bills { get; set; }
    public DbSet<BillStatusLog> BillStatusLogs { get; set; }
    public DbSet<DailyLedgerAdmin> DailyLedgerAdmin { get; set; }
    public DbSet<DailyLedgerApprover> DailyLedgerApprover { get; set; }
    public DbSet<DailyLedgerOperator> DailyLedgerOperator { get; set; }
    
    // Hardened Summary Models (Keyless Entities)
    public DbSet<FySummaryAdmin> FySummaryAdmin { get; set; }
    public DbSet<FySummaryApprover> FySummaryApprover { get; set; }
    public DbSet<FySummaryOperator> FySummaryOperator { get; set; }
    
    // Type-Safe Scalar Capture (Keyless)
    public DbSet<ScalarRawResult> ScalarResults { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<FtoList>().HasKey(f => new { f.FtoNo, f.FinancialYear });
        
        modelBuilder.Entity<DailyLedgerAdmin>().ToTable("daily_ledger_admin", "dashboard").HasKey(x => new { x.FinancialYear, x.LedgerDate });
        modelBuilder.Entity<DailyLedgerApprover>().ToTable("daily_ledger_approver", "dashboard").HasKey(x => new { x.FinancialYear, x.DdoCode, x.LedgerDate });
        modelBuilder.Entity<DailyLedgerOperator>().ToTable("daily_ledger_operator", "dashboard").HasKey(x => new { x.FinancialYear, x.DdoCode, x.UserId, x.LedgerDate });

        // Hardened Table Mapping (Keyless for summary views)
        modelBuilder.Entity<FySummaryAdmin>(eb => { eb.HasNoKey(); eb.ToTable("fy_summary_admin", "dashboard"); });
        modelBuilder.Entity<FySummaryApprover>(eb => { eb.HasNoKey(); eb.ToTable("fy_summary_approver", "dashboard"); });
        modelBuilder.Entity<FySummaryOperator>(eb => { eb.HasNoKey(); eb.ToTable("fy_summary_operator", "dashboard"); });

        modelBuilder.Entity<ScalarRawResult>(eb => { eb.HasNoKey(); });
    }
}

public class ScalarRawResult
{
    public string? Value { get; set; }
}

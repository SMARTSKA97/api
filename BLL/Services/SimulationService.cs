using Dashboard.DAL;
using Microsoft.EntityFrameworkCore;

namespace Dashboard.BLL.Services;

public class SimulationService : ISimulationService
{
    private readonly AppDbContext _context;
    private readonly IFiscalYearUtility _fyUtility;
    private readonly Random _random = new();

    public SimulationService(AppDbContext context, IFiscalYearUtility fyUtility)
    {
        _context = context;
        _fyUtility = fyUtility;
    }

    /**
     * Type-Safe Simulation: Seed FTOs
     * Uses ExecuteSqlInterpolatedAsync to ensure parameterization and prevent SQL injection.
     */
    public async Task SeedRandomFtosAsync(int count)
    {
        var activeFy = _fyUtility.CalculateCurrentFY();
        var users = await _context.Users.Where(u => u.Role == "Operator").ToListAsync();
        if (!users.Any()) return;

        for (int i = 0; i < count; i++)
        {
            var user = users[_random.Next(users.Count)];
            var ftoNo = $"SIM_{Guid.NewGuid().ToString().Substring(0, 8).ToUpper()}";
            var amount = (decimal)(_random.NextDouble() * 10000 + 100);
            
            await _context.Database.ExecuteSqlInterpolatedAsync(
                $"CALL fto.sp_create_fto({ftoNo}, {amount}, {user.UserId}, {user.DdoCode}, {activeFy})");
        }
    }

    public async Task AutoGenerateBillsAsync()
    {
        var pendingFtos = await _context.Ftos
            .Where(f => f.FtoStatus == 0)
            .GroupBy(f => new { f.UserId, f.DdoCode, f.FinancialYear })
            .Select(g => new { g.Key.UserId, g.Key.DdoCode, g.Key.FinancialYear, Ftos = g.Select(x => x.FtoNo).ToList() })
            .ToListAsync();

        if (!pendingFtos.Any()) return;

        var target = pendingFtos[_random.Next(pendingFtos.Count)];
        var count = _random.Next(1, Math.Min(target.Ftos.Count, 6));
        var subset = target.Ftos.Take(count).ToArray();

        await _context.Database.ExecuteSqlInterpolatedAsync(
            $"CALL bills.sp_generate_bill({subset}, {target.UserId}, {target.DdoCode}, {"Operator"}, {target.FinancialYear})");
    }

    public async Task AutoForwardBillsAsync()
    {
        var bills = await _context.Bills.Where(b => b.BillStatus == 1).Take(5).ToListAsync();

        foreach (var bill in bills)
        {
            await _context.Database.ExecuteSqlInterpolatedAsync(
                $"CALL bills.sp_forward_bill({bill.BillNo}, {bill.UserId}, {"Operator"})");
        }
    }

    public async Task AutoFinalizeBillsAsync()
    {
        var bills = await _context.Bills.Where(b => (b.BillStatus == 0 || b.BillStatus == 2)).Take(5).ToListAsync();

        foreach (var bill in bills)
        {
            if (_random.NextDouble() > 0.2)
            {
                await _context.Database.ExecuteSqlInterpolatedAsync(
                    $"CALL bills.sp_forward_bill({bill.BillNo}, 'DDO001_APPROVER', 'Approver')");
            }
            else
            {
                await _context.Database.ExecuteSqlInterpolatedAsync(
                    $"CALL bills.sp_reject_bill({bill.BillNo}, 'DDO001_APPROVER')");
            }
        }
    }

    public async Task RunCycleAsync()
    {
        await SeedRandomFtosAsync(_random.Next(3, 8));
        if (_random.NextDouble() > 0.3) await AutoGenerateBillsAsync();
        if (_random.NextDouble() > 0.4) await AutoForwardBillsAsync();
        if (_random.NextDouble() > 0.5) await AutoFinalizeBillsAsync();
    }
}

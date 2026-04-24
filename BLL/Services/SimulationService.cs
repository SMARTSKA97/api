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
        // 1. FTO Creation (Operators + Approver)
        var operators = await _context.Users.Where(u => u.Role == "Operator").ToListAsync();
        var approvers = await _context.Users.Where(u => u.Role == "Approver").ToListAsync();
        
        // Random Operators create FTOs
        foreach (var op in operators)
        {
            if (_random.NextDouble() > 0.3) await SeedRandomFtosAsync(_random.Next(5, 15));
        }
        
        // Approver also creates FTOs
        foreach (var app in approvers)
        {
            if (_random.NextDouble() > 0.5) await SeedRandomFtosAsync(_random.Next(2, 6));
        }

        // 2. Operator Processes FTOs to Bills and Forwards
        foreach (var op in operators)
        {
            // Create Bills (Status 1)
            var pendingFtos = await _context.Ftos
                .Where(f => f.UserId == op.UserId && f.FtoStatus == 0)
                .Select(f => f.FtoNo)
                .ToListAsync();
            
            if (pendingFtos.Any())
            {
                var subset = pendingFtos.Take(_random.Next(1, pendingFtos.Count)).ToArray();
                await _context.Database.ExecuteSqlInterpolatedAsync(
                    $"CALL bills.sp_generate_bill({subset}, {op.UserId}, {op.DdoCode}, {"Operator"}, {_fyUtility.CalculateCurrentFY()})");
            }

            // Forward Bills to Approver (Status 1 -> 2)
            var billsToForward = await _context.Bills
                .Where(b => b.UserId == op.UserId && b.BillStatus == 1)
                .Select(b => b.BillNo)
                .ToListAsync();
            
            foreach (var billNo in billsToForward)
            {
                await _context.Database.ExecuteSqlInterpolatedAsync(
                    $"CALL bills.sp_forward_bill({billNo}, {op.UserId}, {"Operator"})");
            }
        }

        // 3. Approver Processes own FTOs and handles Operator Bills
        foreach (var app in approvers)
        {
            // Process own FTOs (Status 0)
            var appFtos = await _context.Ftos
                .Where(f => f.UserId == app.UserId && f.FtoStatus == 0)
                .Select(f => f.FtoNo)
                .ToListAsync();
            
            if (appFtos.Any())
            {
                var subset = appFtos.Take(_random.Next(1, appFtos.Count)).ToArray();
                await _context.Database.ExecuteSqlInterpolatedAsync(
                    $"CALL bills.sp_generate_bill({subset}, {app.UserId}, {app.DdoCode}, {"Approver"}, {_fyUtility.CalculateCurrentFY()})");
            }

            // Handle Bills (Reject or Forward to Treasury)
            var incomingBills = await _context.Bills
                .Where(b => b.DdoCode == app.DdoCode && (b.BillStatus == 2 || b.BillStatus == 0))
                .Select(b => b.BillNo)
                .ToListAsync();
            
            foreach (var billNo in incomingBills)
            {
                if (_random.NextDouble() > 0.15) // 85% Forward
                {
                    await _context.Database.ExecuteSqlInterpolatedAsync(
                        $"CALL bills.sp_forward_bill({billNo}, {app.UserId}, {"Approver"})");
                }
                else // 15% Reject
                {
                    await _context.Database.ExecuteSqlInterpolatedAsync(
                        $"CALL bills.sp_reject_bill({billNo}, {app.UserId})");
                }
            }
        }
    }
}

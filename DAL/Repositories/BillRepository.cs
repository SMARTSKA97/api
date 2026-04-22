using Dashboard.DAL.Models;
using Microsoft.EntityFrameworkCore;

namespace Dashboard.DAL.Repositories;

public interface IBillRepository
{
    Task<IEnumerable<BillList>> GetBillsAsync(string role, string ddoCode, string userid, int? status, int page, int pageSize);
    Task<int> GetBillCountAsync(string role, string ddoCode, string userid, int? status);
}

public class BillRepository : IBillRepository
{
    private readonly AppDbContext _context;

    public BillRepository(AppDbContext context)
    {
        _context = context;
    }

    public async Task<IEnumerable<BillList>> GetBillsAsync(string role, string ddoCode, string userid, int? status, int page, int pageSize)
    {
        var query = _context.Bills.AsNoTracking();

        // Strict Role-Based Visibility
        if (role == "Approver")
        {
            query = query.Where(b => b.DdoCode == ddoCode);
        }
        else if (role == "Operator")
        {
            query = query.Where(b => b.DdoCode == ddoCode && b.UserId == userid);
        }

        // Apply Status Filter
        if (status.HasValue)
        {
            query = query.Where(b => b.BillStatus == status.Value);
        }

        return await query
            .OrderByDescending(b => b.BillDate)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();
    }

    public async Task<int> GetBillCountAsync(string role, string ddoCode, string userid, int? status)
    {
        var query = _context.Bills.AsNoTracking();

        if (role == "Approver")
        {
            query = query.Where(b => b.DdoCode == ddoCode);
        }
        else if (role == "Operator")
        {
            query = query.Where(b => b.DdoCode == ddoCode && b.UserId == userid);
        }

        if (status.HasValue)
        {
            query = query.Where(b => b.BillStatus == status.Value);
        }

        return await query.CountAsync();
    }
}

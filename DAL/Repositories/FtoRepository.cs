using Dashboard.DAL.Models;
using Microsoft.EntityFrameworkCore;

namespace Dashboard.DAL.Repositories;

public interface IFtoRepository
{
    Task<IEnumerable<FtoList>> GetFtosAsync(string role, string ddoCode, string userid, int page, int pageSize);
    Task<int> GetFtoCountAsync(string role, string ddoCode, string userid);
}

public class FtoRepository : IFtoRepository
{
    private readonly AppDbContext _context;

    public FtoRepository(AppDbContext context)
    {
        _context = context;
    }

    public async Task<IEnumerable<FtoList>> GetFtosAsync(string role, string ddoCode, string userid, int page, int pageSize)
    {
        var query = _context.Ftos.AsNoTracking();

        // Strict Role-Based Visibility
        if (role == "Approver")
        {
            query = query.Where(f => f.DdoCode == ddoCode);
        }
        else if (role == "Operator")
        {
            query = query.Where(f => f.DdoCode == ddoCode && f.UserId == userid);
        }
        // Admin sees everything if needed, but usually Admin sees dashboard.

        return await query
            .OrderByDescending(f => f.FtoCreationDate)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();
    }

    public async Task<int> GetFtoCountAsync(string role, string ddoCode, string userid)
    {
        var query = _context.Ftos.AsNoTracking();

        if (role == "Approver")
        {
            query = query.Where(f => f.DdoCode == ddoCode);
        }
        else if (role == "Operator")
        {
            query = query.Where(f => f.DdoCode == ddoCode && f.UserId == userid);
        }

        return await query.CountAsync();
    }
}

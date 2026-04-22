using Dashboard.DAL;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using System.Data;

namespace Dashboard.BLL.Services;

public interface IWorkflowService
{
    Task GenerateBillAsync(string[] ftoNos, string userid, string ddoCode, string role, int fy);
    Task ForwardBillAsync(Guid billNo, string userid, string role);
    Task RejectBillAsync(Guid billNo, string userid);
}

public class WorkflowService : IWorkflowService
{
    private readonly AppDbContext _context;

    public WorkflowService(AppDbContext context)
    {
        _context = context;
    }

    public async Task GenerateBillAsync(string[] ftoNos, string userid, string ddoCode, string role, int fy)
    {
        await _context.Database.ExecuteSqlRawAsync(
            "CALL bills.sp_generate_bill({0}, {1}, {2}, {3}, {4})",
            ftoNos, userid, ddoCode, role, fy);
    }

    public async Task ForwardBillAsync(Guid billNo, string userid, string role)
    {
        await _context.Database.ExecuteSqlRawAsync(
            "CALL bills.sp_forward_bill({0}, {1}, {2})",
            billNo, userid, role);
    }

    public async Task RejectBillAsync(Guid billNo, string userid)
    {
        await _context.Database.ExecuteSqlRawAsync(
            "CALL bills.sp_reject_bill({0}, {1})",
            billNo, userid);
    }
}

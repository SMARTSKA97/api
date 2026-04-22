using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Dashboard.DAL.Repositories;
using Dashboard.BLL.Services;

namespace Dashboard.PL.Controllers;

[Authorize]
[ApiController]
[Route("api/[controller]")]
public class BillController : ControllerBase
{
    private readonly IBillRepository _billRepo;
    private readonly IWorkflowService _workflow;

    public BillController(IBillRepository billRepo, IWorkflowService workflow)
    {
        _billRepo = billRepo;
        _workflow = workflow;
    }

    [HttpGet]
    public async Task<IActionResult> GetBills([FromQuery] int? status, [FromQuery] int page = 1, [FromQuery] int pageSize = 10)
    {
        var role = User.FindFirst(ClaimTypes.Role)?.Value;
        var ddoCode = User.FindFirst("ddoCode")?.Value;
        var userid = User.FindFirst(ClaimTypes.Name)?.Value;

        if (string.IsNullOrEmpty(role))
            return Unauthorized();

        if (role != "Admin" && string.IsNullOrEmpty(ddoCode))
            return Unauthorized("DDO Code required for non-admin");

        var bills = await _billRepo.GetBillsAsync(role, ddoCode ?? "", userid!, status, page, pageSize);
        var total = await _billRepo.GetBillCountAsync(role, ddoCode ?? "", userid!, status);

        return Ok(new { Items = bills, Total = total });
    }

    [HttpPost("generate")]
    public async Task<IActionResult> GenerateBill([FromBody] GenerateBillRequest request)
    {
        var role = User.FindFirst(ClaimTypes.Role)?.Value;
        var ddoCode = User.FindFirst("ddoCode")?.Value;
        var userid = User.FindFirst(ClaimTypes.Name)?.Value;

        if (string.IsNullOrEmpty(role) || string.IsNullOrEmpty(ddoCode))
            return Unauthorized();

        await _workflow.GenerateBillAsync(request.FtoNos, userid!, ddoCode, role, request.FinancialYear);
        return Ok(new { Message = "Bill generated successfully" });
    }

    [HttpPost("forward/{billNo}")]
    public async Task<IActionResult> ForwardBill(Guid billNo)
    {
        var role = User.FindFirst(ClaimTypes.Role)?.Value;
        var userid = User.FindFirst(ClaimTypes.Name)?.Value;

        if (string.IsNullOrEmpty(role))
            return Unauthorized();

        await _workflow.ForwardBillAsync(billNo, userid!, role);
        return Ok(new { Message = "Bill forwarded successfully" });
    }

    [HttpPost("reject/{billNo}")]
    public async Task<IActionResult> RejectBill(Guid billNo)
    {
        var role = User.FindFirst(ClaimTypes.Role)?.Value;
        var userid = User.FindFirst(ClaimTypes.Name)?.Value;

        if (role != "Approver")
            return Forbid("Only Approvers can reject bills");

        await _workflow.RejectBillAsync(billNo, userid!);
        return Ok(new { Message = "Bill rejected successfully" });
    }

    public class GenerateBillRequest
    {
        public string[] FtoNos { get; set; } = null!;
        public int FinancialYear { get; set; }
    }
}

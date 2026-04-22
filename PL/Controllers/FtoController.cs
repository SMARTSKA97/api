using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Dashboard.DAL.Repositories;

namespace Dashboard.PL.Controllers;

[Authorize]
[ApiController]
[Route("api/[controller]")]
public class FtoController : ControllerBase
{
    private readonly IFtoRepository _ftoRepo;

    public FtoController(IFtoRepository ftoRepo)
    {
        _ftoRepo = ftoRepo;
    }

    [HttpGet]
    public async Task<IActionResult> GetFtos([FromQuery] int page = 1, [FromQuery] int pageSize = 10)
    {
        var role = User.FindFirst("role")?.Value ?? User.FindFirst(ClaimTypes.Role)?.Value;
        var ddoCode = User.FindFirst("ddoCode")?.Value;
        var userid = User.FindFirst(ClaimTypes.Name)?.Value;

        if (string.IsNullOrEmpty(role))
            return Unauthorized();

        // Admin has no ddoCode restriction
        if (role != "Admin" && string.IsNullOrEmpty(ddoCode))
            return Unauthorized("DDO Code is required for non-admin users");

        var ftos = await _ftoRepo.GetFtosAsync(role, ddoCode ?? "", userid!, page, pageSize);
        var total = await _ftoRepo.GetFtoCountAsync(role, ddoCode ?? "", userid!);

        return Ok(new { Items = ftos, Total = total });
    }
}

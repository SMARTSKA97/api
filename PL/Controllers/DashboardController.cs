using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using Dashboard.BLL.Services;
using Dashboard.DAL.Repositories;

namespace Dashboard.PL.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class DashboardController : ControllerBase
{
    private readonly IDashboardService _dashService;
    private readonly IDashboardUpdateService _dashUpdateService;
    private readonly ILogger<DashboardController> _logger;

    public DashboardController(
        IDashboardService dashService, 
        IDashboardUpdateService dashUpdateService,
        ILogger<DashboardController> logger)
    {
        _dashService = dashService;
        _dashUpdateService = dashUpdateService;
        _logger = logger;
    }

    [HttpGet("status")]
    public async Task<IActionResult> GetStatus()
    {
        try 
        {
            var status = await _dashService.GetStatusAsync();
            return Ok(status);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to retrieve dashboard status Source of Truth.");
            return StatusCode(500, new { message = "Critical system status failure." });
        }
    }

    [HttpGet("metrics")]
    public async Task<IActionResult> GetMetrics([FromQuery] int fy, [FromQuery] DateTime start, [FromQuery] DateTime end)
    {
        var metrics = await _dashService.GetMetricsAsync(fy, start.ToUniversalTime(), end.ToUniversalTime());
        return Ok(metrics);
    }

    [HttpGet("comparison")]
    public async Task<IActionResult> GetComparison([FromQuery] int fy, [FromQuery] string ddoCode, [FromQuery] string userid, [FromQuery] DateTime start, [FromQuery] DateTime end)
    {
        // 404 Fix: Restoring missing endpoint after refactor
        var comparison = await _dashService.GetComparisonAsync(fy, ddoCode, userid, start.ToUniversalTime(), end.ToUniversalTime());
        return Ok(comparison);
    }

    [HttpPost("refresh-baseline")]
    [Authorize(Roles = "Admin,Approver")]
    public async Task<IActionResult> RefreshBaseline()
    {
        var userId = User.Identity?.Name ?? "UNKNOWN";
        _logger.LogInformation("User {User} requested a manual baseline refresh.", userId);

        try 
        {
            await _dashService.RefreshBaselineAsync(userId);
            return Ok(new { message = "Baseline refresh completed successfully." });
        }
        catch (Exception ex) when (ex.Message.Contains("COOLDOWN_ACTIVE"))
        {
            var parts = ex.Message.Split(':');
            var msg = parts.Length > 1 ? parts[1] : "Cooldown in progress.";
            return BadRequest(new { message = msg });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Manual refresh failed for user {User}", userId);
            return StatusCode(500, new { message = "Internal server error during refresh." });
        }
    }

    [HttpGet("metrics-gap")]
    public async Task<IActionResult> GetMetricsGap([FromQuery] string groupName, [FromQuery] long lastId, [FromQuery] long currentId)
    {
        try 
        {
            var gap = await _dashUpdateService.GetMetricsGapAsync(groupName, lastId, currentId);
            return Ok(gap);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to retrieve metrics gap for group {Group}", groupName);
            return StatusCode(500, new { message = "Failed to recover missed data." });
        }
    }
}

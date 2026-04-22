using Dashboard.BLL.Services;
using Microsoft.AspNetCore.Mvc;

namespace Dashboard.PL.Controllers;

[ApiController]
[Route("api/[controller]")]
public class SimulationController : ControllerBase
{
    private readonly ISimulationService _simulation;

    public SimulationController(ISimulationService simulation)
    {
        _simulation = simulation;
    }

    [HttpPost("seed")]
    public async Task<IActionResult> Seed([FromQuery] int count = 10)
    {
        await _simulation.SeedRandomFtosAsync(count);
        return Ok(new { Message = $"{count} FTOs generated" });
    }

    [HttpPost("auto-bill")]
    public async Task<IActionResult> AutoBill()
    {
        await _simulation.AutoGenerateBillsAsync();
        return Ok(new { Message = "Automated bill generation triggered" });
    }

    [HttpPost("run-cycle")]
    public async Task<IActionResult> RunCycle()
    {
        await _simulation.RunCycleAsync();
        return Ok(new { Message = "Simulation cycle completed" });
    }
}

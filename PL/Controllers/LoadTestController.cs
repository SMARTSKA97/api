using Dashboard.BLL.Services;
using Microsoft.AspNetCore.Mvc;

namespace Dashboard.PL.Controllers;

[ApiController]
[Route("api/[controller]")]
public class LoadTestController : ControllerBase
{
    private readonly ILoadTestService _loadTest;

    public LoadTestController(ILoadTestService loadTest)
    {
        _loadTest = loadTest;
    }

    [HttpPost("start")]
    public async Task<IActionResult> Start([FromQuery] int concurrency = 5, [FromQuery] int intensity = 100, [FromQuery] bool autoScale = false)
    {
        await _loadTest.StartAsync(concurrency, intensity, autoScale);
        return Ok(new { Message = $"Load test started with {concurrency} workers (Auto-Scale: {autoScale})" });
    }

    [HttpPost("stop")]
    public async Task<IActionResult> Stop()
    {
        await _loadTest.StopAsync();
        return Ok(new { Message = "Load test stopping..." });
    }

    [HttpGet("status")]
    public IActionResult GetStatus()
    {
        return Ok(_loadTest.GetCurrentStatus());
    }
}

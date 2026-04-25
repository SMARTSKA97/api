using Dashboard.BLL.Models;

namespace Dashboard.BLL.Services;

public interface ILoadTestService
{
    Task StartAsync(int concurrency, int intensity, bool autoScale = false);
    Task StopAsync();
    LoadTestMetrics GetCurrentStatus();
}

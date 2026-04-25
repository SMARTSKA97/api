using System.Threading.Tasks;

namespace Dashboard.BLL.Services;

public interface ISimulationService
{
    Task SeedRandomFtosAsync(int count, CancellationToken ct = default);
    Task AutoGenerateBillsAsync(CancellationToken ct = default);
    Task RunCycleAsync(CancellationToken ct = default);
}

using System.Threading.Tasks;

namespace Dashboard.BLL.Services;

public interface ISimulationService
{
    Task SeedRandomFtosAsync(int count);
    Task AutoGenerateBillsAsync();
    Task RunCycleAsync();
}

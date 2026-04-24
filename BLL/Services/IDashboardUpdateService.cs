namespace Dashboard.BLL.Services;

public interface IDashboardUpdateService
{
    Task PushPulseAsync(string groupName, string methodName, object pulse);
    Task<IEnumerable<object>> GetMetricsGapAsync(string groupName, long lastId, long currentId);
}

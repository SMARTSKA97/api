namespace Dashboard.BLL.Services;

public interface IDashboardUpdateService
{
    Task PushPulseAsync(string groupName, string methodName, object pulse);
}

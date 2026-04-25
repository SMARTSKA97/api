using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading.Tasks;
using Dashboard.DAL.Repositories;
using Microsoft.Extensions.Logging;

namespace Dashboard.BLL.Services;

public interface IDashboardService
{
    Task<object> GetStatusAsync();
    Task<IEnumerable<DashboardMetrics>> GetComparisonAsync(int fy, string ddoCode, string userid, DateTime start, DateTime end);
    Task RefreshBaselineAsync(string userid, bool isAuto = false);
    Task<DashboardMetrics> GetMetricsAsync(int fy, DateTime start, DateTime end);
    Task<IEnumerable<DashboardMetrics>> GetComparisonSmartMetricsAsync(int fy, string ddoCode, string userid, string rangeType, DateTime start, DateTime end);
}

public class DashboardService : IDashboardService
{
    private readonly IDashboardRepository _repo;
    private readonly IFiscalYearUtility _fyUtility;
    private readonly ILogger<DashboardService> _logger;

    public DashboardService(
        IDashboardRepository repo, 
        IFiscalYearUtility fyUtility,
        ILogger<DashboardService> logger)
    {
        _repo = repo;
        _fyUtility = fyUtility;
        _logger = logger;
    }

    public async Task<object> GetStatusAsync()
    {
        // 1. Fetch raw system status from Repository (DAL)
        var rawJson = await _repo.GetDashboardStatusAsync();
        _logger.LogInformation("Raw Status JSON from DB: {Raw}", rawJson);
        
        // 2. Orchestrate and Enrich with Business identity (BLL Utility)
        var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
        
        Dictionary<string, object> status;
        try 
        {
            status = JsonSerializer.Deserialize<Dictionary<string, object>>(rawJson, options) ?? new();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to deserialize DB status JSON. Falling back to empty object.");
            status = new Dictionary<string, object>();
        }
        
        var currentFy = _fyUtility.CalculateCurrentFY();
        status["current_fy"] = currentFy;
        status["fiscal_year_label"] = _fyUtility.FormatFY(currentFy);

        return status;
    }

    public async Task<IEnumerable<DashboardMetrics>> GetComparisonAsync(int fy, string ddoCode, string userid, DateTime start, DateTime end)
    {
        return await _repo.GetComparisonMetricsAsync(fy, ddoCode, userid, start, end);
    }

    public async Task RefreshBaselineAsync(string userid, bool isAuto = false)
    {
        await _repo.RefreshBaselineAsync(userid, isAuto);
    }

    public async Task<DashboardMetrics> GetMetricsAsync(int fy, DateTime start, DateTime end)
    {
        return await _repo.GetAdminMetricsAsync(fy, start, end);
    }

    public async Task<IEnumerable<DashboardMetrics>> GetComparisonSmartMetricsAsync(int fy, string ddoCode, string userid, string rangeType, DateTime start, DateTime end)
    {
        return await _repo.GetComparisonSmartMetricsAsync(fy, ddoCode, userid, rangeType, start, end);
    }
}

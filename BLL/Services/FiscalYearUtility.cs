using System;

namespace Dashboard.BLL.Services;

public interface IFiscalYearUtility
{
    int CalculateCurrentFY();
    (DateTime Start, DateTime End) GetFYPeriod(int fy);
    string FormatFY(int fy);
    int GetStartYear(int fy);
}

public class FiscalYearUtility : IFiscalYearUtility
{
    public int CalculateCurrentFY()
    {
        var now = DateTime.Now;
        // FY starts April 1st. 
        // 2026-04-01 -> 2627
        // 2026-03-31 -> 2526
        if (now.Month >= 4)
        {
            return (now.Year % 100) * 100 + (now.Year % 100 + 1);
        }
        else
        {
            return (now.Year % 100 - 1) * 100 + (now.Year % 100);
        }
    }

    public (DateTime Start, DateTime End) GetFYPeriod(int fy)
    {
        int startYearShort = fy / 100;
        int startYear = 2000 + startYearShort;
        
        return (new DateTime(startYear, 4, 1), new DateTime(startYear + 1, 3, 31, 23, 59, 59));
    }

    public string FormatFY(int fy)
    {
        int start = fy / 100;
        int end = fy % 100;
        return $"FY 20{start:D2}-{end:D2}";
    }

    public int GetStartYear(int fy)
    {
        return 2000 + (fy / 100);
    }
}

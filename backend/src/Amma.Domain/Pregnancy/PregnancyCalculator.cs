namespace Amma.Domain.Pregnancy;

public readonly record struct GestationalAge(int TotalDays, int Weeks, int Days, int Trimester, int DaysToGo, int ProgressPercent);

/// <summary>
/// Naegele's rule based calculations (EDD = LMP + 280 days). The doctor's confirmed dating
/// always takes precedence; this is only used to convert between LMP and EDD.
/// </summary>
public static class PregnancyCalculator
{
    public const int FullTermDays = 280;

    public static DateOnly EddFromLmp(DateOnly lmp) => lmp.AddDays(FullTermDays);
    public static DateOnly LmpFromEdd(DateOnly edd) => edd.AddDays(-FullTermDays);

    public static int TrimesterForWeek(int week) => week < 14 ? 1 : week < 28 ? 2 : 3;

    public static GestationalAge Calculate(DateOnly lmp, DateOnly today)
    {
        var total = Math.Max(0, today.DayNumber - lmp.DayNumber);
        var weeks = total / 7;
        var edd = EddFromLmp(lmp);
        var left = Math.Max(0, edd.DayNumber - today.DayNumber);
        var pct = (int)Math.Clamp(Math.Round(total / (double)FullTermDays * 100), 0, 100);
        return new GestationalAge(total, weeks, total % 7, TrimesterForWeek(weeks), left, pct);
    }

    /// <summary>A plausible current pregnancy is between 2 and ~43 weeks from LMP.</summary>
    public static bool IsPlausibleLmp(DateOnly lmp, DateOnly today)
    {
        var days = today.DayNumber - lmp.DayNumber;
        return days is >= 14 and <= 300;
    }
}

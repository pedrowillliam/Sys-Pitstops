namespace SysPitstops.Api.Domain;

// How a number compares with the same number a month earlier. Lives here, and
// not in the screen, because "no previous value" and "previous value of zero"
// are the same trap in every card that shows it.
public static class PeriodChange
{
    /// <summary>
    /// Percentage variation, one decimal place. Null when there is nothing to
    /// compare against: growing from zero is not an infinite percentage, it is
    /// a first month — and a card showing "+∞%" or "+100%" there would be
    /// inventing a comparison that the data does not support.
    /// </summary>
    public static decimal? Percent(decimal current, decimal previous) =>
        previous == 0m ? null : Math.Round((current - previous) / previous * 100m, 1);
}

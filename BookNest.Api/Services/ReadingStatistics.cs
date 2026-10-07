using BookNest.Api.Domain;

namespace BookNest.Api.Services;

public static class ReadingStatistics
{
    /// <summary>
    /// Splits activity by local day and calculates totals, averages, streaks and period comparisons.
    /// </summary>
    public static object Calculate(IEnumerable<ReadingSession> sessions, TimeZoneInfo zone, string period, DateTimeOffset now)
    {
        var days = new SortedDictionary<DateOnly, (double Seconds, int Pages)>();
        foreach (var s in sessions.Where(s => s.EndedAt > s.StartedAt && s.ActiveSeconds > 0))
        {
            var cursor = s.StartedAt;
            var end = s.EndedAt!.Value;
            while (cursor < end)
            {
                var day = DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(cursor, zone).DateTime);
                var midnight = new DateTimeOffset(TimeZoneInfo.ConvertTimeToUtc(day.AddDays(1).ToDateTime(TimeOnly.MinValue), zone));
                var stop = end < midnight ? end : midnight;
                var fraction = (stop - cursor).TotalSeconds / (end - s.StartedAt).TotalSeconds;
                var value = days.GetValueOrDefault(day);
                // Page turns are attributed to the heartbeat's completion day; seconds split exactly at midnight.
                days[day] = (value.Seconds + s.ActiveSeconds * fraction, value.Pages + (stop == end ? s.PagesRead : 0));
                cursor = stop;
            }
        }

        var today = DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(now, zone).DateTime);
        var start = period switch
        {
            "week" => today.AddDays(-(((int)today.DayOfWeek + 6) % 7)),
            "month" => new(today.Year, today.Month, 1),
            "year" => new(today.Year, 1, 1),
            _ => days.Keys.FirstOrDefault(today)
        };
        var selected = days.Where(d => d.Key >= start && d.Key <= today).ToList();
        var seconds = selected.Sum(d => d.Value.Seconds);
        var longest = 0;
        var streak = 0;
        DateOnly? previous = null;
        foreach (var d in selected)
        {
            streak = previous?.AddDays(1) == d.Key ? streak + 1 : 1;
            longest = Math.Max(longest, streak);
            previous = d.Key;
        }

        var elapsed = today.DayNumber - start.DayNumber + 1;
        double? change = null;
        if (period != "all")
        {
            var prior = period switch
            {
                "week" => start.AddDays(-7),
                "month" => start.AddMonths(-1),
                _ => start.AddYears(-1)
            };
            var priorSeconds = days.Where(d => d.Key >= prior && d.Key < start && d.Key < prior.AddDays(elapsed)).Sum(d => d.Value.Seconds);
            if (priorSeconds > 0)
            {
                change = 100 * (seconds - priorSeconds) / priorSeconds;
            }
        }

        var buckets = selected.GroupBy(d => period switch
        {
            "month" => start.AddDays(((d.Key.DayNumber - start.DayNumber) / 7) * 7).ToString("yyyy-MM-dd"),
            "year" => d.Key.ToString("yyyy-MM"),
            "all" => d.Key.Year.ToString(),
            _ => d.Key.ToString("yyyy-MM-dd")
        });
        return new
        {
            period,
            timezone = zone.Id,
            start,
            end = today,
            totalSeconds = seconds,
            daysRead = selected.Count,
            averageSeconds = seconds / Math.Max(1, elapsed),
            pagesRead = selected.Sum(d => d.Value.Pages),
            longestStreak = longest,
            percentageChange = change,
            breakdown = buckets.Select(g => new
            {
                label = g.Key,
                seconds = g.Sum(d => d.Value.Seconds),
                pages = g.Sum(d => d.Value.Pages)
            })
        };
    }
}

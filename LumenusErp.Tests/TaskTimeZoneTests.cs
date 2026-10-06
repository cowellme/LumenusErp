using LumenusErp.Services;
using Xunit;

namespace LumenusErp.Tests;

public class TaskTimeZoneTests
{
    [Fact]
    public void DateContext_MatchesMyasiFormat()
    {
        var tz = TaskTimeZone.Resolve("Europe/Moscow");
        // 2026-10-05 10:15 UTC = 13:15 по Москве, понедельник
        var s = TaskTimeZone.DateContext(new DateTime(2026, 10, 5, 10, 15, 0), tz);
        Assert.Equal("Сейчас: 2026-10-05, понедельник, 13:15, часовой пояс Europe/Moscow (UTC+03:00)", s);
    }

    [Fact]
    public void Resolve_UnknownZone_FallsBackToUtcPlus3()
    {
        var tz = TaskTimeZone.Resolve("No/Such_Zone");
        Assert.Equal(TimeSpan.FromHours(3), tz.BaseUtcOffset);
    }
}

using LumenusErp.Services;
using Xunit;

namespace LumenusErp.Tests;

public class CallTaskParserTests
{
    // Фиксированный UTC+3, чтобы тест не зависел от tzdata
    private static readonly TimeZoneInfo Msk = TimeZoneInfo.CreateCustomTimeZone("msk", TimeSpan.FromHours(3), "msk", "msk");

    private static List<ParsedCallTask> Parse(string raw) => CallTaskParser.Parse(raw, Msk)!;

    [Fact]
    public void Parse_DeadlineDateOnly_Means2359Local()
    {
        var t = Assert.Single(Parse("""[{"title":"Иван: договор","quote":"q","start":null,"deadline":"2026-10-09"}]"""));
        Assert.Null(t.StartAt);
        Assert.Equal(new DateTime(2026, 10, 9, 20, 59, 0), t.DueAt);
    }

    [Fact]
    public void Parse_StartDateOnly_Means0000Local()
    {
        var t = Assert.Single(Parse("""[{"title":"a","quote":"","start":"2026-10-12","deadline":"2026-10-20"}]"""));
        Assert.Equal(new DateTime(2026, 10, 11, 21, 0, 0), t.StartAt);
        Assert.Equal(new DateTime(2026, 10, 20, 20, 59, 0), t.DueAt);
    }

    [Fact]
    public void Parse_DateTime_ConvertedToUtc()
    {
        var t = Assert.Single(Parse("""[{"title":"a","quote":"","deadline":"2026-10-06T10:00"}]"""));
        Assert.Equal(new DateTime(2026, 10, 6, 7, 0, 0), t.DueAt);
        Assert.Equal(DateTimeKind.Utc, t.DueAt!.Value.Kind);
    }

    [Fact]
    public void Parse_InvalidDates_BecomeNullTaskKept()
    {
        var t = Assert.Single(Parse("""[{"title":"a","quote":"","start":"завтра","deadline":"2026-13-45"}]"""));
        Assert.Equal("a", t.Title);
        Assert.Null(t.StartAt);
        Assert.Null(t.DueAt);
    }

    [Fact]
    public void Parse_NonStringDates_Ignored()
    {
        var t = Assert.Single(Parse("""[{"title":"a","quote":"","start":5,"deadline":true}]"""));
        Assert.Null(t.StartAt);
        Assert.Null(t.DueAt);
    }

    [Fact]
    public void Parse_StartAfterDeadline_DropsStart()
    {
        var t = Assert.Single(Parse("""[{"title":"a","quote":"","start":"2026-10-25","deadline":"2026-10-20"}]"""));
        Assert.Null(t.StartAt);
        Assert.NotNull(t.DueAt);
    }

    [Fact]
    public void Parse_OldFormat_StillWorks()
    {
        var t = Assert.Single(Parse("""[{"title":"Отправить смету","quote":"сегодня отправлю"}]"""));
        Assert.Equal("Отправить смету", t.Title);
        Assert.Equal("сегодня отправлю", t.Quote);
        Assert.Null(t.StartAt);
        Assert.Null(t.DueAt);
    }

    [Fact]
    public void Parse_GarbageAroundJson_AndFences()
    {
        var r = Parse("Вот задачи:\n```json\n[{\"title\":\"a\",\"quote\":\"b\",\"deadline\":\"2026-10-09\"}]\n```\nГотово.");
        Assert.Single(r);
        Assert.NotNull(r[0].DueAt);
    }

    [Fact]
    public void Parse_EmptyArrayAndGarbage()
    {
        Assert.Empty(Parse("[]"));
        Assert.Null(CallTaskParser.Parse("не json", Msk));
        Assert.Null(CallTaskParser.Parse(null, Msk));
    }
}

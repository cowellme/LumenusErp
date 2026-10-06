using LumenusErp.Services;
using Xunit;

namespace LumenusErp.Tests;

public class TaskDateFormatTests
{
    private const int Msk = 180;
    // 2026-10-05 13:15 по Москве
    private static readonly DateTime Now = new(2026, 10, 5, 10, 15, 0);

    // Местное московское время → UTC для краткости записи
    private static DateTime Local(int y, int m, int d, int h = 0, int min = 0) => new DateTime(y, m, d, h, min, 0).AddMinutes(-Msk);

    [Fact]
    public void Format_DueOnlyDay_NoTime()
    {
        var l = TaskDateFormat.Format(null, Local(2026, 10, 9, 23, 59), true, Now, Msk);
        Assert.Null(l.Start);
        Assert.Equal("до 9 окт", l.Due);
        Assert.False(l.Overdue);
    }

    [Fact]
    public void Format_DueWithTime()
    {
        var l = TaskDateFormat.Format(null, Local(2026, 10, 9, 10), true, Now, Msk);
        Assert.Equal("до 9 окт, 10:00", l.Due);
    }

    [Fact]
    public void Format_StartOnly()
    {
        var l = TaskDateFormat.Format(Local(2026, 10, 12), null, true, Now, Msk);
        Assert.Equal("с 12 окт", l.Start);
        Assert.Null(l.Due);
    }

    [Fact]
    public void Format_StartWithTime()
    {
        var l = TaskDateFormat.Format(Local(2026, 10, 12, 9, 30), null, true, Now, Msk);
        Assert.Equal("с 12 окт, 09:30", l.Start);
    }

    [Fact]
    public void Format_Both()
    {
        var l = TaskDateFormat.Format(Local(2026, 10, 12), Local(2026, 10, 20, 23, 59), true, Now, Msk);
        Assert.Equal("с 12 окт", l.Start);
        Assert.Equal("до 20 окт", l.Due);
    }

    [Fact]
    public void Format_OtherYear_AddsYear()
    {
        var l = TaskDateFormat.Format(null, Local(2027, 1, 15, 23, 59), true, Now, Msk);
        Assert.Equal("до 15 янв 2027", l.Due);
    }

    [Fact]
    public void Format_UsesUserOffset_DayShifts()
    {
        // 20:59 UTC = 23:59 в UTC+3, но 20:59 в UTC: время тогда показывается
        var utc = new DateTime(2026, 10, 9, 20, 59, 0);
        Assert.Equal("до 9 окт", TaskDateFormat.Format(null, utc, true, Now, Msk).Due);
        Assert.Equal("до 9 окт, 20:59", TaskDateFormat.Format(null, utc, true, Now, 0).Due);
    }

    [Fact]
    public void Format_Overdue_OnlyForOpenWithPastDue()
    {
        var past = Local(2026, 10, 4, 23, 59);
        Assert.True(TaskDateFormat.Format(null, past, true, Now, Msk).Overdue);
        Assert.False(TaskDateFormat.Format(null, past, false, Now, Msk).Overdue);
        Assert.False(TaskDateFormat.Format(null, Local(2026, 10, 5, 23, 59), true, Now, Msk).Overdue);
        Assert.False(TaskDateFormat.Format(Local(2026, 10, 1), null, true, Now, Msk).Overdue);
    }

    [Fact]
    public void FromInputs_NoTime_DefaultsStartAndDue()
    {
        Assert.True(TaskDateFormat.TryFromInputs("2026-10-12", "", false, Msk, out var start));
        Assert.Equal(new DateTime(2026, 10, 11, 21, 0, 0), start);
        Assert.True(TaskDateFormat.TryFromInputs("2026-10-20", null, true, Msk, out var due));
        Assert.Equal(new DateTime(2026, 10, 20, 20, 59, 0), due);
    }

    [Fact]
    public void FromInputs_WithTime()
    {
        Assert.True(TaskDateFormat.TryFromInputs("2026-10-09", "10:00", true, Msk, out var due));
        Assert.Equal(new DateTime(2026, 10, 9, 7, 0, 0), due);
        Assert.Equal(DateTimeKind.Utc, due!.Value.Kind);
        Assert.True(TaskDateFormat.TryFromInputs("2026-10-09", "10:00", false, -300, out var start));
        Assert.Equal(new DateTime(2026, 10, 9, 15, 0, 0), start);
    }

    [Fact]
    public void FromInputs_EmptyDate_ClearsAndInvalidFails()
    {
        Assert.True(TaskDateFormat.TryFromInputs("", "10:00", true, Msk, out var none));
        Assert.Null(none);
        Assert.False(TaskDateFormat.TryFromInputs("вчера", "", true, Msk, out _));
        Assert.False(TaskDateFormat.TryFromInputs("2026-10-09", "25:99", true, Msk, out _));
    }

    [Fact]
    public void ToInputs_RoundTrip_HidesDefaultTime()
    {
        var due = Local(2026, 10, 20, 23, 59);
        Assert.Equal(("2026-10-20", ""), TaskDateFormat.ToInputs(due, true, Msk));
        var start = Local(2026, 10, 12, 9, 30);
        Assert.Equal(("2026-10-12", "09:30"), TaskDateFormat.ToInputs(start, false, Msk));
        Assert.Equal(("", ""), TaskDateFormat.ToInputs(null, true, Msk));

        var (d, t) = TaskDateFormat.ToInputs(start, false, Msk);
        TaskDateFormat.TryFromInputs(d, t, false, Msk, out var back);
        Assert.Equal(start, back);
    }
}

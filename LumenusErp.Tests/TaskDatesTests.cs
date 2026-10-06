using LumenusErp.Data;
using LumenusErp.Services;
using Xunit;

namespace LumenusErp.Tests;

public class TaskDatesTests
{
    private static readonly DateTime D1 = new(2026, 10, 9, 10, 0, 0);
    private static readonly DateTime D2 = new(2026, 10, 20, 10, 0, 0);

    [Fact]
    public void ToUtc_ConvertsDifferentOffsets()
    {
        var msk = TaskDates.ToUtc(new DateTimeOffset(2026, 10, 9, 23, 59, 0, TimeSpan.FromHours(3)));
        Assert.Equal(new DateTime(2026, 10, 9, 20, 59, 0), msk);
        Assert.Equal(DateTimeKind.Utc, msk!.Value.Kind);

        var neg = TaskDates.ToUtc(new DateTimeOffset(2026, 10, 9, 20, 0, 0, TimeSpan.FromHours(-5)));
        Assert.Equal(new DateTime(2026, 10, 10, 1, 0, 0), neg);

        var utc = TaskDates.ToUtc(new DateTimeOffset(2026, 10, 9, 1, 0, 0, TimeSpan.Zero));
        Assert.Equal(new DateTime(2026, 10, 9, 1, 0, 0), utc);

        Assert.Null(TaskDates.ToUtc(null));
    }

    [Fact]
    public void ToOffset_MarksUnspecifiedAsUtc()
    {
        var o = TaskDates.ToOffset(new DateTime(2026, 10, 9, 20, 59, 0, DateTimeKind.Unspecified));
        Assert.Equal(TimeSpan.Zero, o!.Value.Offset);
        Assert.Equal(20, o.Value.Hour);
        Assert.Null(TaskDates.ToOffset(null));
    }

    [Fact]
    public void Validate_StartAfterDue_IsError() => Assert.NotNull(TaskDates.Validate(D2, D1));

    [Fact]
    public void Validate_EqualOrOrderedOrPartial_IsOk()
    {
        Assert.Null(TaskDates.Validate(D1, D1));
        Assert.Null(TaskDates.Validate(D1, D2));
        Assert.Null(TaskDates.Validate(D1, null));
        Assert.Null(TaskDates.Validate(null, D2));
        Assert.Null(TaskDates.Validate(null, null));
    }

    [Fact]
    public void ShouldFillDuplicate_OnlyWhenDuplicateHasNoDates()
    {
        Assert.True(TaskDates.ShouldFillDuplicate(null, null, null, D2));
        Assert.True(TaskDates.ShouldFillDuplicate(null, null, D1, D2));
        Assert.False(TaskDates.ShouldFillDuplicate(null, D1, null, D2));
        Assert.False(TaskDates.ShouldFillDuplicate(D1, null, D1, D2));
        Assert.False(TaskDates.ShouldFillDuplicate(D1, D2, null, null));
        Assert.False(TaskDates.ShouldFillDuplicate(null, null, null, null));
    }

    private static TaskItem Item(string name, DateTime created, DateTime? due) =>
        new() { Id = Guid.NewGuid(), Title = name, CreatedAt = created, DueAt = due };

    [Fact]
    public void SortOpen_DueAscendingThenNoDueByCreatedDescending()
    {
        var noDueOld = Item("noDueOld", new(2026, 1, 1), null);
        var noDueNew = Item("noDueNew", new(2026, 3, 1), null);
        var late = Item("late", new(2026, 1, 1), new DateTime(2026, 10, 20));
        var soon = Item("soon", new(2026, 2, 1), new DateTime(2026, 10, 9));

        var sorted = TaskDates.SortOpen([noDueOld, late, noDueNew, soon]).Select(t => t.Title).ToArray();

        Assert.Equal(["soon", "late", "noDueNew", "noDueOld"], sorted);
    }
}

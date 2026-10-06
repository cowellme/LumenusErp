using LumenusErp.Data;
using LumenusErp.Services;
using Xunit;

namespace LumenusErp.Tests;

public class UserPromptRulesTests
{
    private const string Key = DefaultPrompts.MyasiTasksKey;
    private static readonly DateTime T1 = new(2026, 1, 1, 10, 0, 0);
    private static readonly DateTime T2 = new(2026, 2, 1, 10, 0, 0);

    private static UserAiPrompt Own(string text = "свой", string? model = null) =>
        new() { Key = Key, SystemPrompt = text, Model = model, Temperature = 0.3, UpdatedAt = T1 };

    private static AiPrompt Common(string text = "общий") =>
        new() { Key = Key, SystemPrompt = text, Model = "m/common", Temperature = 0.1, UpdatedAt = T2 };

    [Fact]
    public void Resolve_OwnOverridesCommon()
    {
        var r = UserPromptRules.Resolve(Own(), Common(), Key);
        Assert.Equal("свой", r.Text);
        Assert.Equal("user", r.Source);
        Assert.Equal(T1, r.UpdatedAt);
        Assert.Equal(0.3, r.Temperature);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Resolve_EmptyOwnFallsBackToCommon(string text)
    {
        var r = UserPromptRules.Resolve(Own(text), Common(), Key);
        Assert.Equal("общий", r.Text);
        Assert.Equal("default", r.Source);
        Assert.Equal("m/common", r.Model);
        Assert.Equal(T2, r.UpdatedAt);
    }

    [Fact]
    public void Resolve_NoCommonUsesCodeDefault()
    {
        var r = UserPromptRules.Resolve(null, null, Key);
        Assert.Equal(DefaultPrompts.MyasiTasks, r.Text);
        Assert.Equal("default", r.Source);
        Assert.Equal(DateTime.MinValue, r.UpdatedAt);
    }

    [Theory]
    [InlineData("")]
    [InlineData("  ")]
    [InlineData(null)]
    public void Resolve_BlankModelBecomesNull(string? model)
    {
        Assert.Null(UserPromptRules.Resolve(Own(model: model), null, Key).Model);
    }

    [Fact]
    public void ETag_StableAndChangesWithSourceAndTime()
    {
        var a = UserPromptRules.ETag(UserPromptRules.Resolve(Own(), Common(), Key));
        Assert.Equal(a, UserPromptRules.ETag(UserPromptRules.Resolve(Own(), Common(), Key)));
        Assert.StartsWith("\"", a);
        Assert.EndsWith("\"", a);

        var common = UserPromptRules.ETag(UserPromptRules.Resolve(null, Common(), Key));
        Assert.NotEqual(a, common);

        var later = Own();
        later.UpdatedAt = T2;
        Assert.NotEqual(a, UserPromptRules.ETag(UserPromptRules.Resolve(later, Common(), Key)));
    }

    [Theory]
    [InlineData("myasi-tasks", true)]
    [InlineData("call-tasks", true)]
    [InlineData("estimate", false)]
    [InlineData("faq", false)]
    [InlineData("whatever", false)]
    [InlineData(null, false)]
    public void IsPersonalKey(string? key, bool expected) => Assert.Equal(expected, UserPromptRules.IsPersonalKey(key));

    [Fact]
    public void Validate_Rules()
    {
        Assert.Null(UserPromptRules.Validate("текст", null, null));
        Assert.Null(UserPromptRules.Validate("текст", "a/b", 2));
        Assert.Null(UserPromptRules.Validate(new string('a', UserPromptRules.MaxText), null, 0));
        Assert.NotNull(UserPromptRules.Validate("", null, null));
        Assert.NotNull(UserPromptRules.Validate("  ", null, null));
        Assert.NotNull(UserPromptRules.Validate(new string('a', UserPromptRules.MaxText + 1), null, null));
        Assert.NotNull(UserPromptRules.Validate("текст", null, -0.1));
        Assert.NotNull(UserPromptRules.Validate("текст", null, 2.1));
        Assert.NotNull(UserPromptRules.Validate("текст", new string('m', UserPromptRules.MaxModel + 1), null));
    }

    [Theory]
    [InlineData("\"user-1\"", "\"user-1\"", true)]
    [InlineData("W/\"user-1\"", "\"user-1\"", true)]
    [InlineData("\"a\", \"user-1\"", "\"user-1\"", true)]
    [InlineData("*", "\"user-1\"", true)]
    [InlineData("\"other\"", "\"user-1\"", false)]
    [InlineData("", "\"user-1\"", false)]
    [InlineData(null, "\"user-1\"", false)]
    public void NotModified(string? header, string etag, bool expected) => Assert.Equal(expected, UserPromptRules.NotModified(header, etag));

    [Fact]
    public void DefaultPrompts_HasMyasiTasks()
    {
        Assert.False(string.IsNullOrWhiteSpace(DefaultPrompts.For("myasi-tasks")));
    }
}

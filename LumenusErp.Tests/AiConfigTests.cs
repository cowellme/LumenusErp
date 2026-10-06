using LumenusErp.Data;
using LumenusErp.Services;
using Xunit;

namespace LumenusErp.Tests;

public class AiConfigTests
{
    private static readonly DateTime T = new(2026, 3, 1, 12, 0, 0);
    private static readonly MyasiAiConfig Base = new("asr/m", "dedup/m", "diar/m", T);

    [Fact]
    public void ETag_SameInputs_Same() =>
        Assert.Equal(AiConfigRules.ETag(Base), AiConfigRules.ETag(Base with { }));

    [Fact]
    public void ETag_ChangesWithAnyField()
    {
        var etag = AiConfigRules.ETag(Base);
        Assert.NotEqual(etag, AiConfigRules.ETag(Base with { AsrModel = "x" }));
        Assert.NotEqual(etag, AiConfigRules.ETag(Base with { DedupModel = "x" }));
        Assert.NotEqual(etag, AiConfigRules.ETag(Base with { DiarizationModel = "x" }));
        Assert.NotEqual(etag, AiConfigRules.ETag(Base with { UpdatedAt = T.AddSeconds(1) }));
        Assert.NotEqual(etag, AiConfigRules.ETag(Base with { AsrModel = null }));
    }

    [Fact]
    public void ETag_ShiftingModelBetweenFields_Differs() =>
        Assert.NotEqual(
            AiConfigRules.ETag(new MyasiAiConfig("a", null, null, T)),
            AiConfigRules.ETag(new MyasiAiConfig(null, "a", null, T)));

    [Fact]
    public void NotModified_MatchesOwnEtag_ButNotStale()
    {
        var etag = AiConfigRules.ETag(Base);
        Assert.True(AiConfigRules.NotModified(etag, Base));
        Assert.True(AiConfigRules.NotModified("W/" + etag, Base));
        Assert.False(AiConfigRules.NotModified(etag, Base with { DedupModel = "new" }));
        Assert.False(AiConfigRules.NotModified("", Base));
        Assert.False(AiConfigRules.NotModified(null, Base));
    }

    [Fact]
    public void Validate_UnknownStage_Error() => Assert.NotNull(AiConfigRules.Validate("nope", "m"));

    [Fact]
    public void Validate_TooLongModel_Error() =>
        Assert.NotNull(AiConfigRules.Validate(AiStages.MyasiAsrKey, new string('x', 201)));

    [Fact]
    public void Validate_EmptyModel_Ok() => Assert.Null(AiConfigRules.Validate(AiStages.MyasiAsrKey, "  "));

    [Fact]
    public void Clean_EmptyToNull() => Assert.Null(AiConfigRules.Clean("   "));

    [Fact]
    public void Stages_KeysUnique() =>
        Assert.Equal(AiStages.All.Count, AiStages.All.Select(s => s.Key).Distinct().Count());

    [Fact]
    public void Stages_WithPrompt_MatchDefaultPrompts()
    {
        var withPrompt = AiStages.All.Where(s => s.HasPrompt).Select(s => s.Key).Order().ToList();
        Assert.Equal(["call-tasks", "estimate", "faq", "myasi-tasks"], withPrompt);
        Assert.All(withPrompt, k => Assert.NotNull(DefaultPrompts.For(k)));
        Assert.All(AiStages.All.Where(s => !s.HasPrompt), s => Assert.Null(DefaultPrompts.For(s.Key)));
    }
}

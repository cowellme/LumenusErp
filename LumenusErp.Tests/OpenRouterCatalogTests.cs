using LumenusErp.Services;
using Xunit;

namespace LumenusErp.Tests;

public class OpenRouterCatalogTests
{
    private const string Json = """
    {"data":[
      {"id":"a/chat","name":"Alpha Chat","context_length":128000,"pricing":{"prompt":"0.000003","completion":"0.000015"},
       "architecture":{"input_modalities":["text"],"output_modalities":["text"]}},
      {"id":"typesafe/jev-router","name":"TypeSafe: Jev Router","context_length":1000000,"pricing":{"prompt":"-1","completion":"-1"},
       "architecture":{"input_modalities":["audio","file","image","text","video"],"output_modalities":["text"]}},
      {"id":"b/vision","name":"beta Vision","pricing":{"prompt":"abc"},
       "architecture":{"input_modalities":["image","text"],"output_modalities":["text"]}},
      {"id":"c/imgonly","name":"Gamma Image","architecture":{"input_modalities":["image"],"output_modalities":["text"]}},
      {"id":"d/noarch","name":"Delta"}
    ]}
    """;

    private static List<OpenRouterModel> Models() => OpenRouterCatalog.Parse(Json);

    [Fact]
    public void Parse_NormalItem_ConvertsPriceToPerMillion()
    {
        var m = Models().Single(x => x.Id == "a/chat");
        Assert.Equal("Alpha Chat", m.Name);
        Assert.Equal(128000, m.ContextLength);
        Assert.Equal(3m, m.PromptPricePerM);
        Assert.Equal(15m, m.CompletionPricePerM);
        Assert.Equal(["text"], m.InputModalities);
    }

    [Fact]
    public void Parse_VariablePrice_IsNull()
    {
        var m = Models().Single(x => x.Id == "typesafe/jev-router");
        Assert.Null(m.PromptPricePerM);
        Assert.Null(m.CompletionPricePerM);
    }

    [Fact]
    public void Parse_MissingOrInvalidPrice_IsNull()
    {
        var m = Models().Single(x => x.Id == "b/vision");
        Assert.Null(m.PromptPricePerM);
        Assert.Null(m.CompletionPricePerM);
        Assert.Null(m.ContextLength);
    }

    [Fact]
    public void Parse_NoArchitecture_EmptyModalities()
    {
        var m = Models().Single(x => x.Id == "d/noarch");
        Assert.Empty(m.InputModalities);
        Assert.Empty(m.OutputModalities);
    }

    [Theory]
    [InlineData("")]
    [InlineData("не json")]
    [InlineData("[]")]
    [InlineData("{\"data\":5}")]
    [InlineData("{\"data\":[1,\"x\",{\"name\":\"без id\"}]}")]
    public void Parse_Garbage_EmptyList(string json) => Assert.Empty(OpenRouterCatalog.Parse(json));

    [Fact]
    public void ForStage_Audio_NeedsAudioInAndTextOut()
    {
        var ids = OpenRouterCatalog.ForStage(Models(), AiStages.Audio).Select(m => m.Id);
        Assert.Equal(["typesafe/jev-router"], ids);
    }

    [Fact]
    public void ForStage_Text_SortedByName()
    {
        var ids = OpenRouterCatalog.ForStage(Models(), AiStages.Text).Select(m => m.Id);
        Assert.Equal(["a/chat", "b/vision", "typesafe/jev-router"], ids);
    }

    [Theory]
    [InlineData("jev", "typesafe/jev-router")]
    [InlineData("ALPHA", "a/chat")]
    [InlineData("B/VIS", "b/vision")]
    [InlineData("  gamma ", "c/imgonly")]
    public void Search_ByIdOrNameIgnoringCase(string q, string id) =>
        Assert.Equal([id], OpenRouterCatalog.Search(Models(), q).Select(m => m.Id));

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void Search_EmptyQuery_ReturnsAll(string? q) => Assert.Equal(5, OpenRouterCatalog.Search(Models(), q).Count);
}

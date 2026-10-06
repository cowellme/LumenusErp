using LumenusErp.Data;
using Xunit;

namespace LumenusErp.Tests;

public class AiPromptSeedTests
{
    private const string V1 = "Первая строка.\n\nВторая строка.";

    [Fact]
    public void ShouldUpgrade_ExactMatch() => Assert.True(AiPromptSeed.ShouldUpgrade(V1, V1));

    [Fact]
    public void ShouldUpgrade_CrLfAndEdgeWhitespace()
    {
        Assert.True(AiPromptSeed.ShouldUpgrade("  \r\nПервая строка.\r\n\r\nВторая строка.\r\n", V1));
    }

    [Fact]
    public void ShouldUpgrade_EditedOrMissing_IsFalse()
    {
        Assert.False(AiPromptSeed.ShouldUpgrade(V1 + " Правка.", V1));
        Assert.False(AiPromptSeed.ShouldUpgrade("Первая  строка.\n\nВторая строка.", V1));
        Assert.False(AiPromptSeed.ShouldUpgrade(null, V1));
    }

    [Fact]
    public void NewDefault_DiffersFromV1()
    {
        Assert.False(AiPromptSeed.ShouldUpgrade(LumenusErp.Services.DefaultPrompts.CallTasks, LumenusErp.Services.DefaultPrompts.CallTasksV1));
    }
}

using ROMVault.Avalonia.ViewModels;

namespace ROMVault.Avalonia.Tests.ViewModels;

public sealed class GameFilterServiceTests
{
    [Fact]
    public void Parse_SeparatesFreeTextDescriptionAndStatuses()
    {
        GameFilter filter = GameFilter.Parse("mario desc:arcade status:missing,fixes");

        Assert.Equal("mario", filter.FreeText);
        Assert.Equal("arcade", filter.DescriptionText);
        Assert.Contains("missing", filter.Statuses);
        Assert.Contains("fixes", filter.Statuses);
    }

    [Fact]
    public void Matches_UsesCaseInsensitiveNameAndDescriptionSearch()
    {
        var values = new GameSearchValues("Super Mario Bros.", "Classic PLATFORMER", GameStatusFlags.Correct);

        Assert.True(GameFilterService.Matches(GameFilter.Parse("mario"), values));
        Assert.True(GameFilterService.Matches(GameFilter.Parse("desc:platformer"), values));
        Assert.False(GameFilterService.Matches(GameFilter.Parse("zelda"), values));
    }

    [Theory]
    [InlineData("complete", GameStatusFlags.Correct, true)]
    [InlineData("complete", GameStatusFlags.Correct | GameStatusFlags.Missing, false)]
    [InlineData("partial", GameStatusFlags.Correct | GameStatusFlags.Missing, true)]
    [InlineData("empty", GameStatusFlags.Missing, true)]
    [InlineData("corrupt", GameStatusFlags.Corrupt, true)]
    public void Matches_RecognizesStatusTokens(string token, GameStatusFlags flags, bool expected)
    {
        var values = new GameSearchValues("Game", string.Empty, flags);

        bool actual = GameFilterService.Matches(GameFilter.Parse($"status:{token}"), values);

        Assert.Equal(expected, actual);
    }

    [Fact]
    public void IsVisible_AlwaysIncludesExceptionalAndUncategorizedRows()
    {
        var noneSelected = new GameVisibilityOptions(false, false, false, false, false, false);

        Assert.True(GameFilterService.IsVisible(GameStatusFlags.Unknown, noneSelected));
        Assert.True(GameFilterService.IsVisible(GameStatusFlags.InToSort, noneSelected));
        Assert.True(GameFilterService.IsVisible(GameStatusFlags.Corrupt, noneSelected));
        Assert.True(GameFilterService.IsVisible(GameStatusFlags.None, noneSelected));
        Assert.False(GameFilterService.IsVisible(GameStatusFlags.Correct, noneSelected));
    }
}

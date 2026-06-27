using CanXe.Domain.Services;

namespace CanXe.Tests.Domain;

public class AutocompleteRankerTests
{
    [Fact]
    public void PrefixSearch_RanksChiDucBeforeAnhDuc()
    {
        var ranked = AutocompleteRanker.Rank(
            new[] { "Anh Đức", "Chị Đức", "Chị Đào" },
            x => x,
            _ => null,
            "Chị Đ",
            8);

        Assert.Equal("Chị Đức", ranked[0]);
        Assert.Equal("Chị Đào", ranked[1]);
        Assert.DoesNotContain("Anh Đức", ranked);
    }

    [Fact]
    public void NormalizedSearch_FindsChiDucWithoutDiacritics()
    {
        var score = AutocompleteRanker.ScoreMatch("Chị Đức", "chi duc");
        Assert.True(score >= AutocompleteRanker.ScoreContains);
    }

    [Fact]
    public void TokenPrefixMatch_MatchesPartialTokens()
    {
        Assert.True(AutocompleteRanker.ScoreTokenPrefixMatch("CHI DUC", "CHI D"));
    }
}

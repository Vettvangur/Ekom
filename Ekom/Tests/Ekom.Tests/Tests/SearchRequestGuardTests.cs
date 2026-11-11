using Ekom.Models;
using Ekom.Utilities;
using Xunit;

namespace Ekom.Tests.Tests;

public sealed class SearchRequestGuardTests
{
    private static readonly string[] Allowed = ["nodeName", "sku", "title"];

    [Theory]
    [InlineData("title", true)]
    [InlineData("title_en-US", true)]
    [InlineData("__Path", true)]
    [InlineData("", false)]
    [InlineData("title:foo OR bar", false)]
    [InlineData("title)", false)]
    [InlineData("*", false)]
    [InlineData("-title", false)]
    public void FieldNamesMustBePlainIdentifiers(string name, bool expected)
    {
        Assert.Equal(expected, SearchRequestGuard.IsValidFieldName(name));
    }

    [Theory]
    [InlineData("^4.0", "^4.0")]
    [InlineData("^12", "^12.0")]
    [InlineData(null, "")]
    [InlineData("^2 OR hidden:*", "")]
    [InlineData(") OR (hidden:a*", "")]
    [InlineData("^1e5", "")]
    public void BoosterIsNumericOrDropped(string? booster, string expected)
    {
        Assert.Equal(expected, SearchRequestGuard.NormalizeBooster(booster));
    }

    [Theory]
    [InlineData("0.7", "0.7")]
    [InlineData("1", "1.0")]
    [InlineData("2", "0.5")]
    [InlineData("0.5 OR hidden:*", "0.5")]
    [InlineData(null, "0.5")]
    public void FuzzyConfigurationIsNumericOrDefault(string? fuzzy, string expected)
    {
        Assert.Equal(expected, SearchRequestGuard.NormalizeFuzzyConfiguration(fuzzy));
    }

    [Fact]
    public void SanitizeFieldsDropsInjectedNamesAndKeepsValidOnes()
    {
        var fields = SearchRequestGuard.SanitizeFields(
        [
            new EkomSearchField { Name = "title", Booster = "^4.0", FuzzyConfiguration = "0.7" },
            new EkomSearchField { Name = "title:x) OR (secret", Booster = "^1" },
            new EkomSearchField { Name = "sku", Booster = "^1 OR secret:*", SearchType = (EkomSearchType)99 },
        ]);

        Assert.Equal(["title", "sku"], fields.Select(x => x.Name));
        Assert.Equal("^4.0", fields[0].Booster);
        Assert.Equal(string.Empty, fields[1].Booster);
        Assert.Equal(EkomSearchType.Exact, fields[1].SearchType);
    }

    [Fact]
    public void PublicRequestsCannotChooseIndexOrUnlistedFields()
    {
        var request = new SearchRequest
        {
            SearchQuery = new string('a', 500),
            ExamineIndex = "MembersIndex",
            SearchFields =
            [
                new EkomSearchField { Name = "TITLE" },
                new EkomSearchField { Name = "internalCostPrice" },
            ],
        };

        SearchRequestGuard.RestrictToPublic(request, Allowed);

        Assert.Equal(string.Empty, request.ExamineIndex);
        Assert.Equal("TITLE", Assert.Single(request.SearchFields).Name);
        Assert.Equal(SearchRequestGuard.MaxPublicSearchQueryLength, request.SearchQuery.Length);
    }

    [Fact]
    public void PublicRequestWithOnlyUnlistedFieldsFallsBackToServiceDefaults()
    {
        var query = new SearchRequest { SearchFields = [new EkomSearchField { Name = "internalCostPrice" }] };

        SearchRequestGuard.RestrictToPublic(query, Allowed);

        Assert.Empty(query.SearchFields);
    }
}

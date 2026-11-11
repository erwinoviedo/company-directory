using Companies.Application.Companies;

namespace Companies.UnitTests;

public class CompanySearchRankerTests
{
    [Fact]
    public void Tiers_rank_from_exact_name_down_to_contains()
    {
        var exact = TestData.Company("Acme", "https://acme.com");
        var namePrefix = TestData.Company("Acme Tools", "https://acme-tools.com");
        var wordPrefix = TestData.Company("Northern Acme Supplies", "https://northernacmesupplies.com");
        var contains = TestData.Company("Pinnacmex", "https://pinnacmex.com");
        var unrelated = TestData.Company("Globex", "https://globex.com");

        Assert.Equal(CompanySearchRanker.ExactName, CompanySearchRanker.Score(exact, "acme"));
        Assert.Equal(CompanySearchRanker.NamePrefix, CompanySearchRanker.Score(namePrefix, "acme"));
        Assert.Equal(CompanySearchRanker.WordPrefix, CompanySearchRanker.Score(wordPrefix, "acme"));
        Assert.Equal(CompanySearchRanker.NameContains, CompanySearchRanker.Score(contains, "acme"));
        Assert.Equal(0, CompanySearchRanker.Score(unrelated, "acme"));
    }

    [Theory]
    [InlineData("ACME tools")]
    [InlineData("  acme   tools ")]
    [InlineData("Acme-Tools")]
    public void Matching_ignores_case_spacing_and_punctuation(string query)
    {
        var company = TestData.Company("Acme Tools", "https://acme-tools.com");

        Assert.Equal(CompanySearchRanker.ExactName, CompanySearchRanker.Score(company, query));
    }

    [Theory]
    [InlineData("bigriver.com")]
    [InlineData("www.bigriver.com")]
    [InlineData("https://www.bigriver.com/about")]
    public void Query_that_is_the_companys_domain_ranks_just_below_an_exact_name(string query)
    {
        var company = TestData.Company("Big River Trading", "https://www.bigriver.com");

        Assert.Equal(CompanySearchRanker.ExactDomain, CompanySearchRanker.Score(company, query));
    }

    [Fact]
    public void Query_matching_only_the_domain_still_finds_the_company()
    {
        var company = TestData.Company("International Business Machines", "https://www.ibm.com");

        Assert.Equal(CompanySearchRanker.ExactDomain, CompanySearchRanker.Score(company, "ibm"));
        Assert.Equal(CompanySearchRanker.DomainPrefix, CompanySearchRanker.Score(company, "ib"));
    }

    [Fact]
    public void Misspelled_query_matches_with_a_low_score()
    {
        var company = TestData.Company("Microsoft", "https://microsoft.com");

        var score = CompanySearchRanker.Score(company, "microsfot");

        Assert.InRange(score, 0.01, CompanySearchRanker.FuzzyWeight);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("!!!")]
    public void Query_without_letters_or_digits_matches_nothing(string query)
    {
        var company = TestData.Company("Acme", "https://acme.com");

        Assert.Equal(0, CompanySearchRanker.Score(company, query));
    }
}

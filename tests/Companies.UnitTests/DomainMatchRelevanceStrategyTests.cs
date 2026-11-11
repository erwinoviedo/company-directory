using Companies.Application.Relevance;

namespace Companies.UnitTests;

public class DomainMatchRelevanceStrategyTests
{
    private readonly DomainMatchRelevanceStrategy _strategy = TestData.RelevanceStrategy();

    [Theory]
    [InlineData("Acme", "https://www.acme.com")]
    [InlineData("ACME Inc.", "https://acme.com")]
    [InlineData("Verus LLC", "https://www.verus.com")]
    [InlineData("Acme Tools Ltd", "https://www.acme-tools.co.uk")]
    [InlineData("The Bank of America Corporation", "https://www.bankofamerica.com")]
    [InlineData("Café Münster", "https://cafemunster.de")]
    [InlineData("McDonald's", "https://www.mcdonalds.com")]
    public void Name_that_is_the_domain_label_is_an_exact_match(string name, string url)
    {
        var assessment = _strategy.Assess(name, TestData.Website(url));

        Assert.True(assessment.IsRelevant);
        Assert.Equal(1.0, assessment.Score);
    }

    [Theory]
    [InlineData("Microsoft Azure Cloud Services", "https://microsoft.com")]
    [InlineData("PayPal Holdings Europe", "https://www.paypal.com")]
    public void Domain_made_of_the_leading_words_of_the_name_is_relevant(string name, string url)
    {
        var assessment = _strategy.Assess(name, TestData.Website(url));

        Assert.True(assessment.IsRelevant);
        Assert.Equal(DomainMatchRelevanceStrategy.LeadingWordsScore, assessment.Score);
    }

    [Theory]
    [InlineData("International Business Machines", "https://www.ibm.com")]
    [InlineData("Bank of America", "https://www.boa.com")]
    [InlineData("Hewlett Packard", "https://hp.com")]
    public void Domain_made_of_the_initials_of_the_name_is_relevant(string name, string url)
    {
        var assessment = _strategy.Assess(name, TestData.Website(url));

        Assert.True(assessment.IsRelevant);
        Assert.Equal(DomainMatchRelevanceStrategy.AcronymScore, assessment.Score);
    }

    [Theory]
    [InlineData("AT&T", "https://www.att.com")]
    [InlineData("I.B.M.", "https://www.ibm.com")]
    public void Initialisms_written_with_punctuation_match_their_domain(string name, string url)
    {
        Assert.True(_strategy.Assess(name, TestData.Website(url)).IsRelevant);
    }

    [Theory]
    [InlineData("Verus", "https://www.verusllc.com")] // domain adds a suffix to the name
    [InlineData("Acme", "https://www.acme-tools.com")] // name is one of the domain's words
    public void Name_sharing_most_of_its_words_with_the_domain_is_relevant(string name, string url)
    {
        Assert.True(_strategy.Assess(name, TestData.Website(url)).IsRelevant);
    }

    [Theory]
    [InlineData("Tumbler", "https://tumblr.com")]
    [InlineData("Mircosoft", "https://microsoft.com")]
    public void Near_miss_spelling_is_relevant(string name, string url)
    {
        Assert.True(_strategy.Assess(name, TestData.Website(url)).IsRelevant);
    }

    [Theory]
    [InlineData("Globex", "https://www.acme.com")]
    [InlineData("Google", "https://www.microsoft.com")]
    [InlineData("Art", "https://smartphones.com")] // contained, but explains little of the domain
    [InlineData("Bolt", "https://volt.com")] // too short for fuzzy matching to be trustworthy
    [InlineData("Alphabet", "https://abc.xyz")] // documented limitation of domain matching
    public void Unrelated_name_is_not_relevant(string name, string url)
    {
        var assessment = _strategy.Assess(name, TestData.Website(url));

        Assert.False(assessment.IsRelevant);
        Assert.True(assessment.Score < 0.7, $"score was {assessment.Score}");
    }

    [Fact]
    public void Threshold_is_configurable()
    {
        var website = TestData.Website("https://www.acme-tools.com");
        var lenient = TestData.RelevanceStrategy(threshold: 0.5).Assess("Acme", website);
        var strict = TestData.RelevanceStrategy(threshold: 0.95).Assess("Acme", website);

        Assert.Equal(lenient.Score, strict.Score);
        Assert.True(lenient.IsRelevant);
        Assert.False(strict.IsRelevant);
    }

    [Fact]
    public void Assessment_names_the_signal_that_decided_it()
    {
        var assessment = _strategy.Assess("International Business Machines", TestData.Website("https://ibm.com"));

        Assert.Contains("acronym", assessment.Signal);
    }
}

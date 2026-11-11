using Companies.Application.Text;
using Companies.Domain;

namespace Companies.Application.Companies;

/// <summary>
/// Scores how well a company answers a free-text search. Higher is better; 0 means "not a result".
/// The tiers are deliberately simple so the ordering can be explained to a user:
/// exact name, exact domain, name starts with, a word starts with, domain starts with,
/// name contains, domain contains, and finally a near-miss spelling.
/// </summary>
internal static class CompanySearchRanker
{
    internal const double ExactName = 1.0;
    internal const double ExactDomain = 0.95;
    internal const double NamePrefix = 0.9;
    internal const double WordPrefix = 0.8;
    internal const double DomainPrefix = 0.75;
    internal const double NameContains = 0.6;
    internal const double DomainContains = 0.55;
    internal const double FuzzyWeight = 0.5;

    private const double MinFuzzySimilarity = 0.7;
    private const int MinFuzzyLength = 4;

    public static double Score(Company company, string query)
    {
        var queryWords = TextNormalizer.Tokenize(query);
        if (queryWords.Count == 0)
        {
            return 0;
        }

        var queryText = string.Join(' ', queryWords);
        var queryCompact = TextNormalizer.Compact(queryWords);
        var queryHost = WebsiteAddress.NormalizeHostInput(query) ?? queryCompact;

        var nameWords = TextNormalizer.Tokenize(company.Name);
        var nameText = string.Join(' ', nameWords);
        var website = company.Website;

        if (nameText == queryText)
        {
            return ExactName;
        }

        if (queryHost == website.Domain || queryHost == website.Host || queryCompact == website.DomainLabel)
        {
            return ExactDomain;
        }

        if (nameText.StartsWith(queryText, StringComparison.Ordinal))
        {
            return NamePrefix;
        }

        if (nameText.Contains(' ' + queryText, StringComparison.Ordinal))
        {
            return WordPrefix;
        }

        if (website.Host.StartsWith(queryHost, StringComparison.Ordinal)
            || website.Domain.StartsWith(queryHost, StringComparison.Ordinal))
        {
            return DomainPrefix;
        }

        if (nameText.Contains(queryText, StringComparison.Ordinal))
        {
            return NameContains;
        }

        if (website.Host.Contains(queryHost, StringComparison.Ordinal))
        {
            return DomainContains;
        }

        return FuzzyScore(queryCompact, nameWords, website.DomainLabel);
    }

    private static double FuzzyScore(string queryCompact, IReadOnlyList<string> nameWords, string domainLabel)
    {
        if (queryCompact.Length < MinFuzzyLength)
        {
            return 0;
        }

        var candidates = nameWords.Append(TextNormalizer.Compact(nameWords)).Append(domainLabel);
        var best = candidates.Max(candidate => StringSimilarity.Levenshtein(queryCompact, candidate));
        return best >= MinFuzzySimilarity ? best * FuzzyWeight : 0;
    }
}

using Companies.Application.Text;
using Companies.Domain;
using Microsoft.Extensions.Options;

namespace Companies.Application.Relevance;

/// <summary>
/// Compares the company name with the website's domain label ("acme" in www.acme.co.uk).
/// Five independent signals each produce a score; the strongest one wins.
/// <list type="number">
/// <item>Exact: the name, without legal suffix, is the domain label. Score 1.0.</item>
/// <item>Leading words: the domain label is the first word(s) of the name. Score 0.9.</item>
/// <item>Acronym: the initials of the name are the domain label. Score 0.85.</item>
/// <item>Word coverage: how much of the name appears in the domain and vice versa. Score 0 to 1.</item>
/// <item>Fuzzy: edit-distance similarity, to tolerate typos and dropped letters. Score 0 to 1.</item>
/// </list>
/// Works offline and deterministically. Known limit: brands whose domain shares no text
/// with the name (Alphabet and abc.xyz) are rejected.
/// </summary>
public sealed class DomainMatchRelevanceStrategy(IOptions<RelevanceOptions> options) : ICompanyRelevanceStrategy
{
    internal const double LeadingWordsScore = 0.9;
    internal const double AcronymScore = 0.85;

    private const int MinCoveredWordLength = 3;
    private const int MinFuzzyLength = 5;

    private readonly double _threshold = options.Value.Threshold;

    public RelevanceAssessment Assess(string companyName, WebsiteAddress website)
    {
        var name = CompanyNameTokens.From(companyName);
        var domain = TextNormalizer.Compact(TextNormalizer.Tokenize(website.DomainLabel));

        if (name.Words.Count == 0 || domain.Length == 0)
        {
            return new RelevanceAssessment(0, false, "nothing to compare");
        }

        (double Score, string Signal)[] signals =
        [
            (Exact(name, domain), "exact match"),
            (LeadingWords(name, domain), "domain is the leading words of the name"),
            (Acronym(name, domain), "domain is the acronym of the name"),
            (WordCoverage(name, domain), "words shared between name and domain"),
            (Fuzzy(name, domain), "similar spelling"),
        ];

        var best = signals.MaxBy(signal => signal.Score);
        var score = Math.Round(best.Score, 2);
        return new RelevanceAssessment(score, score >= _threshold, score > 0 ? best.Signal : "no match");
    }

    private static double Exact(CompanyNameTokens name, string domain) =>
        TextNormalizer.Compact(name.Words) == domain || TextNormalizer.Compact(name.Significant) == domain ? 1 : 0;

    private static double LeadingWords(CompanyNameTokens name, string domain) =>
        IsLeadingWords(name.Words, domain) || IsLeadingWords(name.Significant, domain) ? LeadingWordsScore : 0;

    private static bool IsLeadingWords(IReadOnlyList<string> words, string domain)
    {
        var prefix = string.Empty;
        foreach (var word in words)
        {
            prefix += word;
            if (prefix == domain)
            {
                return true;
            }

            if (prefix.Length >= domain.Length)
            {
                return false;
            }
        }

        return false;
    }

    private static double Acronym(CompanyNameTokens name, string domain) =>
        IsAcronym(name.Words, domain) || IsAcronym(name.Significant, domain) ? AcronymScore : 0;

    private static bool IsAcronym(IReadOnlyList<string> words, string domain) =>
        words.Count >= 2 && string.Concat(words.Select(word => word[0])) == domain;

    /// <summary>
    /// Average of two ratios: the share of the name's letters found in the domain, and the share
    /// of the domain's letters explained by the name. Both must be high for a strong score, so
    /// "Art" does not match smartphones.com even though "art" appears inside it.
    /// </summary>
    private static double WordCoverage(CompanyNameTokens name, string domain)
    {
        var nameLength = name.Significant.Sum(word => word.Length);
        var coveredLength = name.Significant
            .Distinct()
            .Where(word => word.Length >= MinCoveredWordLength && domain.Contains(word, StringComparison.Ordinal))
            .Sum(word => word.Length);

        if (coveredLength == 0 || nameLength == 0)
        {
            return 0;
        }

        var nameCoverage = Math.Min(1, (double)coveredLength / nameLength);
        var domainCoverage = Math.Min(1, (double)coveredLength / domain.Length);
        return (nameCoverage + domainCoverage) / 2;
    }

    private static double Fuzzy(CompanyNameTokens name, string domain)
    {
        var compactName = TextNormalizer.Compact(name.Significant);

        // Very short strings are one edit away from too many unrelated words ("bolt" / "volt").
        return compactName.Length < MinFuzzyLength || domain.Length < MinFuzzyLength
            ? 0
            : StringSimilarity.Levenshtein(compactName, domain);
    }
}

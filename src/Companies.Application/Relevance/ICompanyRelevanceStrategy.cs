using Companies.Domain;

namespace Companies.Application.Relevance;

/// <summary>
/// Decides whether a company name plausibly belongs to a website.
/// Swap the implementation (for example, one that inspects page content) without touching callers.
/// </summary>
public interface ICompanyRelevanceStrategy
{
    RelevanceAssessment Assess(string companyName, WebsiteAddress website);
}

/// <param name="Score">0 (unrelated) to 1 (certain match).</param>
/// <param name="IsRelevant">True when the score reaches the configured threshold.</param>
/// <param name="Signal">The rule that produced the score, for logs and explanations.</param>
public sealed record RelevanceAssessment(double Score, bool IsRelevant, string Signal);

public sealed class RelevanceOptions
{
    public const string SectionName = "Relevance";

    /// <summary>Minimum score for a name to be accepted for a website.</summary>
    public double Threshold { get; set; } = 0.7;
}

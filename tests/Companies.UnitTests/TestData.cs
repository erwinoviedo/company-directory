using Companies.Application.Companies;
using Companies.Application.Relevance;
using Companies.Domain;
using Microsoft.Extensions.Options;

namespace Companies.UnitTests;

internal static class TestData
{
    public static readonly DateTimeOffset Now = new(2026, 1, 15, 9, 30, 0, TimeSpan.Zero);

    public static WebsiteAddress Website(string url) =>
        WebsiteAddress.TryParse(url, out var website)
            ? website
            : throw new ArgumentException($"'{url}' is not a valid website for a test.", nameof(url));

    public static Company Company(string name, string url) =>
        Domain.Company.Create(name, Website(url), Now);

    public static DomainMatchRelevanceStrategy RelevanceStrategy(double threshold = 0.7) =>
        new(Options.Create(new RelevanceOptions { Threshold = threshold }));

    public static CompanyValidator Validator() => new(RelevanceStrategy());
}

/// <summary>A clock the test controls.</summary>
internal sealed class FixedClock(DateTimeOffset now) : TimeProvider
{
    public DateTimeOffset Now { get; set; } = now;

    public override DateTimeOffset GetUtcNow() => Now;
}

using System.Diagnostics.CodeAnalysis;

namespace Companies.Domain;

/// <summary>
/// A validated company website. Knows how to split its host into the parts the rest of the
/// application reasons about: the registrable domain ("acme.co.uk") and its label ("acme").
/// </summary>
public sealed record WebsiteAddress
{
    public const int MaxLength = 2048;

    // Two-level public suffixes we recognise. A production system would use the full
    // Public Suffix List (publicsuffix.org); this short list keeps the exercise dependency-free.
    private static readonly HashSet<string> TwoLevelSuffixes = new(StringComparer.OrdinalIgnoreCase)
    {
        "co.uk", "org.uk", "ac.uk", "gov.uk", "com.au", "net.au", "org.au", "co.nz", "co.za",
        "co.jp", "co.kr", "co.in", "com.br", "com.mx", "com.ar", "com.co", "com.pe", "com.ec",
        "com.cn", "com.sg", "com.tr", "com.hk",
    };

    private WebsiteAddress(Uri url, string host, string domain, string domainLabel)
    {
        Url = url;
        Host = host;
        Domain = domain;
        DomainLabel = domainLabel;
    }

    /// <summary>The full URL as submitted, in canonical form.</summary>
    public Uri Url { get; }

    /// <summary>Host without a leading "www.", lower case. Example: "shop.acme.co.uk".</summary>
    public string Host { get; }

    /// <summary>Registrable domain, lower case. Example: "acme.co.uk".</summary>
    public string Domain { get; }

    /// <summary>The registrable domain without its public suffix. Example: "acme".</summary>
    public string DomainLabel { get; }

    /// <summary>
    /// Parses an absolute http/https URL whose host is a domain name.
    /// IP addresses and single-label hosts such as "localhost" are rejected.
    /// </summary>
    public static bool TryParse(string? value, [NotNullWhen(true)] out WebsiteAddress? website)
    {
        website = null;

        if (string.IsNullOrWhiteSpace(value) || value.Length > MaxLength)
        {
            return false;
        }

        if (!Uri.TryCreate(value.Trim(), UriKind.Absolute, out var url))
        {
            return false;
        }

        if (url.Scheme != Uri.UriSchemeHttp && url.Scheme != Uri.UriSchemeHttps)
        {
            return false;
        }

        if (url.HostNameType != UriHostNameType.Dns)
        {
            return false;
        }

        var host = NormalizeHost(url.IdnHost);
        var labels = host.Split('.', StringSplitOptions.RemoveEmptyEntries);
        if (labels.Length < 2 || labels.Any(label => label.Length == 0))
        {
            return false;
        }

        var suffixLabelCount = HasTwoLevelSuffix(labels) ? 2 : 1;
        if (labels.Length <= suffixLabelCount)
        {
            return false;
        }

        var domainLabel = labels[^(suffixLabelCount + 1)];
        var domain = string.Join('.', labels[^(suffixLabelCount + 1)..]);

        website = new WebsiteAddress(url, host, domain, domainLabel);
        return true;
    }

    /// <summary>
    /// Reduces user input such as "https://www.Acme.com/about", "www.acme.com" or "ACME.com"
    /// to a comparable host ("acme.com"). Returns null when nothing usable remains.
    /// </summary>
    public static string? NormalizeHostInput(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var text = value.Trim();
        if (Uri.TryCreate(text, UriKind.Absolute, out var url) && !string.IsNullOrEmpty(url.Host))
        {
            return NormalizeHost(url.IdnHost);
        }

        var hostPart = text.Split('/', 2)[0];
        return hostPart.Length == 0 ? null : NormalizeHost(hostPart);
    }

    public override string ToString() => Url.AbsoluteUri;

    private static string NormalizeHost(string host)
    {
        var lowered = host.Trim().TrimEnd('.').ToLowerInvariant();
        return lowered.StartsWith("www.", StringComparison.Ordinal) ? lowered[4..] : lowered;
    }

    private static bool HasTwoLevelSuffix(string[] labels) =>
        labels.Length >= 2 && TwoLevelSuffixes.Contains($"{labels[^2]}.{labels[^1]}");
}

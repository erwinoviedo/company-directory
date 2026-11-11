using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
using Companies.Domain;

namespace Companies.Application.Companies;

// The annotations below are a cheap first guard at the API boundary (missing or oversized input).
// The full rules, including name/website relevance, live in CompanyValidator.

public sealed record CreateCompanyRequest(
    [Required(ErrorMessage = "Company name is required.")]
    [StringLength(Company.NameMaxLength, ErrorMessage = "Company name must be at most {1} characters.")]
    string? Name,
    [Required(ErrorMessage = "Website URL is required.")]
    [StringLength(WebsiteAddress.MaxLength, ErrorMessage = "Website URL must be at most {1} characters.")]
    string? WebsiteUrl);

public sealed record UpdateCompanyRequest(
    [Required(ErrorMessage = "Company name is required.")]
    [StringLength(Company.NameMaxLength, ErrorMessage = "Company name must be at most {1} characters.")]
    string? Name,
    [Required(ErrorMessage = "Website URL is required.")]
    [StringLength(WebsiteAddress.MaxLength, ErrorMessage = "Website URL must be at most {1} characters.")]
    string? WebsiteUrl);

/// <param name="Domain">Registrable domain of the website, for example "acme.co.uk".</param>
/// <param name="RelevanceScore">Present only on free-text searches: how well this company matches the query (0 to 1).</param>
public sealed record CompanyResponse(
    Guid Id,
    string Name,
    string WebsiteUrl,
    string Domain,
    DateTimeOffset CreatedAt,
    DateTimeOffset? UpdatedAt,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    double? RelevanceScore = null);

/// <summary>Query-string contract for listing and searching companies.</summary>
public sealed record CompanyQuery
{
    public const int DefaultPageSize = 50;
    public const int MaxPageSize = 100;

    /// <summary>Filter: company name contains this text (case-insensitive).</summary>
    [StringLength(Company.NameMaxLength, ErrorMessage = "Name filter must be at most {1} characters.")]
    public string? Name { get; init; }

    /// <summary>Filter: website is on this domain, for example "acme.com".</summary>
    [StringLength(253, ErrorMessage = "Domain filter must be at most {1} characters.")]
    public string? Domain { get; init; }

    /// <summary>Free-text search over name and domain. Results are ordered by relevance.</summary>
    [StringLength(Company.NameMaxLength, ErrorMessage = "Search text must be at most {1} characters.")]
    public string? Q { get; init; }

    [Range(1, int.MaxValue, ErrorMessage = "Page must be 1 or greater.")]
    public int Page { get; init; } = 1;

    [Range(1, MaxPageSize, ErrorMessage = "Page size must be between {1} and {2}.")]
    public int PageSize { get; init; } = DefaultPageSize;
}

public sealed record PagedResponse<T>(IReadOnlyList<T> Items, int Page, int PageSize, int TotalCount);

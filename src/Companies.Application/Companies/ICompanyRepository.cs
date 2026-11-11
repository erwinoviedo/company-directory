using Companies.Domain;

namespace Companies.Application.Companies;

/// <summary>
/// Storage port for companies. The contract is asynchronous and speaks only in domain types,
/// so an EF Core or Dapper implementation can replace the in-memory one without other changes.
/// </summary>
public interface ICompanyRepository
{
    Task<Company?> GetByIdAsync(Guid id, CancellationToken cancellationToken);

    /// <summary>Returns the companies matching the filter. An empty filter returns everything.</summary>
    Task<IReadOnlyList<Company>> ListAsync(CompanyFilter filter, CancellationToken cancellationToken);

    Task<SaveOutcome> AddAsync(Company company, CancellationToken cancellationToken);

    Task<SaveOutcome> UpdateAsync(Company company, CancellationToken cancellationToken);

    /// <summary>Returns false when no company has that id.</summary>
    Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken);
}

/// <param name="NameContains">Case-insensitive fragment of the company name.</param>
/// <param name="Host">Normalised host ("acme.com"); matches that host and its subdomains.</param>
public sealed record CompanyFilter(string? NameContains = null, string? Host = null);

public enum SaveOutcome
{
    Saved,
    NotFound,

    /// <summary>Another company already uses the same website host (the unique-key violation of a database).</summary>
    DuplicateWebsite,
}

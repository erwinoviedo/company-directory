using Companies.Application.Companies;
using Companies.Domain;

namespace Companies.Infrastructure.Persistence;

/// <summary>
/// Keeps companies in process memory. Registered as a singleton so data lives as long as the app.
/// The dictionary never leaves this class: callers receive immutable <see cref="Company"/> values
/// and fresh lists. A lock (rather than ConcurrentDictionary) keeps the "website must be unique"
/// check and the write atomic, the way a unique index would in a database.
/// </summary>
public sealed class InMemoryCompanyRepository : ICompanyRepository
{
    private readonly Dictionary<Guid, Company> _companies = [];
    private readonly Lock _gate = new();

    public Task<Company?> GetByIdAsync(Guid id, CancellationToken cancellationToken)
    {
        lock (_gate)
        {
            return Task.FromResult(_companies.GetValueOrDefault(id));
        }
    }

    public Task<IReadOnlyList<Company>> ListAsync(CompanyFilter filter, CancellationToken cancellationToken)
    {
        lock (_gate)
        {
            IReadOnlyList<Company> matches = _companies.Values.Where(company => Matches(company, filter)).ToList();
            return Task.FromResult(matches);
        }
    }

    public Task<SaveOutcome> AddAsync(Company company, CancellationToken cancellationToken)
    {
        lock (_gate)
        {
            if (WebsiteIsTaken(company))
            {
                return Task.FromResult(SaveOutcome.DuplicateWebsite);
            }

            _companies.Add(company.Id, company);
            return Task.FromResult(SaveOutcome.Saved);
        }
    }

    public Task<SaveOutcome> UpdateAsync(Company company, CancellationToken cancellationToken)
    {
        lock (_gate)
        {
            if (!_companies.ContainsKey(company.Id))
            {
                return Task.FromResult(SaveOutcome.NotFound);
            }

            if (WebsiteIsTaken(company))
            {
                return Task.FromResult(SaveOutcome.DuplicateWebsite);
            }

            _companies[company.Id] = company;
            return Task.FromResult(SaveOutcome.Saved);
        }
    }

    public Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken)
    {
        lock (_gate)
        {
            return Task.FromResult(_companies.Remove(id));
        }
    }

    private bool WebsiteIsTaken(Company company) =>
        _companies.Values.Any(other => other.Id != company.Id && other.Website.Host == company.Website.Host);

    private static bool Matches(Company company, CompanyFilter filter)
    {
        if (filter.NameContains is { Length: > 0 } name
            && !company.Name.Contains(name, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (filter.Host is { Length: > 0 } host)
        {
            var website = company.Website;
            return website.Host == host
                || website.Domain == host
                || website.Host.EndsWith('.' + host, StringComparison.Ordinal);
        }

        return true;
    }
}

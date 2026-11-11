using Companies.Application.Common;

namespace Companies.Application.Companies;

/// <summary>
/// Use cases for companies. Controllers call this and nothing else.
/// </summary>
public interface ICompanyService
{
    Task<Result<CompanyResponse>> CreateAsync(CreateCompanyRequest request, CancellationToken cancellationToken);

    Task<Result<CompanyResponse>> GetByIdAsync(Guid id, CancellationToken cancellationToken);

    /// <summary>
    /// Lists companies, optionally filtered by name and domain. With a free-text query the results
    /// are ordered by relevance; otherwise by name. No match is an empty page, not an error.
    /// </summary>
    Task<PagedResponse<CompanyResponse>> SearchAsync(CompanyQuery query, CancellationToken cancellationToken);

    Task<Result<CompanyResponse>> UpdateAsync(Guid id, UpdateCompanyRequest request, CancellationToken cancellationToken);

    Task<Result> DeleteAsync(Guid id, CancellationToken cancellationToken);
}

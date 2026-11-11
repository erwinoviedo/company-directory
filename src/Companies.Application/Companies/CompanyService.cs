using Companies.Application.Common;
using Companies.Domain;
using Microsoft.Extensions.Logging;

namespace Companies.Application.Companies;

public sealed partial class CompanyService(
    ICompanyRepository repository,
    ICompanyValidator validator,
    TimeProvider clock,
    ILogger<CompanyService> logger) : ICompanyService
{
    public async Task<Result<CompanyResponse>> CreateAsync(CreateCompanyRequest request, CancellationToken cancellationToken)
    {
        var validation = validator.Validate(request.Name, request.WebsiteUrl);
        if (!validation.IsValid)
        {
            var error = Error.Validation(validation.Errors);
            Log.CompanyRejected(logger, request.Name, request.WebsiteUrl, error.Message);
            return error;
        }

        var company = Company.Create(validation.Input.Name, validation.Input.Website, clock.GetUtcNow());

        var outcome = await repository.AddAsync(company, cancellationToken);
        if (outcome == SaveOutcome.DuplicateWebsite)
        {
            Log.DuplicateWebsite(logger, company.Website.Host);
            return DuplicateWebsite(company.Website);
        }

        Log.CompanyCreated(logger, company.Id, company.Name, company.Website.Domain);
        return ToResponse(company);
    }

    public async Task<Result<CompanyResponse>> GetByIdAsync(Guid id, CancellationToken cancellationToken)
    {
        var company = await repository.GetByIdAsync(id, cancellationToken);
        if (company is null)
        {
            Log.CompanyNotFound(logger, id);
            return NotFound(id);
        }

        return ToResponse(company);
    }

    public async Task<PagedResponse<CompanyResponse>> SearchAsync(CompanyQuery query, CancellationToken cancellationToken)
    {
        var filter = new CompanyFilter(
            NameContains: string.IsNullOrWhiteSpace(query.Name) ? null : query.Name.Trim(),
            Host: WebsiteAddress.NormalizeHostInput(query.Domain));

        var candidates = await repository.ListAsync(filter, cancellationToken);

        var ordered = string.IsNullOrWhiteSpace(query.Q)
            ? candidates
                .OrderBy(company => company.Name, StringComparer.OrdinalIgnoreCase)
                .Select(company => ToResponse(company))
                .ToList()
            : candidates
                .Select(company => (Company: company, Score: CompanySearchRanker.Score(company, query.Q)))
                .Where(match => match.Score > 0)
                .OrderByDescending(match => match.Score)
                .ThenBy(match => match.Company.Name, StringComparer.OrdinalIgnoreCase)
                .Select(match => ToResponse(match.Company, Math.Round(match.Score, 2)))
                .ToList();

        var page = ordered
            .Skip((query.Page - 1) * query.PageSize)
            .Take(query.PageSize)
            .ToList();

        Log.CompaniesSearched(logger, query.Name, query.Domain, query.Q, ordered.Count);
        return new PagedResponse<CompanyResponse>(page, query.Page, query.PageSize, ordered.Count);
    }

    public async Task<Result<CompanyResponse>> UpdateAsync(Guid id, UpdateCompanyRequest request, CancellationToken cancellationToken)
    {
        var existing = await repository.GetByIdAsync(id, cancellationToken);
        if (existing is null)
        {
            Log.CompanyNotFound(logger, id);
            return NotFound(id);
        }

        var validation = validator.Validate(request.Name, request.WebsiteUrl);
        if (!validation.IsValid)
        {
            var error = Error.Validation(validation.Errors);
            Log.CompanyRejected(logger, request.Name, request.WebsiteUrl, error.Message);
            return error;
        }

        var updated = existing.WithDetails(validation.Input.Name, validation.Input.Website, clock.GetUtcNow());

        switch (await repository.UpdateAsync(updated, cancellationToken))
        {
            case SaveOutcome.NotFound: // deleted between the read and the write
                Log.CompanyNotFound(logger, id);
                return NotFound(id);
            case SaveOutcome.DuplicateWebsite:
                Log.DuplicateWebsite(logger, updated.Website.Host);
                return DuplicateWebsite(updated.Website);
            default:
                Log.CompanyUpdated(logger, id);
                return ToResponse(updated);
        }
    }

    public async Task<Result> DeleteAsync(Guid id, CancellationToken cancellationToken)
    {
        if (!await repository.DeleteAsync(id, cancellationToken))
        {
            Log.CompanyNotFound(logger, id);
            return NotFound(id);
        }

        Log.CompanyDeleted(logger, id);
        return Result.Success();
    }

    private static CompanyResponse ToResponse(Company company, double? relevanceScore = null) => new(
        company.Id,
        company.Name,
        company.Website.Url.AbsoluteUri,
        company.Website.Domain,
        company.CreatedAt,
        company.UpdatedAt,
        relevanceScore);

    private static Error NotFound(Guid id) => Error.NotFound($"No company exists with id '{id}'.");

    private static Error DuplicateWebsite(WebsiteAddress website) =>
        Error.Conflict($"A company with the website '{website.Host}' already exists.");

    private static partial class Log
    {
        [LoggerMessage(EventId = 1000, Level = LogLevel.Information,
            Message = "Company {CompanyId} created: '{Name}' on {Domain}")]
        public static partial void CompanyCreated(ILogger logger, Guid companyId, string name, string domain);

        [LoggerMessage(EventId = 1001, Level = LogLevel.Information, Message = "Company {CompanyId} updated")]
        public static partial void CompanyUpdated(ILogger logger, Guid companyId);

        [LoggerMessage(EventId = 1002, Level = LogLevel.Information, Message = "Company {CompanyId} deleted")]
        public static partial void CompanyDeleted(ILogger logger, Guid companyId);

        [LoggerMessage(EventId = 1003, Level = LogLevel.Debug,
            Message = "Company search (name: '{Name}', domain: '{Domain}', q: '{Query}') matched {Count} companies")]
        public static partial void CompaniesSearched(ILogger logger, string? name, string? domain, string? query, int count);

        [LoggerMessage(EventId = 1100, Level = LogLevel.Warning,
            Message = "Company rejected (name: '{Name}', website: '{WebsiteUrl}'): {Reason}")]
        public static partial void CompanyRejected(ILogger logger, string? name, string? websiteUrl, string reason);

        [LoggerMessage(EventId = 1101, Level = LogLevel.Warning,
            Message = "Company rejected: website {Host} is already registered")]
        public static partial void DuplicateWebsite(ILogger logger, string host);

        [LoggerMessage(EventId = 1102, Level = LogLevel.Information, Message = "Company {CompanyId} not found")]
        public static partial void CompanyNotFound(ILogger logger, Guid companyId);
    }
}

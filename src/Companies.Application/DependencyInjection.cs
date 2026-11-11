using Companies.Application.Companies;
using Companies.Application.Relevance;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Companies.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddOptions<RelevanceOptions>();
        services.TryAddSingleton(TimeProvider.System);
        services.AddSingleton<ICompanyRelevanceStrategy, DomainMatchRelevanceStrategy>();
        services.AddSingleton<ICompanyValidator, CompanyValidator>();
        services.AddScoped<ICompanyService, CompanyService>();
        return services;
    }
}

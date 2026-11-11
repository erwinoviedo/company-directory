using Companies.Application.Companies;
using Companies.Infrastructure.Persistence;
using Microsoft.Extensions.DependencyInjection;

namespace Companies.Infrastructure;

public static class DependencyInjection
{
    /// <summary>
    /// Introducing a database later means registering a different <see cref="ICompanyRepository"/> here.
    /// </summary>
    public static IServiceCollection AddInfrastructure(this IServiceCollection services)
    {
        services.AddSingleton<ICompanyRepository, InMemoryCompanyRepository>();
        return services;
    }
}

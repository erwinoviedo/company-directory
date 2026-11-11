using Companies.Application.Companies;
using Companies.Infrastructure.Persistence;

namespace Companies.UnitTests;

public class InMemoryCompanyRepositoryTests
{
    private static readonly CancellationToken None = CancellationToken.None;

    private readonly InMemoryCompanyRepository _repository = new();

    [Fact]
    public async Task Listing_returns_a_snapshot_that_later_writes_do_not_change()
    {
        await _repository.AddAsync(TestData.Company("Acme", "https://acme.com"), None);

        var snapshot = await _repository.ListAsync(new CompanyFilter(), None);
        await _repository.AddAsync(TestData.Company("Globex", "https://globex.com"), None);

        Assert.Single(snapshot);
        Assert.Equal(2, (await _repository.ListAsync(new CompanyFilter(), None)).Count);
    }

    [Fact]
    public async Task Website_host_is_unique_across_companies()
    {
        await _repository.AddAsync(TestData.Company("Acme", "https://www.acme.com"), None);

        var sameHost = await _repository.AddAsync(TestData.Company("Acme Inc", "http://acme.com/en"), None);
        var subdomain = await _repository.AddAsync(TestData.Company("Acme Shop", "https://shop.acme.com"), None);

        Assert.Equal(SaveOutcome.DuplicateWebsite, sameHost);
        Assert.Equal(SaveOutcome.Saved, subdomain);
    }

    [Fact]
    public async Task Domain_filter_matches_the_domain_and_its_subdomains_only()
    {
        await _repository.AddAsync(TestData.Company("Acme", "https://www.acme.com"), None);
        await _repository.AddAsync(TestData.Company("Acme Shop", "https://shop.acme.com"), None);
        await _repository.AddAsync(TestData.Company("Not Acme", "https://notacme.com"), None);

        var matches = await _repository.ListAsync(new CompanyFilter(Host: "acme.com"), None);

        Assert.Equal(["Acme", "Acme Shop"], matches.Select(c => c.Name).Order());
    }

    [Fact]
    public async Task Concurrent_adds_for_the_same_website_store_exactly_one()
    {
        var attempts = Enumerable.Range(0, 50).Select(_ =>
            Task.Run(() => _repository.AddAsync(TestData.Company("Acme", "https://acme.com"), None)));

        var outcomes = await Task.WhenAll(attempts);

        Assert.Single(outcomes, outcome => outcome == SaveOutcome.Saved);
        Assert.Single(await _repository.ListAsync(new CompanyFilter(), None));
    }

    [Fact]
    public async Task Update_and_delete_report_a_missing_company()
    {
        var ghost = TestData.Company("Acme", "https://acme.com");

        Assert.Equal(SaveOutcome.NotFound, await _repository.UpdateAsync(ghost, None));
        Assert.False(await _repository.DeleteAsync(ghost.Id, None));
        Assert.Null(await _repository.GetByIdAsync(ghost.Id, None));
    }
}

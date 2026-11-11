using Companies.Application.Common;
using Companies.Application.Companies;
using Companies.Infrastructure.Persistence;
using Microsoft.Extensions.Logging.Abstractions;

namespace Companies.UnitTests;

public class CompanyServiceTests
{
    private static readonly CancellationToken None = CancellationToken.None;

    private readonly FixedClock _clock = new(TestData.Now);
    private readonly CompanyService _service;

    public CompanyServiceTests()
    {
        _service = new CompanyService(
            new InMemoryCompanyRepository(), TestData.Validator(), _clock, NullLogger<CompanyService>.Instance);
    }

    [Fact]
    public async Task Create_stores_the_company_and_returns_it()
    {
        var result = await _service.CreateAsync(new("  Acme Tools Ltd ", "https://www.acme-tools.co.uk"), None);

        Assert.True(result.IsSuccess);
        Assert.NotEqual(Guid.Empty, result.Value.Id);
        Assert.Equal("Acme Tools Ltd", result.Value.Name);
        Assert.Equal("https://www.acme-tools.co.uk/", result.Value.WebsiteUrl);
        Assert.Equal("acme-tools.co.uk", result.Value.Domain);
        Assert.Equal(TestData.Now, result.Value.CreatedAt);
        Assert.Null(result.Value.UpdatedAt);

        var fetched = await _service.GetByIdAsync(result.Value.Id, None);
        Assert.Equal(result.Value, fetched.Value);
    }

    [Fact]
    public async Task Create_with_invalid_input_fails_validation_and_stores_nothing()
    {
        var result = await _service.CreateAsync(new("Globex", "https://www.acme.com"), None);

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorKind.Validation, result.Error.Kind);
        Assert.Contains(CompanyValidator.NameField, result.Error.FieldErrors!.Keys);
        Assert.Equal(0, (await _service.SearchAsync(new CompanyQuery(), None)).TotalCount);
    }

    [Fact]
    public async Task Create_for_a_website_that_is_already_registered_is_a_conflict()
    {
        await _service.CreateAsync(new("Acme", "https://www.acme.com"), None);

        var result = await _service.CreateAsync(new("Acme Inc", "http://acme.com/home"), None);

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorKind.Conflict, result.Error.Kind);
        Assert.Equal("A company with the website 'acme.com' already exists.", result.Error.Message);
    }

    [Fact]
    public async Task GetById_for_an_unknown_id_is_not_found()
    {
        var result = await _service.GetByIdAsync(Guid.NewGuid(), None);

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorKind.NotFound, result.Error.Kind);
    }

    [Fact]
    public async Task Search_with_no_companies_returns_an_empty_page()
    {
        var page = await _service.SearchAsync(new CompanyQuery { Q = "anything" }, None);

        Assert.Empty(page.Items);
        Assert.Equal(0, page.TotalCount);
    }

    [Fact]
    public async Task Search_without_criteria_returns_everything_ordered_by_name()
    {
        await SeedAsync();

        var page = await _service.SearchAsync(new CompanyQuery(), None);

        Assert.Equal(["Acme", "Acme Tools", "Globex", "Northern Acme Supplies"], page.Items.Select(c => c.Name));
        Assert.All(page.Items, company => Assert.Null(company.RelevanceScore));
    }

    [Fact]
    public async Task Search_filters_by_partial_name_ignoring_case()
    {
        await SeedAsync();

        var page = await _service.SearchAsync(new CompanyQuery { Name = "TOOL" }, None);

        Assert.Equal("Acme Tools", Assert.Single(page.Items).Name);
    }

    [Theory]
    [InlineData("globex.co.uk")]
    [InlineData("www.globex.co.uk")]
    [InlineData("https://shop.globex.co.uk/")]
    public async Task Search_filters_by_website_domain(string domain)
    {
        await SeedAsync();

        var page = await _service.SearchAsync(new CompanyQuery { Domain = domain }, None);

        Assert.Equal("Globex", Assert.Single(page.Items).Name);
    }

    [Fact]
    public async Task Search_by_text_orders_results_by_relevance_and_reports_the_score()
    {
        await SeedAsync();

        var page = await _service.SearchAsync(new CompanyQuery { Q = "acme" }, None);

        Assert.Equal(["Acme", "Acme Tools", "Northern Acme Supplies"], page.Items.Select(c => c.Name));
        Assert.Equal([1.0, 0.9, 0.8], page.Items.Select(c => c.RelevanceScore!.Value));
    }

    [Fact]
    public async Task Search_combines_filters_with_text_search()
    {
        await SeedAsync();

        var page = await _service.SearchAsync(new CompanyQuery { Q = "acme", Name = "supplies" }, None);

        Assert.Equal("Northern Acme Supplies", Assert.Single(page.Items).Name);
    }

    [Fact]
    public async Task Search_pages_results_and_reports_the_full_count()
    {
        await SeedAsync();

        var page = await _service.SearchAsync(new CompanyQuery { Page = 2, PageSize = 3 }, None);

        Assert.Equal("Northern Acme Supplies", Assert.Single(page.Items).Name);
        Assert.Equal(4, page.TotalCount);
        Assert.Equal(2, page.Page);
        Assert.Equal(3, page.PageSize);
    }

    [Fact]
    public async Task Update_replaces_the_details_and_stamps_the_time()
    {
        var created = (await _service.CreateAsync(new("Acme", "https://www.acme.com"), None)).Value!;
        _clock.Now = TestData.Now.AddHours(1);

        var result = await _service.UpdateAsync(created.Id, new("Acme Tools", "https://acme-tools.com"), None);

        Assert.True(result.IsSuccess);
        Assert.Equal(created.Id, result.Value.Id);
        Assert.Equal("Acme Tools", result.Value.Name);
        Assert.Equal("acme-tools.com", result.Value.Domain);
        Assert.Equal(created.CreatedAt, result.Value.CreatedAt);
        Assert.Equal(TestData.Now.AddHours(1), result.Value.UpdatedAt);
    }

    [Fact]
    public async Task Update_can_keep_the_same_website()
    {
        var created = (await _service.CreateAsync(new("Acme", "https://www.acme.com"), None)).Value!;

        var result = await _service.UpdateAsync(created.Id, new("Acme Inc", "https://www.acme.com"), None);

        Assert.True(result.IsSuccess);
    }

    [Fact]
    public async Task Update_applies_the_same_validation_as_create()
    {
        var created = (await _service.CreateAsync(new("Acme", "https://www.acme.com"), None)).Value!;

        var result = await _service.UpdateAsync(created.Id, new("Globex", "https://www.acme.com"), None);

        Assert.Equal(ErrorKind.Validation, result.Error!.Kind);
        Assert.Equal("Acme", (await _service.GetByIdAsync(created.Id, None)).Value!.Name);
    }

    [Fact]
    public async Task Update_to_another_companys_website_is_a_conflict()
    {
        await _service.CreateAsync(new("Globex", "https://globex.com"), None);
        var acme = (await _service.CreateAsync(new("Acme", "https://www.acme.com"), None)).Value!;

        var result = await _service.UpdateAsync(acme.Id, new("Globex", "https://globex.com"), None);

        Assert.Equal(ErrorKind.Conflict, result.Error!.Kind);
    }

    [Fact]
    public async Task Update_of_an_unknown_id_is_not_found()
    {
        var result = await _service.UpdateAsync(Guid.NewGuid(), new("Acme", "https://www.acme.com"), None);

        Assert.Equal(ErrorKind.NotFound, result.Error!.Kind);
    }

    [Fact]
    public async Task Delete_removes_the_company()
    {
        var created = (await _service.CreateAsync(new("Acme", "https://www.acme.com"), None)).Value!;

        var deleted = await _service.DeleteAsync(created.Id, None);
        var deletedAgain = await _service.DeleteAsync(created.Id, None);

        Assert.True(deleted.IsSuccess);
        Assert.Equal(ErrorKind.NotFound, deletedAgain.Error!.Kind);
        Assert.Equal(ErrorKind.NotFound, (await _service.GetByIdAsync(created.Id, None)).Error!.Kind);
    }

    private async Task SeedAsync()
    {
        (string Name, string Url)[] companies =
        [
            ("Northern Acme Supplies", "https://northernacmesupplies.com"),
            ("Globex", "https://shop.globex.co.uk"),
            ("Acme Tools", "https://www.acme-tools.com"),
            ("Acme", "https://www.acme.com"),
        ];

        foreach (var (name, url) in companies)
        {
            var result = await _service.CreateAsync(new(name, url), None);
            Assert.True(result.IsSuccess, result.Error?.Message);
        }
    }
}

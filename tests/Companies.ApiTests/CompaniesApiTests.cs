using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Companies.Application.Companies;
using Microsoft.AspNetCore.Mvc.Testing;

namespace Companies.ApiTests;

/// <summary>
/// Exercises the real HTTP pipeline in memory. xUnit builds a new instance, and so a new
/// application with an empty store, for every test.
/// </summary>
public sealed class CompaniesApiTests : IDisposable
{
    private const string Route = "/api/companies";

    private readonly WebApplicationFactory<Program> _factory = new();
    private readonly HttpClient _client;

    public CompaniesApiTests() => _client = _factory.CreateClient();

    public void Dispose() => _factory.Dispose();

    // ---- POST ----------------------------------------------------------------------------

    [Fact]
    public async Task Post_valid_company_returns_201_with_location_and_body()
    {
        var response = await PostAsync("Acme Tools Ltd", "https://www.acme-tools.co.uk");

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var company = await ReadAsync<CompanyResponse>(response);
        Assert.Equal("Acme Tools Ltd", company.Name);
        Assert.Equal("acme-tools.co.uk", company.Domain);
        Assert.EndsWith($"{Route}/{company.Id}", response.Headers.Location!.ToString());

        var fetched = await _client.GetFromJsonAsync<CompanyResponse>(response.Headers.Location);
        Assert.Equal(company, fetched);
    }

    [Fact]
    public async Task Post_invalid_fields_returns_400_problem_listing_each_field()
    {
        var response = await PostAsync("A", "not-a-url");

        var problem = await AssertProblemAsync(response, HttpStatusCode.BadRequest);
        var errors = problem.GetProperty("errors");
        Assert.Equal("Company name must be at least 3 characters.", errors.GetProperty("name")[0].GetString());
        Assert.StartsWith("Website URL must be a valid", errors.GetProperty("websiteUrl")[0].GetString());
        Assert.Contains("Company name must be at least 3 characters.", problem.GetProperty("detail").GetString());
    }

    [Fact]
    public async Task Post_name_unrelated_to_website_returns_400()
    {
        var response = await PostAsync("Globex", "https://www.acme.com");

        var problem = await AssertProblemAsync(response, HttpStatusCode.BadRequest);
        Assert.Equal(
            "Company name 'Globex' does not appear to match the website 'acme.com'.",
            problem.GetProperty("detail").GetString());
    }

    [Fact]
    public async Task Post_missing_fields_returns_400_keyed_by_json_field_names()
    {
        var response = await _client.PostAsync(Route, Json("{}"));

        var problem = await AssertProblemAsync(response, HttpStatusCode.BadRequest);
        var errors = problem.GetProperty("errors");
        Assert.Equal("Company name is required.", errors.GetProperty("name")[0].GetString());
        Assert.Equal("Website URL is required.", errors.GetProperty("websiteUrl")[0].GetString());
    }

    [Fact]
    public async Task Post_malformed_json_returns_400()
    {
        var response = await _client.PostAsync(Route, Json("{ \"name\": "));

        await AssertProblemAsync(response, HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Post_duplicate_website_returns_409()
    {
        await PostAsync("Acme", "https://www.acme.com");

        var response = await PostAsync("Acme Inc", "https://acme.com");

        var problem = await AssertProblemAsync(response, HttpStatusCode.Conflict);
        Assert.Equal("A company with the website 'acme.com' already exists.", problem.GetProperty("detail").GetString());
    }

    // ---- GET -----------------------------------------------------------------------------

    [Fact]
    public async Task Get_unknown_id_returns_404_problem()
    {
        var response = await _client.GetAsync($"{Route}/{Guid.NewGuid()}");

        await AssertProblemAsync(response, HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Get_malformed_id_returns_400()
    {
        var response = await _client.GetAsync($"{Route}/not-a-guid");

        await AssertProblemAsync(response, HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Get_list_when_empty_returns_200_with_no_items()
    {
        var response = await _client.GetAsync(Route);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var page = await ReadAsync<PagedResponse<CompanyResponse>>(response);
        Assert.Empty(page.Items);
        Assert.Equal(0, page.TotalCount);
        Assert.Equal(1, page.Page);
        Assert.Equal(CompanyQuery.DefaultPageSize, page.PageSize);
    }

    [Fact]
    public async Task Get_list_returns_all_companies()
    {
        await SeedAsync();

        var page = await _client.GetFromJsonAsync<PagedResponse<CompanyResponse>>(Route);

        Assert.Equal(["Acme", "Acme Tools", "Globex"], page!.Items.Select(c => c.Name));
    }

    [Fact]
    public async Task Get_list_filters_by_name_and_by_domain()
    {
        await SeedAsync();

        var byName = await _client.GetFromJsonAsync<PagedResponse<CompanyResponse>>($"{Route}?name=tool");
        var byDomain = await _client.GetFromJsonAsync<PagedResponse<CompanyResponse>>($"{Route}?domain=globex.com");

        Assert.Equal("Acme Tools", Assert.Single(byName!.Items).Name);
        Assert.Equal("Globex", Assert.Single(byDomain!.Items).Name);
    }

    [Fact]
    public async Task Get_list_with_q_orders_by_relevance_and_includes_scores()
    {
        await SeedAsync();

        var response = await _client.GetAsync($"{Route}?q=acme");

        var json = await ReadAsync<JsonElement>(response);
        var items = json.GetProperty("items").EnumerateArray().ToList();
        Assert.Equal(["Acme", "Acme Tools"], items.Select(i => i.GetProperty("name").GetString()));
        Assert.Equal([1.0, 0.9], items.Select(i => i.GetProperty("relevanceScore").GetDouble()));
    }

    [Fact]
    public async Task Get_list_without_q_omits_relevance_score()
    {
        await SeedAsync();

        var json = await _client.GetFromJsonAsync<JsonElement>(Route);

        Assert.All(
            json.GetProperty("items").EnumerateArray(),
            item => Assert.False(item.TryGetProperty("relevanceScore", out _)));
    }

    [Fact]
    public async Task Get_list_with_no_match_returns_200_with_no_items()
    {
        await SeedAsync();

        var page = await _client.GetFromJsonAsync<PagedResponse<CompanyResponse>>($"{Route}?q=zzzzzz");

        Assert.Empty(page!.Items);
    }

    [Theory]
    [InlineData("page=0")]
    [InlineData("pageSize=0")]
    [InlineData("pageSize=101")]
    [InlineData("page=abc")]
    public async Task Get_list_with_invalid_paging_returns_400(string query)
    {
        var response = await _client.GetAsync($"{Route}?{query}");

        await AssertProblemAsync(response, HttpStatusCode.BadRequest);
    }

    // ---- PUT / DELETE ----------------------------------------------------------------------

    [Fact]
    public async Task Put_updates_the_company_and_returns_200()
    {
        var created = await ReadAsync<CompanyResponse>(await PostAsync("Acme", "https://www.acme.com"));

        var response = await _client.PutAsJsonAsync(
            $"{Route}/{created.Id}", new UpdateCompanyRequest("Acme Tools", "https://acme-tools.com"));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var updated = await ReadAsync<CompanyResponse>(response);
        Assert.Equal("Acme Tools", updated.Name);
        Assert.Equal("acme-tools.com", updated.Domain);
        Assert.NotNull(updated.UpdatedAt);
    }

    [Fact]
    public async Task Put_invalid_returns_400_and_unknown_id_returns_404()
    {
        var created = await ReadAsync<CompanyResponse>(await PostAsync("Acme", "https://www.acme.com"));

        var invalid = await _client.PutAsJsonAsync(
            $"{Route}/{created.Id}", new UpdateCompanyRequest("Globex", "https://www.acme.com"));
        var unknown = await _client.PutAsJsonAsync(
            $"{Route}/{Guid.NewGuid()}", new UpdateCompanyRequest("Acme", "https://www.acme.com"));

        await AssertProblemAsync(invalid, HttpStatusCode.BadRequest);
        await AssertProblemAsync(unknown, HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Delete_returns_204_then_404()
    {
        var created = await ReadAsync<CompanyResponse>(await PostAsync("Acme", "https://www.acme.com"));

        var first = await _client.DeleteAsync($"{Route}/{created.Id}");
        var second = await _client.DeleteAsync($"{Route}/{created.Id}");

        Assert.Equal(HttpStatusCode.NoContent, first.StatusCode);
        await AssertProblemAsync(second, HttpStatusCode.NotFound);
    }

    // ---- helpers ---------------------------------------------------------------------------

    private Task<HttpResponseMessage> PostAsync(string name, string websiteUrl) =>
        _client.PostAsJsonAsync(Route, new CreateCompanyRequest(name, websiteUrl));

    private async Task SeedAsync()
    {
        (string Name, string Url)[] companies =
        [
            ("Globex", "https://www.globex.com"),
            ("Acme Tools", "https://www.acme-tools.com"),
            ("Acme", "https://www.acme.com"),
        ];

        foreach (var (name, url) in companies)
        {
            (await PostAsync(name, url)).EnsureSuccessStatusCode();
        }
    }

    private static StringContent Json(string body) => new(body, Encoding.UTF8, "application/json");

    private static async Task<T> ReadAsync<T>(HttpResponseMessage response) =>
        (await response.Content.ReadFromJsonAsync<T>())!;

    private static async Task<JsonElement> AssertProblemAsync(HttpResponseMessage response, HttpStatusCode expected)
    {
        Assert.Equal(expected, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);

        var problem = await ReadAsync<JsonElement>(response);
        Assert.Equal((int)expected, problem.GetProperty("status").GetInt32());
        Assert.False(string.IsNullOrWhiteSpace(problem.GetProperty("title").GetString()));
        return problem;
    }
}

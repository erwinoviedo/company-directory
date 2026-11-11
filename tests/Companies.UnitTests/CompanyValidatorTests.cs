using Companies.Application.Companies;

namespace Companies.UnitTests;

public class CompanyValidatorTests
{
    private readonly CompanyValidator _validator = TestData.Validator();

    [Fact]
    public void Valid_input_is_trimmed_and_parsed()
    {
        var result = _validator.Validate("  Acme Tools  ", " https://www.acme-tools.com ");

        Assert.True(result.IsValid);
        Assert.Empty(result.Errors);
        Assert.Equal("Acme Tools", result.Input.Name);
        Assert.Equal("acme-tools.com", result.Input.Website.Domain);
    }

    [Theory]
    [InlineData(null, "Company name is required.")]
    [InlineData("", "Company name is required.")]
    [InlineData("   ", "Company name is required.")]
    [InlineData("Ab", "Company name must be at least 3 characters.")]
    [InlineData(" A  ", "Company name must be at least 3 characters.")]
    public void Missing_or_too_short_name_is_rejected(string? name, string expectedMessage)
    {
        var result = _validator.Validate(name, "https://www.acme.com");

        Assert.False(result.IsValid);
        Assert.Equal([expectedMessage], result.Errors[CompanyValidator.NameField]);
    }

    [Fact]
    public void Overlong_name_is_rejected()
    {
        var result = _validator.Validate(new string('a', 101), "https://www.acme.com");

        Assert.Equal(["Company name must be at most 100 characters."], result.Errors[CompanyValidator.NameField]);
    }

    [Theory]
    [InlineData(null, "Website URL is required.")]
    [InlineData(" ", "Website URL is required.")]
    [InlineData("acme", "Website URL must be a valid http or https address, for example https://www.example.com.")]
    [InlineData("ftp://acme.com", "Website URL must be a valid http or https address, for example https://www.example.com.")]
    public void Missing_or_malformed_website_is_rejected(string? url, string expectedMessage)
    {
        var result = _validator.Validate("Acme", url);

        Assert.False(result.IsValid);
        Assert.Equal([expectedMessage], result.Errors[CompanyValidator.WebsiteUrlField]);
        Assert.DoesNotContain(CompanyValidator.NameField, result.Errors.Keys);
    }

    [Fact]
    public void Every_invalid_field_is_reported_at_once()
    {
        var result = _validator.Validate("", "nope");

        Assert.Equal(2, result.Errors.Count);
    }

    [Fact]
    public void Name_unrelated_to_the_website_is_rejected_with_a_message_naming_both()
    {
        var result = _validator.Validate("Globex", "https://www.acme.com");

        Assert.False(result.IsValid);
        var message = Assert.Single(result.Errors[CompanyValidator.NameField]);
        Assert.Equal("Company name 'Globex' does not appear to match the website 'acme.com'.", message);
    }
}

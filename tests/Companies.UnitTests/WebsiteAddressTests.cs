using Companies.Domain;

namespace Companies.UnitTests;

public class WebsiteAddressTests
{
    [Theory]
    [InlineData("https://www.example.com", "example.com", "example.com", "example")]
    [InlineData("http://Example.COM/about?x=1", "example.com", "example.com", "example")]
    [InlineData("https://shop.acme.co.uk/products", "shop.acme.co.uk", "acme.co.uk", "acme")]
    [InlineData("https://acme-tools.com.au", "acme-tools.com.au", "acme-tools.com.au", "acme-tools")]
    [InlineData("  https://blog.contoso.io  ", "blog.contoso.io", "contoso.io", "contoso")]
    public void TryParse_accepts_web_addresses_and_splits_the_host(
        string input, string expectedHost, string expectedDomain, string expectedLabel)
    {
        var parsed = WebsiteAddress.TryParse(input, out var website);

        Assert.True(parsed);
        Assert.Equal(expectedHost, website!.Host);
        Assert.Equal(expectedDomain, website.Domain);
        Assert.Equal(expectedLabel, website.DomainLabel);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("not-a-url")]
    [InlineData("example.com")] // no scheme
    [InlineData("ftp://example.com")] // not a web address
    [InlineData("http://localhost")] // no domain
    [InlineData("http://192.168.1.10")] // IP address, not a domain name
    [InlineData("https://co.uk")] // a public suffix on its own
    [InlineData("https://")]
    public void TryParse_rejects_anything_that_is_not_a_web_address_on_a_domain(string? input)
    {
        Assert.False(WebsiteAddress.TryParse(input, out var website));
        Assert.Null(website);
    }

    [Fact]
    public void TryParse_rejects_overlong_input()
    {
        var input = "https://example.com/" + new string('a', WebsiteAddress.MaxLength);

        Assert.False(WebsiteAddress.TryParse(input, out _));
    }

    [Theory]
    [InlineData("https://www.Acme.com/about", "acme.com")]
    [InlineData("WWW.acme.com", "acme.com")]
    [InlineData("acme.com/contact", "acme.com")]
    [InlineData(" Acme.CO.UK ", "acme.co.uk")]
    public void NormalizeHostInput_reduces_user_input_to_a_host(string input, string expected)
    {
        Assert.Equal(expected, WebsiteAddress.NormalizeHostInput(input));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("  ")]
    public void NormalizeHostInput_returns_null_for_blank_input(string? input)
    {
        Assert.Null(WebsiteAddress.NormalizeHostInput(input));
    }
}

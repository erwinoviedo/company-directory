using Companies.Application.Relevance;
using Companies.Domain;

namespace Companies.Application.Companies;

public interface ICompanyValidator
{
    CompanyValidationResult Validate(string? name, string? websiteUrl);
}

/// <summary>Cleaned, trusted input ready to become a <see cref="Company"/>.</summary>
public sealed record ValidCompanyInput(string Name, WebsiteAddress Website);

public sealed class CompanyValidationResult
{
    private CompanyValidationResult(ValidCompanyInput? input, IReadOnlyDictionary<string, string[]> errors)
    {
        Input = input;
        Errors = errors;
    }

    public ValidCompanyInput? Input { get; }

    /// <summary>Messages keyed by the JSON field they belong to.</summary>
    public IReadOnlyDictionary<string, string[]> Errors { get; }

    [System.Diagnostics.CodeAnalysis.MemberNotNullWhen(true, nameof(Input))]
    public bool IsValid => Input is not null;

    public static CompanyValidationResult Valid(ValidCompanyInput input) =>
        new(input, new Dictionary<string, string[]>());

    public static CompanyValidationResult Invalid(IReadOnlyDictionary<string, string[]> errors) => new(null, errors);
}

/// <summary>
/// The single source of truth for what makes a company acceptable:
/// a real name, a well-formed website, and a name that belongs to that website.
/// </summary>
public sealed class CompanyValidator(ICompanyRelevanceStrategy relevance) : ICompanyValidator
{
    public const string NameField = "name";
    public const string WebsiteUrlField = "websiteUrl";

    public CompanyValidationResult Validate(string? name, string? websiteUrl)
    {
        var errors = new Dictionary<string, string[]>();

        var trimmedName = name?.Trim() ?? string.Empty;
        var nameError = ValidateName(trimmedName);
        if (nameError is not null)
        {
            errors[NameField] = [nameError];
        }

        WebsiteAddress? website = null;
        if (string.IsNullOrWhiteSpace(websiteUrl))
        {
            errors[WebsiteUrlField] = ["Website URL is required."];
        }
        else if (!WebsiteAddress.TryParse(websiteUrl, out website))
        {
            errors[WebsiteUrlField] =
                ["Website URL must be a valid http or https address, for example https://www.example.com."];
        }

        // Relevance only makes sense once both fields are individually valid.
        if (errors.Count == 0 && website is not null && !relevance.Assess(trimmedName, website).IsRelevant)
        {
            errors[NameField] =
                [$"Company name '{trimmedName}' does not appear to match the website '{website.Domain}'."];
        }

        return errors.Count == 0 && website is not null
            ? CompanyValidationResult.Valid(new ValidCompanyInput(trimmedName, website))
            : CompanyValidationResult.Invalid(errors);
    }

    private static string? ValidateName(string trimmedName) => trimmedName.Length switch
    {
        0 => "Company name is required.",
        < Company.NameMinLength => $"Company name must be at least {Company.NameMinLength} characters.",
        > Company.NameMaxLength => $"Company name must be at most {Company.NameMaxLength} characters.",
        _ => null,
    };
}

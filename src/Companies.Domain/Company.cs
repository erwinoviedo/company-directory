namespace Companies.Domain;

/// <summary>
/// A company and the website it is known by. Instances are immutable; a change produces a new instance.
/// </summary>
public sealed record Company
{
    public const int NameMinLength = 3;
    public const int NameMaxLength = 100;

    private Company(Guid id, string name, WebsiteAddress website, DateTimeOffset createdAt, DateTimeOffset? updatedAt)
    {
        Id = id;
        Name = name;
        Website = website;
        CreatedAt = createdAt;
        UpdatedAt = updatedAt;
    }

    public Guid Id { get; }

    public string Name { get; }

    public WebsiteAddress Website { get; }

    public DateTimeOffset CreatedAt { get; }

    public DateTimeOffset? UpdatedAt { get; }

    public static Company Create(string name, WebsiteAddress website, DateTimeOffset now) =>
        new(Guid.NewGuid(), name, website, now, updatedAt: null);

    public Company WithDetails(string name, WebsiteAddress website, DateTimeOffset now) =>
        new(Id, name, website, CreatedAt, now);
}

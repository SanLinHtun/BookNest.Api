namespace BookNest.Api.Domain;

public sealed class Author
{
    public long AuthorId
    {
        get;
        set;
    }
    public string AuthorName
    {
        get;
        set;
    } = string.Empty;
    public string? Bio
    {
        get;
        set;
    }
    public DateTimeOffset CreatedAt
    {
        get;
        set;
    } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAt
    {
        get;
        set;
    } = DateTimeOffset.UtcNow;
}

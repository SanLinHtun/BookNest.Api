namespace BookNest.Api.Domain;

public sealed class Collection
{
    public long CollectionId
    {
        get;
        set;
    }
    public long UserId
    {
        get;
        set;
    }
    public string Name
    {
        get;
        set;
    } = string.Empty;
    public string? Description
    {
        get;
        set;
    }
    public string? CoverImageUrl
    {
        get;
        set;
    }
    public string Visibility
    {
        get;
        set;
    } = "PRIVATE";
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

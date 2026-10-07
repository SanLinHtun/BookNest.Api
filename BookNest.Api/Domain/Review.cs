namespace BookNest.Api.Domain;

public sealed class Review
{
    public long ReviewId
    {
        get;
        set;
    }
    public long UserId
    {
        get;
        set;
    }
    public long BookId
    {
        get;
        set;
    }
    public int Rating
    {
        get;
        set;
    }
    public string? ReviewText
    {
        get;
        set;
    }
    public string Status
    {
        get;
        set;
    } = "PUBLISHED";
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

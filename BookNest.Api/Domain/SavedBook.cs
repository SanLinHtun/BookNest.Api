namespace BookNest.Api.Domain;

public sealed class SavedBook
{
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
    public DateTimeOffset SavedAt
    {
        get;
        set;
    } = DateTimeOffset.UtcNow;
}

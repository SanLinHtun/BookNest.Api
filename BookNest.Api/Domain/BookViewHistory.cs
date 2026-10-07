namespace BookNest.Api.Domain;

public sealed class BookViewHistory
{
    public long ViewId
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
    public DateTimeOffset ViewedAt
    {
        get;
        set;
    } = DateTimeOffset.UtcNow;
    public string EventKey
    {
        get;
        set;
    } = string.Empty;
}

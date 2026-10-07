namespace BookNest.Api.Domain;

public sealed class UserBook
{
    public long UserBookId
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
    public DateTimeOffset AddedAt
    {
        get;
        set;
    } = DateTimeOffset.UtcNow;
    public DateTimeOffset? ArchivedAt
    {
        get;
        set;
    }
}

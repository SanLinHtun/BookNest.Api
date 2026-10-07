namespace BookNest.Api.Domain;

public sealed class Bookmark
{
    public long BookmarkId
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
    public long? ChapterId
    {
        get;
        set;
    }
    public int? PageNo
    {
        get;
        set;
    }
    public string? LocationToken
    {
        get;
        set;
    }
    public string? Label
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

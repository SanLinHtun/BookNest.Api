namespace BookNest.Api.Domain;

public sealed class ReadingProgress
{
    public long ProgressId
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
    public int CurrentPage
    {
        get;
        set;
    } = 0;
    public string? LocationToken
    {
        get;
        set;
    }
    public string Status
    {
        get;
        set;
    } = "NOT_STARTED";
    public DateTimeOffset? StartedAt
    {
        get;
        set;
    }
    public DateTimeOffset? LastReadAt
    {
        get;
        set;
    }
    public DateTimeOffset? FinishedAt
    {
        get;
        set;
    }
    public DateTimeOffset UpdatedAt
    {
        get;
        set;
    } = DateTimeOffset.UtcNow;
}

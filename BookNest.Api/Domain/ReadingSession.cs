namespace BookNest.Api.Domain;

public sealed class ReadingSession
{
    public long SessionId
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
    public string ClientSessionKey
    {
        get;
        set;
    } = string.Empty;
    public DateTimeOffset StartedAt
    {
        get;
        set;
    }
    public DateTimeOffset? EndedAt
    {
        get;
        set;
    }
    public int ActiveSeconds
    {
        get;
        set;
    } = 0;
    public int StartPage
    {
        get;
        set;
    } = 0;
    public int EndPage
    {
        get;
        set;
    } = 0;
    public int PagesRead
    {
        get;
        set;
    } = 0;
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

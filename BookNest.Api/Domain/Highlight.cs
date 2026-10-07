namespace BookNest.Api.Domain;

public sealed class Highlight
{
    public long HighlightId
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
    public string HighlightedText
    {
        get;
        set;
    } = string.Empty;
    public string StartAnchor
    {
        get;
        set;
    } = string.Empty;
    public string EndAnchor
    {
        get;
        set;
    } = string.Empty;
    public string ColorCode
    {
        get;
        set;
    } = "YELLOW";
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

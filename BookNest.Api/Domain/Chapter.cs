namespace BookNest.Api.Domain;

public sealed class Chapter
{
    public long ChapterId
    {
        get;
        set;
    }
    public long BookId
    {
        get;
        set;
    }
    public int ChapterNo
    {
        get;
        set;
    }
    public string Title
    {
        get;
        set;
    } = string.Empty;
    public int StartPage
    {
        get;
        set;
    }
    public int EndPage
    {
        get;
        set;
    }
    public int SortOrder
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

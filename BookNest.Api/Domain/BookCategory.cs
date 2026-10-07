namespace BookNest.Api.Domain;

public sealed class BookCategory
{
    public long BookId
    {
        get;
        set;
    }
    public long CategoryId
    {
        get;
        set;
    }
    public bool IsPrimary
    {
        get;
        set;
    } = false;
}

namespace BookNest.Api.Domain;

public sealed class BookAuthor
{
    public long BookId
    {
        get;
        set;
    }
    public long AuthorId
    {
        get;
        set;
    }
    public int DisplayOrder
    {
        get;
        set;
    } = 1;
}

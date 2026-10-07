namespace BookNest.Api.Domain;

public sealed class CollectionBook
{
    public long CollectionId
    {
        get;
        set;
    }
    public long BookId
    {
        get;
        set;
    }
    public int SortOrder
    {
        get;
        set;
    } = 0;
    public DateTimeOffset AddedAt
    {
        get;
        set;
    } = DateTimeOffset.UtcNow;
}

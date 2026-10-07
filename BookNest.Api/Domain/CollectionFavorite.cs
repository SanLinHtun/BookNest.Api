namespace BookNest.Api.Domain;

public sealed class CollectionFavorite
{
    public long UserId
    {
        get;
        set;
    }
    public long CollectionId
    {
        get;
        set;
    }
    public DateTimeOffset FavoritedAt
    {
        get;
        set;
    } = DateTimeOffset.UtcNow;
}

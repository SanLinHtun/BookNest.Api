namespace BookNest.Api.Domain;

public sealed class CollectionMember
{
    public long CollectionId
    {
        get;
        set;
    }
    public long UserId
    {
        get;
        set;
    }
    public string Role
    {
        get;
        set;
    } = "VIEWER";
    public string Status
    {
        get;
        set;
    } = "INVITED";
    public DateTimeOffset? JoinedAt
    {
        get;
        set;
    }
    public DateTimeOffset CreatedAt
    {
        get;
        set;
    } = DateTimeOffset.UtcNow;
}

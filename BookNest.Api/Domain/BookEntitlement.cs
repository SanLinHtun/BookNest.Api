namespace BookNest.Api.Domain;

public sealed class BookEntitlement
{
    public long EntitlementId
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
    public long? OrderItemId
    {
        get;
        set;
    }
    public long? SubscriptionId
    {
        get;
        set;
    }
    public string SourceType
    {
        get;
        set;
    } = string.Empty;
    public DateTimeOffset StartsAt
    {
        get;
        set;
    }
    public DateTimeOffset? ExpiresAt
    {
        get;
        set;
    }
    public DateTimeOffset? RevokedAt
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

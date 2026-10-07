namespace BookNest.Api.Domain;

public sealed class UserSubscription
{
    public long SubscriptionId
    {
        get;
        set;
    }
    public long UserId
    {
        get;
        set;
    }
    public long PlanId
    {
        get;
        set;
    }
    public string Status
    {
        get;
        set;
    } = "PENDING";
    public DateTimeOffset StartedAt
    {
        get;
        set;
    }
    public DateTimeOffset EndsAt
    {
        get;
        set;
    }
    public bool CancelAtPeriodEnd
    {
        get;
        set;
    } = false;
    public string? ProviderReference
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

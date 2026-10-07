namespace BookNest.Api.Domain;

public sealed class SubscriptionPlan
{
    public long PlanId
    {
        get;
        set;
    }
    public string PlanCode
    {
        get;
        set;
    } = string.Empty;
    public string Name
    {
        get;
        set;
    } = string.Empty;
    public decimal Price
    {
        get;
        set;
    } = 0m;
    public string CurrencyCode
    {
        get;
        set;
    } = "JPY";
    public string BillingInterval
    {
        get;
        set;
    } = string.Empty;
    public bool OfflineAccess
    {
        get;
        set;
    } = true;
    public bool AdFree
    {
        get;
        set;
    } = true;
    public bool IsActive
    {
        get;
        set;
    } = true;
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

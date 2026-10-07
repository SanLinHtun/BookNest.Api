namespace BookNest.Api.Domain;

public sealed class Order
{
    public long OrderId
    {
        get;
        set;
    }
    public long UserId
    {
        get;
        set;
    }
    public string OrderNumber
    {
        get;
        set;
    } = string.Empty;
    public string Status
    {
        get;
        set;
    } = "PENDING";
    public string CurrencyCode
    {
        get;
        set;
    } = "JPY";
    public decimal TotalAmount
    {
        get;
        set;
    } = 0m;
    public string? PaymentReference
    {
        get;
        set;
    }
    public DateTimeOffset? PaidAt
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

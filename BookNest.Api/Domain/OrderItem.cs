namespace BookNest.Api.Domain;

public sealed class OrderItem
{
    public long OrderItemId
    {
        get;
        set;
    }
    public long OrderId
    {
        get;
        set;
    }
    public long BookId
    {
        get;
        set;
    }
    public string AccessType
    {
        get;
        set;
    } = string.Empty;
    public decimal UnitPrice
    {
        get;
        set;
    } = 0m;
    public int Quantity
    {
        get;
        set;
    } = 1;
    public decimal LineTotal
    {
        get;
        set;
    } = 0m;
    public int? RentalDays
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

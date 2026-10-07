namespace BookNest.Api.Domain;

public sealed class ReadingGoal
{
    public long GoalId
    {
        get;
        set;
    }
    public long UserId
    {
        get;
        set;
    }
    public int TargetYear
    {
        get;
        set;
    }
    public int TargetBooks
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

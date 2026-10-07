namespace BookNest.Api.Domain;

public sealed class Category
{
    public long CategoryId
    {
        get;
        set;
    }
    public long? ParentCategoryId
    {
        get;
        set;
    }
    public string Name
    {
        get;
        set;
    } = string.Empty;
    public string Slug
    {
        get;
        set;
    } = string.Empty;
    public string? Description
    {
        get;
        set;
    }
    public string? IconKey
    {
        get;
        set;
    }
    public string? BannerImageUrl
    {
        get;
        set;
    }
    public int SortOrder
    {
        get;
        set;
    } = 0;
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

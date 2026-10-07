using System.Text.Json.Serialization;

namespace BookNest.Api.Domain;

public sealed class User
{
    public long UserId
    {
        get;
        set;
    }
    public string Email
    {
        get;
        set;
    } = string.Empty;
    [JsonIgnore]
    public string? PasswordHash
    {
        get;
        set;
    }
    public string DisplayName
    {
        get;
        set;
    } = string.Empty;
    public string? AvatarUrl
    {
        get;
        set;
    }
    public string? Bio
    {
        get;
        set;
    }
    public string Timezone
    {
        get;
        set;
    } = "Asia/Tokyo";
    public string Status
    {
        get;
        set;
    } = "ACTIVE";
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

namespace BookNest.Api.Domain;

public sealed class Download
{
    public long DownloadId
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
    public string DeviceKey
    {
        get;
        set;
    } = string.Empty;
    public int ContentVersion
    {
        get;
        set;
    } = 1;
    public string Status
    {
        get;
        set;
    } = "QUEUED";
    public long BytesDownloaded
    {
        get;
        set;
    } = 0;
    public DateTimeOffset? DownloadedAt
    {
        get;
        set;
    }
    public DateTimeOffset? AccessExpiresAt
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

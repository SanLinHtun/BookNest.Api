using System.Text.Json.Serialization;

namespace BookNest.Api.Domain;

public sealed class Book
{
    public long BookId
    {
        get;
        set;
    }
    public string Title
    {
        get;
        set;
    } = string.Empty;
    public string? Description
    {
        get;
        set;
    }
    public string? CoverImageUrl
    {
        get;
        set;
    }
    public string? Isbn
    {
        get;
        set;
    }
    public string? PublisherName
    {
        get;
        set;
    }
    public DateOnly? PublishedDate
    {
        get;
        set;
    }
    public string LanguageCode
    {
        get;
        set;
    } = string.Empty;
    public int PageCount
    {
        get;
        set;
    }
    public decimal PurchasePrice
    {
        get;
        set;
    } = 0m;
    public decimal RentalPrice
    {
        get;
        set;
    } = 0m;
    public string CurrencyCode
    {
        get;
        set;
    } = "JPY";
    public int RentalDays
    {
        get;
        set;
    } = 30;
    public string? PreviewUrl
    {
        get;
        set;
    }
    [JsonIgnore]
    public string ContentStorageKey
    {
        get;
        set;
    } = string.Empty;
    public string FileFormat
    {
        get;
        set;
    } = "PDF";
    public long? FileSizeBytes
    {
        get;
        set;
    }
    public int ContentVersion
    {
        get;
        set;
    } = 1;
    public bool PremiumEligible
    {
        get;
        set;
    } = true;
    public string Status
    {
        get;
        set;
    } = "DRAFT";
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

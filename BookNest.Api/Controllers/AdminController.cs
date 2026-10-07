using System.ComponentModel.DataAnnotations;
using BookNest.Api.Data;
using BookNest.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
namespace BookNest.Api.Controllers;

public sealed record BookRequest([Required, MaxLength(255)] string Title, [MaxLength(20000)] string? Description,
    [MaxLength(1000)] string? CoverImageUrl, [MaxLength(20)] string? Isbn, [MaxLength(255)] string? PublisherName, DateOnly? PublishedDate,
    [Required, MaxLength(35)] string LanguageCode, [Range(1, int.MaxValue)] int PageCount,
    [Range(typeof(decimal), "0", "9999999999.99")] decimal PurchasePrice, [Range(typeof(decimal), "0", "9999999999.99")] decimal RentalPrice,
    [Required, RegularExpression("[A-Z]{3}")] string CurrencyCode, [Range(1, 3650)] int RentalDays,
    [MaxLength(1000)] string? PreviewUrl, [Required, MaxLength(1000)] string ContentStorageKey,
    [Required, RegularExpression("PDF|EPUB")] string FileFormat, [Range(0, long.MaxValue)] long? FileSizeBytes,
    [Range(1, int.MaxValue)] int ContentVersion, bool PremiumEligible, [Required, RegularExpression("DRAFT|PUBLISHED|ARCHIVED")] string Status);
public sealed record AuthorRequest([Required, MaxLength(150)] string AuthorName, [MaxLength(20000)] string? Bio);
public sealed record CategoryRequest(long? ParentCategoryId, [Required, MaxLength(100)] string Name, [Required, MaxLength(100)] string Slug,
    [MaxLength(20000)] string? Description, [MaxLength(100)] string? IconKey, [MaxLength(1000)] string? BannerImageUrl, int SortOrder, bool IsActive = true);
public sealed record ChapterRequest([Range(1, int.MaxValue)] int ChapterNo, [Required, MaxLength(255)] string Title,
    [Range(1, int.MaxValue)] int StartPage, [Range(1, int.MaxValue)] int EndPage, int SortOrder);
public sealed record AuthorLinkRequest([Range(1, int.MaxValue)] int DisplayOrder = 1);
public sealed record CategoryLinkRequest(bool IsPrimary = false);
public sealed record PlanRequest([Required, MaxLength(30)] string PlanCode, [Required, MaxLength(100)] string Name,
    [Range(typeof(decimal), "0", "9999999999.99")] decimal Price, [Required, RegularExpression("[A-Z]{3}")] string CurrencyCode,
    [Required, RegularExpression("MONTH|YEAR")] string BillingInterval, bool OfflineAccess = true, bool AdFree = true, bool IsActive = true);
public sealed record ReviewModeration([Required, RegularExpression("PUBLISHED|HIDDEN")] string Status);

[ApiController, Authorize(Policy = "Admin"), Route("api/v1/admin")]
public sealed class AdminController(BookNestDbContext db) : UserController
{
    /// <summary>
    /// Returns a page of catalog books, including draft and archived records, for admins.
    /// </summary>
    [HttpGet("books")]
    public async Task<IActionResult> Books(int page = 1, int pageSize = 20, CancellationToken ct = default)
    {
        var (skip, take) = Page(page, pageSize);
        return Ok(await db.Books.AsNoTracking().OrderBy(b => b.BookId).Skip(skip).Take(take).ToListAsync(ct));
    }
    /// <summary>
    /// Creates catalog metadata using the validated admin request.
    /// </summary>
    [HttpPost("books")]
    public async Task<IActionResult> CreateBook(BookRequest r, CancellationToken ct) => await WriteBook(null, r, ct);
    /// <summary>
    /// Updates catalog metadata while preserving valid content locations and versions.
    /// </summary>
    [HttpPut("books/{id:long}")]
    public async Task<IActionResult> UpdateBook(long id, BookRequest r, CancellationToken ct) => await WriteBook(id, r, ct);
    /// <summary>
    /// Validates storage paths, page counts and content versions before saving a book.
    /// </summary>
    private async Task<IActionResult> WriteBook(long? id, BookRequest r, CancellationToken ct)
    {
        Require(!string.IsNullOrWhiteSpace(r.Title) && !string.IsNullOrWhiteSpace(r.LanguageCode), "Title and language required.");
        Require(!Path.IsPathRooted(r.ContentStorageKey) && !r.ContentStorageKey.Split('/', '\\').Contains(".."), "Content key must be a relative path inside content storage.");
        var b = id == null
            ? new Domain.Book()
            : await db.Books.FindAsync([id.Value], ct) ?? throw new ApiException(404, "Book not found.");
        if (id != null)
        {
            Require(!await db.Chapters.AnyAsync(c => c.BookId == id && c.EndPage > r.PageCount, ct), "Page count is smaller than an existing chapter.");
            Require(!await db.ReadingProgress.AnyAsync(p => p.BookId == id && p.CurrentPage > r.PageCount, ct), "Page count is smaller than existing reading progress.");
            Require(r.ContentVersion >= b.ContentVersion, "Content version cannot decrease.");
            Require((r.ContentStorageKey == b.ContentStorageKey && r.FileFormat == b.FileFormat) || r.ContentVersion > b.ContentVersion, "Increase content version when replacing content.");
        }
        b.Title = r.Title.Trim();
        b.Description = r.Description;
        b.CoverImageUrl = r.CoverImageUrl;
        b.Isbn = r.Isbn?.Replace("-", "").Replace(" ", "");
        b.PublisherName = r.PublisherName;
        b.PublishedDate = r.PublishedDate;
        b.LanguageCode = r.LanguageCode;
        b.PageCount = r.PageCount;
        b.PurchasePrice = r.PurchasePrice;
        b.RentalPrice = r.RentalPrice;
        b.CurrencyCode = r.CurrencyCode;
        b.RentalDays = r.RentalDays;
        b.PreviewUrl = r.PreviewUrl;
        b.ContentStorageKey = r.ContentStorageKey;
        b.FileFormat = r.FileFormat;
        b.FileSizeBytes = r.FileSizeBytes;
        b.ContentVersion = r.ContentVersion;
        b.PremiumEligible = r.PremiumEligible;
        b.Status = r.Status;
        if (id == null)
        {
            db.Books.Add(b);
        }
        await db.SaveChangesAsync(ct);
        return Ok(b);
    }
    /// <summary>
    /// Archives a catalog book while preserving its references and purchase history.
    /// </summary>
    [HttpDelete("books/{id:long}")]
    public async Task<IActionResult> ArchiveBook(long id, CancellationToken ct)
    {
        var b = await db.Books.FindAsync([id], ct) ?? throw new ApiException(404, "Book not found.");
        b.Status = "ARCHIVED";
        await db.SaveChangesAsync(ct);
        return NoContent();
    }
    /// <summary>
    /// Creates an author record for use in the catalog.
    /// </summary>
    [HttpPost("authors")]
    public async Task<IActionResult> CreateAuthor(AuthorRequest r, CancellationToken ct)
    {
        var a = new Domain.Author
        {
            AuthorName = r.AuthorName,
            Bio = r.Bio
        };
        db.Authors.Add(a);
        await db.SaveChangesAsync(ct);
        return Ok(a);
    }
    /// <summary>
    /// Updates an existing author's display name and biography.
    /// </summary>
    [HttpPut("authors/{id:long}")]
    public async Task<IActionResult> UpdateAuthor(long id, AuthorRequest r, CancellationToken ct)
    {
        var a = await db.Authors.FindAsync([id], ct) ?? throw new ApiException(404, "Author not found.");
        a.AuthorName = r.AuthorName;
        a.Bio = r.Bio;
        await db.SaveChangesAsync(ct);
        return Ok(a);
    }
    /// <summary>
    /// Creates a category after checking its parent hierarchy.
    /// </summary>
    [HttpPost("categories")]
    public async Task<IActionResult> CreateCategory(CategoryRequest r, CancellationToken ct) => await WriteCategory(null, r, ct);
    /// <summary>
    /// Updates a category after checking its parent hierarchy.
    /// </summary>
    [HttpPut("categories/{id:long}")]
    public async Task<IActionResult> UpdateCategory(long id, CategoryRequest r, CancellationToken ct) => await WriteCategory(id, r, ct);
    /// <summary>
    /// Checks for hierarchy cycles and saves category changes transactionally.
    /// </summary>
    private async Task<IActionResult> WriteCategory(long? id, CategoryRequest r, CancellationToken ct)
    {
        await using var tx = await db.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable, ct);
        var cursor = r.ParentCategoryId;
        var seen = new HashSet<long>();
        while (cursor != null)
        {
            Require(cursor != id && seen.Add(cursor.Value), "Category hierarchy cannot contain cycles.");
            var parent = await db.Categories.FindAsync([cursor.Value], ct) ?? throw new ApiException(400, "Parent category not found.");
            cursor = parent.ParentCategoryId;
        }
        var c = id == null ? new Domain.Category() : await db.Categories.FindAsync([id.Value], ct) ?? throw new ApiException(404, "Category not found.");
        c.ParentCategoryId = r.ParentCategoryId;
        c.Name = r.Name;
        c.Slug = r.Slug;
        c.Description = r.Description;
        c.IconKey = r.IconKey;
        c.BannerImageUrl = r.BannerImageUrl;
        c.SortOrder = r.SortOrder;
        c.IsActive = r.IsActive;
        if (id == null)
        {
            db.Categories.Add(c);
        }
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        return Ok(c);
    }
    /// <summary>
    /// Creates a chapter within the book's valid page range.
    /// </summary>
    [HttpPost("books/{bookId:long}/chapters")]
    public async Task<IActionResult> CreateChapter(long bookId, ChapterRequest r, CancellationToken ct) => await WriteChapter(bookId, null, r, ct);
    /// <summary>
    /// Updates an existing chapter belonging to the specified book.
    /// </summary>
    [HttpPut("books/{bookId:long}/chapters/{id:long}")]
    public async Task<IActionResult> UpdateChapter(long bookId, long id, ChapterRequest r, CancellationToken ct) => await WriteChapter(bookId, id, r, ct);
    /// <summary>
    /// Checks chapter ownership and page boundaries before saving its metadata.
    /// </summary>
    private async Task<IActionResult> WriteChapter(long bookId, long? id, ChapterRequest r, CancellationToken ct)
    {
        var b = await db.Books.FindAsync([bookId], ct) ?? throw new ApiException(404, "Book not found.");
        Require(r.StartPage <= r.EndPage && r.EndPage <= b.PageCount, "Invalid chapter page range.");
        var c = id == null ? new Domain.Chapter
        {
            BookId = bookId
        }

        : await db.Chapters.SingleOrDefaultAsync(c => c.ChapterId == id && c.BookId == bookId, ct)
            ?? throw new ApiException(404, "Chapter not found.");

        c.ChapterNo = r.ChapterNo;
        c.Title = r.Title;
        c.StartPage = r.StartPage;
        c.EndPage = r.EndPage;
        c.SortOrder = r.SortOrder;
        if (id == null)
        {
            db.Chapters.Add(c);
        }
        await db.SaveChangesAsync(ct);
        return Ok(c);
    }
    /// <summary>
    /// Adds or updates a book-author link and its display order.
    /// </summary>
    [HttpPut("books/{bookId:long}/authors/{authorId:long}")]
    public async Task<IActionResult> LinkAuthor(long bookId, long authorId, AuthorLinkRequest r, CancellationToken ct)
    {
        var row = await db.BookAuthors.FindAsync([bookId, authorId], ct);
        if (row == null)
        {
            row = new()
            {
                BookId = bookId,
                AuthorId = authorId
            };
            db.BookAuthors.Add(row);
        }
        row.DisplayOrder = r.DisplayOrder;
        await db.SaveChangesAsync(ct);
        return NoContent();
    }
    /// <summary>
    /// Removes the specified book-author link.
    /// </summary>
    [HttpDelete("books/{bookId:long}/authors/{authorId:long}")]
    public async Task<IActionResult> UnlinkAuthor(long bookId, long authorId, CancellationToken ct)
    {
        await db.BookAuthors.Where(x => x.BookId == bookId && x.AuthorId == authorId).ExecuteDeleteAsync(ct);
        return NoContent();
    }
    /// <summary>
    /// Adds or updates a book category while keeping at most one primary category.
    /// </summary>
    [HttpPut("books/{bookId:long}/categories/{categoryId:long}")]
    public async Task<IActionResult> LinkCategory(long bookId, long categoryId, CategoryLinkRequest r, CancellationToken ct)
    {
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        await db.Database.ExecuteSqlInterpolatedAsync($"SELECT book_id FROM books WHERE book_id = {bookId} FOR UPDATE", ct);
        if (r.IsPrimary)
        {
            await db.Database.ExecuteSqlInterpolatedAsync(
                $"SELECT book_id FROM book_categories WHERE book_id = {bookId} AND is_primary = TRUE FOR UPDATE", ct);
        }

        await db.BookCategories.Where(x => x.BookId == bookId && x.IsPrimary)
            .ExecuteUpdateAsync(s => s.SetProperty(x => x.IsPrimary, false), ct);
        var row = await db.BookCategories.FindAsync([bookId, categoryId], ct);
        if (row == null)
        {
            row = new()
            {
                BookId = bookId,
                CategoryId = categoryId
            };
            db.BookCategories.Add(row);
        }
        row.IsPrimary = r.IsPrimary;
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        return NoContent();
    }
    /// <summary>
    /// Removes the specified book-category link.
    /// </summary>
    [HttpDelete("books/{bookId:long}/categories/{categoryId:long}")]
    public async Task<IActionResult> UnlinkCategory(long bookId, long categoryId, CancellationToken ct)
    {
        await db.BookCategories.Where(x => x.BookId == bookId && x.CategoryId == categoryId).ExecuteDeleteAsync(ct);
        return NoContent();
    }
    /// <summary>
    /// Creates a subscription plan with its price, billing interval and benefits.
    /// </summary>
    [HttpPost("subscription-plans")]
    public async Task<IActionResult> CreatePlan(PlanRequest r, CancellationToken ct)
    {
        var p = new Domain.SubscriptionPlan
        {
            PlanCode = r.PlanCode,
            Name = r.Name,
            Price = r.Price,
            CurrencyCode = r.CurrencyCode,
            BillingInterval = r.BillingInterval,
            OfflineAccess = r.OfflineAccess,
            AdFree = r.AdFree,
            IsActive = r.IsActive
        };
        db.SubscriptionPlans.Add(p);
        await db.SaveChangesAsync(ct);
        return Ok(p);
    }
    /// <summary>
    /// Updates plan details and benefits without changing existing billing terms.
    /// </summary>
    [HttpPut("subscription-plans/{id:long}")]
    public async Task<IActionResult> UpdatePlan(long id, PlanRequest r, CancellationToken ct)
    {
        var p = await db.SubscriptionPlans.FindAsync([id], ct) ?? throw new ApiException(404, "Plan not found.");
        Require(p.Price == r.Price && p.CurrencyCode == r.CurrencyCode && p.BillingInterval == r.BillingInterval,
            "Create a new plan to change billing terms; existing pending payments retain their plan price.");
        p.Name = r.Name;
        p.IsActive = r.IsActive;
        p.OfflineAccess = r.OfflineAccess;
        p.AdFree = r.AdFree;
        await db.SaveChangesAsync(ct);
        return Ok(p);
    }
    /// <summary>
    /// Changes whether a review is published or hidden from public rating totals.
    /// </summary>
    [HttpPut("reviews/{id:long}/status")]
    public async Task<IActionResult> ModerateReview(long id, ReviewModeration r, CancellationToken ct)
    {
        var review = await db.Reviews.FindAsync([id], ct) ?? throw new ApiException(404, "Review not found.");
        review.Status = r.Status;
        await db.SaveChangesAsync(ct);
        return Ok(review);
    }
}

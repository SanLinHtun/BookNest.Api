using BookNest.Api.Data;
using BookNest.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BookNest.Api.Controllers;

[ApiController, Route("api/v1")]
public sealed class CatalogController(BookNestDbContext db) : UserController
{
    /// <summary>
    /// Returns a page of published books with optional title/author and category filters.
    /// </summary>
    [HttpGet("books")]
    public async Task<IActionResult> Books(string? search, long? categoryId, int page = 1, int pageSize = 20, CancellationToken ct = default)
    {
        var (skip, take) = Page(page, pageSize);
        var query = db.Books.AsNoTracking().Where(x => x.Status == "PUBLISHED");
        if (!string.IsNullOrWhiteSpace(search))
        {
            query = query.Where(x => x.Title.Contains(search) || db.BookAuthors.Any(link =>
                link.BookId == x.BookId && db.Authors.Any(author =>
                    author.AuthorId == link.AuthorId && author.AuthorName.Contains(search))));
        }

        if (categoryId != null)
        {
            query = query.Where(x => db.BookCategories.Any(c => c.BookId == x.BookId && c.CategoryId == categoryId));
        }

        return Ok(new
        {
            page,
            pageSize,
            total = await query.CountAsync(ct),
            items = await query.OrderBy(x => x.BookId).Skip(skip).Take(take).ToListAsync(ct)
        });
    }

    /// <summary>
    /// Returns a published book with its authors, categories and published review totals.
    /// </summary>
    [HttpGet("books/{id:long}")]
    public async Task<IActionResult> Detail(long id, CancellationToken ct)
    {
        var book = await db.Books.AsNoTracking().SingleOrDefaultAsync(x => x.BookId == id && x.Status == "PUBLISHED", ct) 
            ?? throw new ApiException(404, "Book not found.");
        var ratings = db.Reviews.Where(x => x.BookId == id && x.Status == "PUBLISHED");
        return Ok(new
        {
            book,
            authors = await (
            from a in db.Authors
            join l in db.BookAuthors on a.AuthorId equals l.AuthorId
            where l.BookId == id
            orderby l.DisplayOrder
            select a).ToListAsync(ct),
            categories = await (
            from c in db.Categories
            join l in db.BookCategories on c.CategoryId equals l.CategoryId
            where l.BookId == id && c.IsActive
            select c).ToListAsync(ct),
            rating = await ratings.Select(x => (double?)x.Rating).AverageAsync(ct),
            reviewCount = await ratings.CountAsync(ct)
        });
    }

    /// <summary>
    /// Lists the chapters of a published book in reading order.
    /// </summary>
    [HttpGet("books/{id:long}/chapters")]
    public async Task<IActionResult> Chapters(long id, CancellationToken ct)
    {
        if (!await db.Books.AnyAsync(b => b.BookId == id && b.Status == "PUBLISHED", ct))
        {
            throw new ApiException(404, "Book not found.");
        }

        return Ok(await db.Chapters.AsNoTracking().Where(x => x.BookId == id).OrderBy(x => x.SortOrder).ThenBy(x => x.ChapterNo).ToListAsync(ct));
    }

    /// <summary>
    /// Lists active catalog categories in display order.
    /// </summary>
    [HttpGet("categories")]
    public async Task<IActionResult> Categories(CancellationToken ct) => Ok(await db.Categories.AsNoTracking().Where(c => c.IsActive).OrderBy(c => c.SortOrder).ToListAsync(ct));
    /// <summary>
    /// Returns a page of catalog authors.
    /// </summary>
    [HttpGet("authors")]
    public async Task<IActionResult> Authors(int page = 1, int pageSize = 20, CancellationToken ct = default)
    {
        var (skip, take) = Page(page, pageSize);
        return Ok(await db.Authors.AsNoTracking().OrderBy(a => a.AuthorId).Skip(skip).Take(take).ToListAsync(ct));
    }

    /// <summary>
    /// Returns a page of published reviews with reviewer display information.
    /// </summary>
    [HttpGet("books/{id:long}/reviews")]
    public async Task<IActionResult> Reviews(long id, int page = 1, int pageSize = 20, CancellationToken ct = default)
    {
        var (skip, take) = Page(page, pageSize);
        return Ok(await (
            from r in db.Reviews
            join u in db.Users on r.UserId equals u.UserId
            where r.BookId == id && r.Status == "PUBLISHED"
            orderby r.CreatedAt descending
            select new
            {
                r.ReviewId,
                r.Rating,
                r.ReviewText,
                r.CreatedAt,
                u.DisplayName,
                u.AvatarUrl
            }).Skip(skip).Take(take).ToListAsync(ct));
    }

    /// <summary>
    /// Lists published books ranked by recorded detail views.
    /// </summary>
    [HttpGet("books/popular")]
    public async Task<IActionResult> Popular(CancellationToken ct) => Ok(await db.Books.AsNoTracking().Where(b => b.Status == "PUBLISHED").OrderByDescending(b => db.BookViewHistory.Count(v => v.BookId == b.BookId)).ThenBy(b => b.BookId).Take(20).ToListAsync(ct));
    /// <summary>
    /// Records a book view once per event key and rejects conflicting key reuse.
    /// </summary>
    [Authorize, HttpPost("books/{id:long}/views")]
    public async Task<IActionResult> View(long id, ViewRequest request, CancellationToken ct)
    {
        if (!await db.Books.AnyAsync(x => x.BookId == id && x.Status == "PUBLISHED", ct))
        {
            throw new ApiException(404, "Book not found.");
        }

        var existing = await db.BookViewHistory.SingleOrDefaultAsync(v => v.EventKey == request.EventKey, ct);
        if (existing != null)
        {
            if (existing.UserId != UserId || existing.BookId != id)
            {
                throw new ApiException(409, "Event key already used.");
            }

            return NoContent();
        }

        db.BookViewHistory.Add(new()
        {
            UserId = UserId,
            BookId = id,
            EventKey = request.EventKey
        });
        await db.SaveChangesAsync(ct);
        return NoContent();
    }
}

public sealed record ViewRequest([System.ComponentModel.DataAnnotations.Required, System.ComponentModel.DataAnnotations.MaxLength(100)] string EventKey);

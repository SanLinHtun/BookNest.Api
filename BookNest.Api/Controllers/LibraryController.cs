using System.ComponentModel.DataAnnotations;
using BookNest.Api.Data;
using BookNest.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BookNest.Api.Controllers;

[ApiController, Authorize, Route("api/v1/me")]
public sealed class LibraryController(BookNestDbContext db, AccessService access) : UserController
{
    /// <summary>
    /// Returns the user's active library, optionally filtered by reading status.
    /// </summary>
    [HttpGet("library")]
    public async Task<IActionResult> Library(string? status, int page = 1, int pageSize = 20, CancellationToken ct = default)
    {
        var (skip, take) = Page(page, pageSize);
        Require(status == null || status is "NOT_STARTED" or "READING" or "FINISHED", "Invalid reading status.");
        var q =
            from l in db.UserBooks
            join b in db.Books on l.BookId equals b.BookId
            where l.UserId == UserId && l.ArchivedAt == null
            select new
            {
                book = b,
                l.AddedAt,
                progress = db.ReadingProgress.FirstOrDefault(p => p.UserId == UserId && p.BookId == b.BookId)
            };
        if (status != null)
        {
            q = q.Where(x => x.progress != null && x.progress.Status == status);
        }

        return Ok(new
        {
            page,
            pageSize,
            total = await q.CountAsync(ct),
            items = await q.OrderByDescending(x => x.AddedAt).ThenBy(x => x.book.BookId).Skip(skip).Take(take).ToListAsync(ct)
        });
    }

    /// <summary>
    /// Adds or restores library membership without granting paid content access.
    /// </summary>
    [HttpPut("library/{bookId:long}")]
    public async Task<IActionResult> Add(long bookId, CancellationToken ct)
    {
        if (!await db.Books.AnyAsync(b => b.BookId == bookId && b.Status == "PUBLISHED", ct))
        {
            throw new ApiException(404, "Book not found.");
        }

        await access.EnsureLibrary(UserId, bookId, ct);
        await db.SaveChangesAsync(ct);
        return NoContent();
    }

    /// <summary>
    /// Archives the user's library membership without deleting reading history.
    /// </summary>
    [HttpDelete("library/{bookId:long}")]
    public async Task<IActionResult> Archive(long bookId, CancellationToken ct)
    {
        var l = await db.UserBooks.SingleOrDefaultAsync(x => x.UserId == UserId && x.BookId == bookId, ct);
        if (l != null)
        {
            l.ArchivedAt = DateTimeOffset.UtcNow;
            await db.SaveChangesAsync(ct);
        }

        return NoContent();
    }

    /// <summary>
    /// Returns a page of the user's saved books in most recently saved order.
    /// </summary>
    [HttpGet("saved")]
    public async Task<IActionResult> Saved(int page = 1, int pageSize = 20, CancellationToken ct = default)
    {
        var (skip, take) = Page(page, pageSize);
        return Ok(await (
            from s in db.SavedBooks
            join b in db.Books on s.BookId equals b.BookId
            where s.UserId == UserId
            orderby s.SavedAt descending
            select new
            {
                book = b,
                s.SavedAt
            }).Skip(skip).Take(take).ToListAsync(ct));
    }

    /// <summary>
    /// Adds a published book to the wishlist without granting reading access.
    /// </summary>
    [HttpPut("saved/{bookId:long}")]
    public async Task<IActionResult> Save(long bookId, CancellationToken ct)
    {
        if (!await db.Books.AnyAsync(b => b.BookId == bookId && b.Status == "PUBLISHED", ct))
        {
            throw new ApiException(404, "Book not found.");
        }

        if (!await db.SavedBooks.AnyAsync(x => x.UserId == UserId && x.BookId == bookId, ct))
        {
            db.SavedBooks.Add(new()
            {
                UserId = UserId,
                BookId = bookId
            });
            await db.SaveChangesAsync(ct);
        }

        return NoContent();
    }

    /// <summary>
    /// Removes the book from the authenticated user's wishlist.
    /// </summary>
    [HttpDelete("saved/{bookId:long}")]
    public async Task<IActionResult> Unsave(long bookId, CancellationToken ct)
    {
        await db.SavedBooks.Where(x => x.UserId == UserId && x.BookId == bookId).ExecuteDeleteAsync(ct);
        return NoContent();
    }

    /// <summary>
    /// Lists recently viewed published books using the latest view for each book.
    /// </summary>
    [HttpGet("recently-viewed")]
    public async Task<IActionResult> Recent(CancellationToken ct) => Ok(await (
        from b in db.Books
        let viewed = db.BookViewHistory.Where(v => v.UserId == UserId && v.BookId == b.BookId).Max(v => (DateTimeOffset?)v.ViewedAt)
        where viewed != null && b.Status == "PUBLISHED"
        orderby viewed descending
        select new
        {
            book = b,
            viewedAt = viewed
        }).Take(20).ToListAsync(ct));
    /// <summary>
    /// Returns library, reading, wishlist and current-device download counts.
    /// </summary>
    [HttpGet("library/counts")]
    public async Task<IActionResult> Counts(string? deviceKey, CancellationToken ct)
    {
        var active = db.UserBooks.Where(l => l.UserId == UserId && l.ArchivedAt == null);
        var p = db.ReadingProgress.Where(p => p.UserId == UserId && active.Any(l => l.BookId == p.BookId));
        return Ok(new
        {
            totalBooks = await active.CountAsync(ct),
            readingNow = await p.CountAsync(p => p.Status == "READING", ct),
            finished = await p.CountAsync(p => p.Status == "FINISHED", ct),
            saved = await db.SavedBooks.CountAsync(s => s.UserId == UserId, ct),
            downloads = deviceKey == null ? 0 : await db.Downloads.Where(d => d.UserId == UserId && d.DeviceKey == deviceKey && d.Status == "COMPLETED" && (d.AccessExpiresAt == null || d.AccessExpiresAt > DateTimeOffset.UtcNow)).Select(d => d.BookId).Distinct().CountAsync(ct)
        });
    }

    /// <summary>
    /// Creates or updates the authenticated user's review for a published book.
    /// </summary>
    [HttpPut("reviews/{bookId:long}")]
    public async Task<IActionResult> Review(long bookId, ReviewRequest r, CancellationToken ct)
    {
        if (!await db.Books.AnyAsync(b => b.BookId == bookId && b.Status == "PUBLISHED", ct))
        {
            throw new ApiException(404, "Book not found.");
        }

        var review = await db.Reviews.SingleOrDefaultAsync(x => x.UserId == UserId && x.BookId == bookId, ct);
        if (review == null)
        {
            review = new()
            {
                UserId = UserId,
                BookId = bookId
            };
            db.Reviews.Add(review);
        }

        review.Rating = r.Rating;
        review.ReviewText = r.ReviewText;
        await db.SaveChangesAsync(ct);
        return Ok(review);
    }

    /// <summary>
    /// Deletes only the authenticated user's review for the book.
    /// </summary>
    [HttpDelete("reviews/{bookId:long}")]
    public async Task<IActionResult> DeleteReview(long bookId, CancellationToken ct)
    {
        await db.Reviews.Where(r => r.UserId == UserId && r.BookId == bookId).ExecuteDeleteAsync(ct);
        return NoContent();
    }
}

public sealed record ReviewRequest([Range(1, 5)] int Rating, [MaxLength(20000)] string? ReviewText);

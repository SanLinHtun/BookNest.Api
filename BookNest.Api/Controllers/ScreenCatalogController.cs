using BookNest.Api.Data;
using BookNest.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BookNest.Api.Controllers;

/// <summary>Enriched book cards for the Figma catalog and library screens.</summary>
[ApiController, Route("api/v1/screens")]
public sealed class ScreenCatalogController(BookNestDbContext db) : UserController
{
    [HttpGet("catalog"), AllowAnonymous]
    public async Task<IActionResult> Catalog(string? search, long? categoryId, string sort = "popular",
        int page = 1, int pageSize = 20, CancellationToken ct = default)
    {
        var (skip, take) = Page(page, pageSize);
        Require(sort is "popular" or "recent" or "title" or "price", "Invalid sort.");
        var query = db.Books.AsNoTracking().Where(b => b.Status == "PUBLISHED");
        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim().ToLower();
            query = query.Where(b => b.Title.ToLower().Contains(term) || db.BookAuthors.Any(l =>
                l.BookId == b.BookId && db.Authors.Any(a => a.AuthorId == l.AuthorId && a.AuthorName.ToLower().Contains(term))));
        }
        if (categoryId.HasValue)
            query = query.Where(b => db.BookCategories.Any(l => l.BookId == b.BookId &&
                (l.CategoryId == categoryId || db.Categories.Any(c => c.CategoryId == l.CategoryId && c.ParentCategoryId == categoryId))));
        var total = await query.CountAsync(ct);
        var ordered = sort switch
        {
            "recent" => query.OrderByDescending(b => b.CreatedAt).ThenBy(b => b.BookId),
            "title" => query.OrderBy(b => b.Title).ThenBy(b => b.BookId),
            "price" => query.OrderBy(b => b.PurchasePrice).ThenBy(b => b.BookId),
            _ => query.OrderByDescending(b => db.BookViewHistory.Count(v => v.BookId == b.BookId)).ThenBy(b => b.BookId)
        };
        var books = await ordered.Skip(skip).Take(take).ToListAsync(ct);
        return Ok(new { page, pageSize, total, items = await Cards(books, ct) });
    }

    [HttpGet("explore"), AllowAnonymous]
    public async Task<IActionResult> Explore(CancellationToken ct)
    {
        var categories = await db.Categories.AsNoTracking().Where(c => c.IsActive)
            .OrderBy(c => c.SortOrder).ThenBy(c => c.CategoryId).ToListAsync(ct);
        var counts = await (from l in db.BookCategories join b in db.Books on l.BookId equals b.BookId
                            where b.Status == "PUBLISHED" select new { l.CategoryId, l.BookId }).Distinct().ToListAsync(ct);
        return Ok(new { categories = categories.Select(c => new
        {
            c.CategoryId, c.ParentCategoryId, c.Name, c.Slug, c.IconKey, c.BannerImageUrl,
            bookCount = counts.Where(l => l.CategoryId == c.CategoryId || categories.Any(child =>
                child.ParentCategoryId == c.CategoryId && child.CategoryId == l.CategoryId)).Select(l => l.BookId).Distinct().Count()
        }) });
    }

    [HttpGet("library"), Authorize]
    public async Task<IActionResult> Library(string tab = "all", string? search = null, string? deviceKey = null,
        int page = 1, int pageSize = 20, CancellationToken ct = default)
    {
        var (skip, take) = Page(page, pageSize);
        Require(tab is "all" or "reading" or "finished" or "saved" or "downloads" or "notes", "Invalid tab.");
        Require(tab != "downloads" || !string.IsNullOrWhiteSpace(deviceKey), "deviceKey is required for downloads.");
        var query = db.Books.AsNoTracking().Where(b => b.Status == "PUBLISHED");
        query = tab switch
        {
            "saved" => query.Where(b => db.SavedBooks.Any(s => s.UserId == UserId && s.BookId == b.BookId)),
            "downloads" => query.Where(b => db.Downloads.Any(d => d.UserId == UserId && d.BookId == b.BookId && d.DeviceKey == deviceKey && d.Status == "COMPLETED")),
            "notes" => query.Where(b => db.Notes.Any(n => n.UserId == UserId && n.BookId == b.BookId)),
            _ => query.Where(b => db.UserBooks.Any(l => l.UserId == UserId && l.BookId == b.BookId && l.ArchivedAt == null))
        };
        if (tab is "reading" or "finished")
        {
            var status = tab == "reading" ? "READING" : "FINISHED";
            query = query.Where(b => db.ReadingProgress.Any(p => p.UserId == UserId && p.BookId == b.BookId && p.Status == status));
        }
        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim().ToLower();
            query = query.Where(b => b.Title.ToLower().Contains(term) || db.BookAuthors.Any(l => l.BookId == b.BookId &&
                db.Authors.Any(a => a.AuthorId == l.AuthorId && a.AuthorName.ToLower().Contains(term))));
        }
        var total = await query.CountAsync(ct);
        var books = await query.OrderByDescending(b => db.UserBooks.Where(l => l.UserId == UserId && l.BookId == b.BookId)
            .Select(l => (DateTimeOffset?)l.AddedAt).Max()).ThenBy(b => b.BookId).Skip(skip).Take(take).ToListAsync(ct);
        var ids = books.Select(b => b.BookId).ToArray();
        var cards = await Cards(books, ct);
        var progress = await db.ReadingProgress.AsNoTracking().Where(p => p.UserId == UserId && ids.Contains(p.BookId)).ToListAsync(ct);
        var saved = await db.SavedBooks.Where(s => s.UserId == UserId && ids.Contains(s.BookId)).Select(s => s.BookId).ToListAsync(ct);
        var notes = await db.Notes.AsNoTracking().Where(n => n.UserId == UserId && ids.Contains(n.BookId)).OrderByDescending(n => n.UpdatedAt).ToListAsync(ct);
        var chapters = await db.Chapters.AsNoTracking().Where(c => ids.Contains(c.BookId)).ToListAsync(ct);
        return Ok(new { page, pageSize, total, items = books.Select((b, i) =>
        {
            var p = progress.FirstOrDefault(p => p.BookId == b.BookId);
            return new { card = cards[i], progress = p, progressPercent = p == null ? 0 : Math.Clamp((int)Math.Round(100d * p.CurrentPage / b.PageCount), 0, 100),
                currentChapter = chapters.FirstOrDefault(c => c.ChapterId == p?.ChapterId), totalChapters = chapters.Count(c => c.BookId == b.BookId),
                isSaved = saved.Contains(b.BookId), latestNote = notes.FirstOrDefault(n => n.BookId == b.BookId), noteCount = notes.Count(n => n.BookId == b.BookId) };
        }) });
    }

    [HttpGet("collections"), Authorize]
    public async Task<IActionResult> Collections(string tab = "all", string? search = null,
        int page = 1, int pageSize = 20, CancellationToken ct = default)
    {
        var (skip, take) = Page(page, pageSize);
        Require(tab is "all" or "owned" or "shared" or "favorites", "Invalid tab.");
        // All means the user's own collections plus accepted shared memberships, not every public collection.
        var q = db.Collections.AsNoTracking().Where(c => c.UserId == UserId ||
            (c.Visibility == "SHARED" && db.CollectionMembers.Any(m => m.CollectionId == c.CollectionId && m.UserId == UserId && m.Status == "ACCEPTED")));
        if (tab == "owned") q = q.Where(c => c.UserId == UserId);
        if (tab == "shared") q = q.Where(c => c.Visibility == "SHARED");
        if (tab == "favorites") q = q.Where(c => db.CollectionFavorites.Any(f => f.CollectionId == c.CollectionId && f.UserId == UserId));
        if (!string.IsNullOrWhiteSpace(search)) { var term = search.Trim().ToLower(); q = q.Where(c => c.Name.ToLower().Contains(term)); }
        return Ok(new { page, pageSize, total = await q.CountAsync(ct), items = await q.OrderByDescending(c => c.UpdatedAt)
            .ThenBy(c => c.CollectionId).Skip(skip).Take(take).Select(c => new { collection = c,
                bookCount = db.CollectionBooks.Count(l => l.CollectionId == c.CollectionId),
                isFavorite = db.CollectionFavorites.Any(f => f.CollectionId == c.CollectionId && f.UserId == UserId) }).ToListAsync(ct) });
    }

    private async Task<object[]> Cards(List<BookNest.Api.Domain.Book> books, CancellationToken ct)
    {
        var ids = books.Select(b => b.BookId).ToArray();
        var authors = await (from l in db.BookAuthors join a in db.Authors on l.AuthorId equals a.AuthorId
                             where ids.Contains(l.BookId) orderby l.DisplayOrder, a.AuthorId
                             select new { l.BookId, a.AuthorId, a.AuthorName }).ToListAsync(ct);
        var categories = await (from l in db.BookCategories join c in db.Categories on l.CategoryId equals c.CategoryId
                                where ids.Contains(l.BookId) && c.IsActive select new { l.BookId, c.CategoryId, c.Name, c.Slug, l.IsPrimary }).ToListAsync(ct);
        var ratings = await db.Reviews.Where(r => ids.Contains(r.BookId) && r.Status == "PUBLISHED")
            .GroupBy(r => r.BookId).Select(g => new { BookId = g.Key, Rating = g.Average(r => (double)r.Rating), Count = g.Count() }).ToListAsync(ct);
        return books.Select(b => (object)new { book = b,
            authors = authors.Where(a => a.BookId == b.BookId).Select(a => new { a.AuthorId, a.AuthorName }),
            categories = categories.Where(c => c.BookId == b.BookId).Select(c => new { c.CategoryId, c.Name, c.Slug, c.IsPrimary }),
            rating = ratings.FirstOrDefault(r => r.BookId == b.BookId)?.Rating,
            reviewCount = ratings.FirstOrDefault(r => r.BookId == b.BookId)?.Count ?? 0,
            isDemo = b.ContentStorageKey.StartsWith("demo/", StringComparison.Ordinal) }).ToArray();
    }
}

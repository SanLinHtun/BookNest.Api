using BookNest.Api.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BookNest.Api.Controllers;

/// <summary>Public data needed to render the mobile home screen in one request.</summary>
[ApiController, AllowAnonymous, Route("api/v1/home")]
public sealed class HomeController(BookNestDbContext db) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> Get(CancellationToken ct)
    {
        var categories = await db.Categories.AsNoTracking()
            .Where(c => c.IsActive && c.ParentCategoryId == null).OrderBy(c => c.SortOrder).ThenBy(c => c.CategoryId).Take(4)
            .Select(c => new { c.CategoryId, c.Name, c.Slug, c.IconKey }).ToListAsync(ct);

        var books = await db.Books.AsNoTracking().Where(b => b.Status == "PUBLISHED")
            .OrderByDescending(b => db.BookViewHistory.Count(v => v.BookId == b.BookId))
            .ThenBy(b => b.BookId).Take(20).ToListAsync(ct);
        var ids = books.Select(b => b.BookId).ToArray();
        var authors = await (from link in db.BookAuthors.AsNoTracking()
                             join author in db.Authors.AsNoTracking() on link.AuthorId equals author.AuthorId
                             where ids.Contains(link.BookId)
                             orderby link.DisplayOrder, author.AuthorId
                             select new { link.BookId, author.AuthorId, author.AuthorName }).ToListAsync(ct);

        return Ok(new
        {
            // This promotional copy is app content, not an advertised purchase price.
            banners = new[]
            {
                new
                {
                    bannerId = "home-reading",
                    title = "Good Books.\nGreat moments.",
                    subtitle = "Buy, rent and read your favorite books anytime, anywhere.",
                    imageUrl = "/images/home/reading-banner.svg",
                    buttonText = "Explore Now",
                    action = "OPEN_EXPLORE"
                }
            },
            categories,
            popularBooks = books.Select(b => new
            {
                b.BookId,
                b.Title,
                b.CoverImageUrl,
                authors = authors.Where(a => a.BookId == b.BookId)
                    .Select(a => new { a.AuthorId, a.AuthorName }).ToArray(),
                b.PurchasePrice,
                b.RentalPrice,
                b.CurrencyCode,
                b.RentalDays,
                b.PageCount,
                b.LanguageCode,
                b.FileFormat,
                b.PremiumEligible,
                // Seeded demo catalog entries have no downloadable licensed content.
                isDemo = b.ContentStorageKey.StartsWith("demo/", StringComparison.Ordinal)
            })
        });
    }
}

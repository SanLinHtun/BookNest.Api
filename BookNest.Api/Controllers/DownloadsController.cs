using System.ComponentModel.DataAnnotations;
using BookNest.Api.Data;
using BookNest.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BookNest.Api.Controllers;

public sealed record DownloadRequest([Required, MaxLength(100)] string DeviceKey, [Required, RegularExpression("QUEUED|DOWNLOADING|COMPLETED|FAILED|REMOVED")] string Status, [Range(0, long.MaxValue)] long BytesDownloaded);
[ApiController, Authorize, Route("api/v1")]
public sealed class DownloadsController(BookNestDbContext db, AccessService access, IConfiguration config) : UserController
{
    /// <summary>
    /// Returns a page of the user's download records for the specified device.
    /// </summary>
    [HttpGet("me/downloads")]
    public async Task<IActionResult> List([Required, MaxLength(100)] string deviceKey, int page = 1, int pageSize = 20, CancellationToken ct = default)
    {
        var (skip, take) = Page(page, pageSize);
        return Ok(await db.Downloads.AsNoTracking().Where(x => x.UserId == UserId && x.DeviceKey == deviceKey).OrderByDescending(x => x.UpdatedAt).Skip(skip).Take(take).ToListAsync(ct));
    }

    /// <summary>
    /// Checks offline access and file size before saving device download status.
    /// </summary>
    [HttpPut("me/downloads/{bookId:long}")]
    public async Task<IActionResult> Update(long bookId, DownloadRequest r, CancellationToken ct)
    {
        DateTimeOffset? expires = null;
        if (r.Status != "REMOVED")
        {
            expires = await access.RequireAsync(UserId, bookId, true, ct);
        }

        var b = await db.Books.FindAsync([bookId], ct) ?? throw new ApiException(404, "Book not found.");
        Require(b.FileSizeBytes == null || r.BytesDownloaded <= b.FileSizeBytes, "Downloaded bytes exceed file size.");
        Require(r.Status != "COMPLETED" || b.FileSizeBytes == null || r.BytesDownloaded == b.FileSizeBytes, "Incomplete download.");
        var d = await db.Downloads.SingleOrDefaultAsync(x => x.UserId == UserId && x.BookId == bookId && x.DeviceKey == r.DeviceKey, ct);
        if (d == null)
        {
            d = new()
            {
                UserId = UserId,
                BookId = bookId,
                DeviceKey = r.DeviceKey
            };
            db.Downloads.Add(d);
        }

        var renewal = DateTimeOffset.UtcNow.AddDays(7);
        d.ContentVersion = b.ContentVersion;
        d.Status = r.Status;
        d.BytesDownloaded = r.BytesDownloaded;
        d.AccessExpiresAt = expires != null && expires < renewal ? expires : renewal;
        d.DownloadedAt = r.Status == "COMPLETED" ? DateTimeOffset.UtcNow : null;
        await db.SaveChangesAsync(ct);
        return Ok(d);
    }

    /// <summary>
    /// Checks reading or offline access and returns content metadata and license expiry.
    /// </summary>
    [HttpGet("books/{bookId:long}/access")]
    public async Task<IActionResult> Access(long bookId, bool offline = false, CancellationToken ct = default)
    {
        var expires = await access.RequireAsync(UserId, bookId, offline, ct);
        var b = await db.Books.FindAsync([bookId], ct);
        // Offline licenses must periodically be renewed even for permanent purchases.
        var renewal = DateTimeOffset.UtcNow.AddDays(7);
        if (expires != null && expires < renewal)
        {
            renewal = expires.Value;
        }

        return Ok(new
        {
            bookId,
            expiresAt = expires,
            offlineLicenseExpiresAt = renewal,
            b!.ContentVersion,
            b.FileFormat,
            b.FileSizeBytes,
            contentUrl = $"/api/v1/books/{bookId}/content?offline={offline.ToString().ToLowerInvariant()}"
        });
    }

    /// <summary>
    /// Streams a private book file with range support after validating access and storage path.
    /// </summary>
    [HttpGet("books/{bookId:long}/content")]
    public async Task<IActionResult> Content(long bookId, bool offline = false, CancellationToken ct = default)
    {
        await access.RequireAsync(UserId, bookId, offline, ct);
        var b = await db.Books.FindAsync([bookId], ct);
        var configured = config["Content:RootPath"];
        if (string.IsNullOrWhiteSpace(configured))
        {
            throw new ApiException(503, "Content storage is not configured.");
        }

        var root = Path.GetFullPath(configured).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
        var path = Path.GetFullPath(Path.Combine(root, b!.ContentStorageKey));
        if (!path.StartsWith(root, StringComparison.OrdinalIgnoreCase) || !System.IO.File.Exists(path))
        {
            throw new ApiException(404, "Content unavailable.");
        }

        return PhysicalFile(path, b.FileFormat == "EPUB" ? "application/epub+zip" : "application/pdf", enableRangeProcessing: true);
    }
}

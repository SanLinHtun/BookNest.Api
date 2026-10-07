using System.ComponentModel.DataAnnotations;
using BookNest.Api.Data;
using BookNest.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BookNest.Api.Controllers;

public sealed record AnnotationRequest(long BookId, long? ChapterId, [Range(1, int.MaxValue)] int? PageNo, [MaxLength(500)] string? LocationToken, [MaxLength(150)] string? Title, [MaxLength(20000)] string? Text, [MaxLength(500)] string? StartAnchor, [MaxLength(500)] string? EndAnchor, [RegularExpression("YELLOW|GREEN|BLUE|PINK")] string ColorCode = "YELLOW");
[ApiController, Authorize, Route("api/v1/me/annotations/{kind}")]
public sealed class AnnotationsController(BookNestDbContext db, AccessService access) : UserController
{
    /// <summary>
    /// Rejects annotation kinds other than notes, bookmarks and highlights.
    /// </summary>
    private static void Kind(string kind)
    {
        if (kind is not ("notes" or "bookmarks" or "highlights"))
        {
            throw new ApiException(404, "Annotation kind not found.");
        }
    }

    /// <summary>
    /// Returns a page of the user's annotations, optionally restricted to one book.
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> List(string kind, long? bookId, int page = 1, int pageSize = 20, CancellationToken ct = default)
    {
        Kind(kind);
        var (skip, take) = Page(page, pageSize);
        return kind switch
        {
            "notes" => Ok(await db.Notes.AsNoTracking().Where(x => x.UserId == UserId && (bookId == null || x.BookId == bookId)).OrderByDescending(x => x.CreatedAt).ThenBy(x => x.NoteId).Skip(skip).Take(take).ToListAsync(ct)),
            "bookmarks" => Ok(await db.Bookmarks.AsNoTracking().Where(x => x.UserId == UserId && (bookId == null || x.BookId == bookId)).OrderByDescending(x => x.CreatedAt).ThenBy(x => x.BookmarkId).Skip(skip).Take(take).ToListAsync(ct)),
            _ => Ok(await db.Highlights.AsNoTracking().Where(x => x.UserId == UserId && (bookId == null || x.BookId == bookId)).OrderByDescending(x => x.CreatedAt).ThenBy(x => x.HighlightId).Skip(skip).Take(take).ToListAsync(ct))
        };
    }

    /// <summary>
    /// Creates an annotation after checking book access and its content location.
    /// </summary>
    [HttpPost]
    public async Task<IActionResult> Create(string kind, AnnotationRequest r, CancellationToken ct) => await Write(kind, null, r, ct);
    /// <summary>
    /// Updates an owned annotation after checking book access and its content location.
    /// </summary>
    [HttpPut("{id:long}")]
    public async Task<IActionResult> Update(string kind, long id, AnnotationRequest r, CancellationToken ct) => await Write(kind, id, r, ct);
    /// <summary>
    /// Validates annotation fields and saves the matching owned annotation type.
    /// </summary>
    private async Task<IActionResult> Write(string kind, long? id, AnnotationRequest r, CancellationToken ct)
    {
        Kind(kind);
        Require(r.PageNo != null || !string.IsNullOrWhiteSpace(r.LocationToken), "Page or location token required.");
        if (kind is "notes" or "highlights")
        {
            Require(!string.IsNullOrWhiteSpace(r.Text), "Text required.");
        }

        if (kind == "highlights")
        {
            Require(!string.IsNullOrWhiteSpace(r.StartAnchor) && !string.IsNullOrWhiteSpace(r.EndAnchor), "Highlight anchors required.");
        }

        await access.RequireAsync(UserId, r.BookId, ct: ct);
        await access.ValidateLocation(r.BookId, r.ChapterId, r.PageNo, ct);
        object row;
        if (kind == "notes")
        {
            var x = id == null ? new Domain.Note
            {
                UserId = UserId
            }

            : await db.Notes.SingleOrDefaultAsync(x => x.NoteId == id && x.UserId == UserId, ct) ?? throw new ApiException(404, "Note not found.");
            x.BookId = r.BookId;
            x.ChapterId = r.ChapterId;
            x.PageNo = r.PageNo;
            x.LocationToken = r.LocationToken;
            x.Title = r.Title;
            x.NoteText = r.Text!;
            if (id == null)
            {
                db.Notes.Add(x);
            }

            row = x;
        }
        else if (kind == "bookmarks")
        {
            var x = id == null ? new Domain.Bookmark
            {
                UserId = UserId
            }

            : await db.Bookmarks.SingleOrDefaultAsync(x => x.BookmarkId == id && x.UserId == UserId, ct) ?? throw new ApiException(404, "Bookmark not found.");
            x.BookId = r.BookId;
            x.ChapterId = r.ChapterId;
            x.PageNo = r.PageNo;
            x.LocationToken = r.LocationToken;
            x.Label = r.Title;
            if (id == null)
            {
                db.Bookmarks.Add(x);
            }

            row = x;
        }
        else
        {
            var x = id == null ? new Domain.Highlight
            {
                UserId = UserId
            }

            : await db.Highlights.SingleOrDefaultAsync(x => x.HighlightId == id && x.UserId == UserId, ct) ?? throw new ApiException(404, "Highlight not found.");
            x.BookId = r.BookId;
            x.ChapterId = r.ChapterId;
            x.PageNo = r.PageNo;
            x.LocationToken = r.LocationToken;
            x.HighlightedText = r.Text!;
            x.StartAnchor = r.StartAnchor!;
            x.EndAnchor = r.EndAnchor!;
            x.ColorCode = r.ColorCode;
            if (id == null)
            {
                db.Highlights.Add(x);
            }

            row = x;
        }

        await db.SaveChangesAsync(ct);
        return Ok(row);
    }

    /// <summary>
    /// Deletes an annotation only when it belongs to the authenticated user.
    /// </summary>
    [HttpDelete("{id:long}")]
    public async Task<IActionResult> Delete(string kind, long id, CancellationToken ct)
    {
        Kind(kind);
        var count = kind switch
        {
            "notes" => await db.Notes.Where(x => x.NoteId == id && x.UserId == UserId).ExecuteDeleteAsync(ct),
            "bookmarks" => await db.Bookmarks.Where(x => x.BookmarkId == id && x.UserId == UserId).ExecuteDeleteAsync(ct),
            _ => await db.Highlights.Where(x => x.HighlightId == id && x.UserId == UserId).ExecuteDeleteAsync(ct)
        };
        if (count == 0)
        {
            throw new ApiException(404, "Annotation not found.");
        }

        return NoContent();
    }
}

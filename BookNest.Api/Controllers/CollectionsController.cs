using System.ComponentModel.DataAnnotations;
using BookNest.Api.Data;
using BookNest.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BookNest.Api.Controllers;

public sealed record CollectionRequest([Required, MaxLength(100)] string Name, [MaxLength(500)] string? Description, [MaxLength(1000)] string? CoverImageUrl, [Required, RegularExpression("PRIVATE|SHARED|PUBLIC")] string Visibility = "PRIVATE");
public sealed record InviteRequest(long UserId, [Required, RegularExpression("VIEWER|EDITOR")] string Role = "VIEWER");
public sealed record MembershipRequest([Required, RegularExpression("ACCEPTED|DECLINED")] string Status);
public sealed record CollectionBookRequest([Range(0, int.MaxValue)] int SortOrder = 0);
[ApiController, Authorize, Route("api/v1/collections")]
public sealed class CollectionsController(BookNestDbContext db, CollectionAccess access) : UserController
{
    /// <summary>
    /// Lists accessible collections for the owned, shared, favorites or public tab.
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> List(string tab = "owned", int page = 1, int pageSize = 20, CancellationToken ct = default)
    {
        Require(tab is "owned" or "shared" or "favorites" or "public", "Unknown collection tab.");
        var (skip, take) = Page(page, pageSize);
        var q = db.Collections.AsNoTracking().Where(c => c.UserId == UserId || c.Visibility == "PUBLIC" || (c.Visibility == "SHARED" && db.CollectionMembers.Any(m => m.CollectionId == c.CollectionId && m.UserId == UserId && m.Status == "ACCEPTED")));
        q = tab switch
        {
            "owned" => q.Where(c => c.UserId == UserId),
            "shared" => q.Where(c => c.Visibility == "SHARED"),
            "favorites" => q.Where(c => db.CollectionFavorites.Any(f => f.CollectionId == c.CollectionId && f.UserId == UserId)),
            _ => q.Where(c => c.Visibility == "PUBLIC")
        };
        return Ok(await q.OrderByDescending(c => c.UpdatedAt).ThenBy(c => c.CollectionId).Skip(skip).Take(take).ToListAsync(ct));
    }

    /// <summary>
    /// Checks collection visibility and membership before returning its books.
    /// </summary>
    [HttpGet("{id:long}")]
    public async Task<IActionResult> Detail(long id, int page = 1, int pageSize = 20, CancellationToken ct = default)
    {
        var c = await access.Require(id, UserId, false, ct);
        var (skip, take) = Page(page, pageSize);
        return Ok(new
        {
            collection = c,
            books = await (
            from l in db.CollectionBooks
            join b in db.Books on l.BookId equals b.BookId
            where l.CollectionId == id
            orderby l.SortOrder, l.BookId
            select b).Skip(skip).Take(take).ToListAsync(ct)
        });
    }

    /// <summary>
    /// Creates a collection owned by the authenticated user.
    /// </summary>
    [HttpPost]
    public async Task<IActionResult> Create(CollectionRequest r, CancellationToken ct)
    {
        Require(!string.IsNullOrWhiteSpace(r.Name), "Collection name required.");
        var c = new Domain.Collection
        {
            UserId = UserId,
            Name = r.Name.Trim(),
            Description = r.Description,
            CoverImageUrl = r.CoverImageUrl,
            Visibility = r.Visibility
        };
        db.Collections.Add(c);
        await db.SaveChangesAsync(ct);
        return CreatedAtAction(nameof(Detail), new
        {
            id = c.CollectionId
        }, c);
    }

    /// <summary>
    /// Updates collection metadata and visibility only for its owner.
    /// </summary>
    [HttpPut("{id:long}")]
    public async Task<IActionResult> Update(long id, CollectionRequest r, CancellationToken ct)
    {
        Require(!string.IsNullOrWhiteSpace(r.Name), "Collection name required.");
        var c = await access.Owner(id, UserId, ct);
        c.Name = r.Name.Trim();
        c.Description = r.Description;
        c.CoverImageUrl = r.CoverImageUrl;
        c.Visibility = r.Visibility;
        await db.SaveChangesAsync(ct);
        return Ok(c);
    }

    /// <summary>
    /// Deletes an owned collection and its related links in one transaction.
    /// </summary>
    [HttpDelete("{id:long}")]
    public async Task<IActionResult> Delete(long id, CancellationToken ct)
    {
        var c = await access.Owner(id, UserId, ct);
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        await db.CollectionBooks.Where(x => x.CollectionId == id).ExecuteDeleteAsync(ct);
        await db.CollectionMembers.Where(x => x.CollectionId == id).ExecuteDeleteAsync(ct);
        await db.CollectionFavorites.Where(x => x.CollectionId == id).ExecuteDeleteAsync(ct);
        db.Collections.Remove(c);
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        return NoContent();
    }

    /// <summary>
    /// Adds or reorders a published book when the user has collection editing access.
    /// </summary>
    [HttpPut("{id:long}/books/{bookId:long}")]
    public async Task<IActionResult> AddBook(long id, long bookId, CollectionBookRequest r, CancellationToken ct)
    {
        var c = await access.Require(id, UserId, true, ct);
        if (!await db.Books.AnyAsync(b => b.BookId == bookId && b.Status == "PUBLISHED", ct))
        {
            throw new ApiException(404, "Book not found.");
        }

        var row = await db.CollectionBooks.FindAsync([id, bookId], ct);
        if (row == null)
        {
            row = new()
            {
                CollectionId = id,
                BookId = bookId
            };
            db.CollectionBooks.Add(row);
        }

        row.SortOrder = r.SortOrder;
        c.UpdatedAt = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync(ct);
        return NoContent();
    }

    /// <summary>
    /// Removes a book when the user has collection editing access.
    /// </summary>
    [HttpDelete("{id:long}/books/{bookId:long}")]
    public async Task<IActionResult> RemoveBook(long id, long bookId, CancellationToken ct)
    {
        var c = await access.Require(id, UserId, true, ct);
        var row = await db.CollectionBooks.FindAsync([id, bookId], ct);
        if (row != null)
        {
            db.CollectionBooks.Remove(row);
        }

        c.UpdatedAt = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync(ct);
        return NoContent();
    }

    /// <summary>
    /// Favorites a collection only if the user can already access it.
    /// </summary>
    [HttpPut("{id:long}/favorite")]
    public async Task<IActionResult> Favorite(long id, CancellationToken ct)
    {
        await access.Require(id, UserId, false, ct);
        if (!await db.CollectionFavorites.AnyAsync(x => x.UserId == UserId && x.CollectionId == id, ct))
        {
            db.CollectionFavorites.Add(new()
            {
                UserId = UserId,
                CollectionId = id
            });
            await db.SaveChangesAsync(ct);
        }

        return NoContent();
    }

    /// <summary>
    /// Removes the user's favorite selection without changing collection access.
    /// </summary>
    [HttpDelete("{id:long}/favorite")]
    public async Task<IActionResult> Unfavorite(long id, CancellationToken ct)
    {
        await db.CollectionFavorites.Where(x => x.UserId == UserId && x.CollectionId == id).ExecuteDeleteAsync(ct);
        return NoContent();
    }

    /// <summary>
    /// Lists collection invitations and memberships only for the owner.
    /// </summary>
    [HttpGet("{id:long}/members")]
    public async Task<IActionResult> Members(long id, CancellationToken ct)
    {
        await access.Owner(id, UserId, ct);
        return Ok(await db.CollectionMembers.AsNoTracking().Where(m => m.CollectionId == id).ToListAsync(ct));
    }

    /// <summary>
    /// Creates or resets a shared-collection invitation for another active user.
    /// </summary>
    [HttpPut("{id:long}/members")]
    public async Task<IActionResult> Invite(long id, InviteRequest r, CancellationToken ct)
    {
        var c = await access.Owner(id, UserId, ct);
        Require(c.Visibility == "SHARED", "Set collection visibility to SHARED first.");
        Require(r.UserId != UserId, "Owner cannot be invited.");
        if (!await db.Users.AnyAsync(u => u.UserId == r.UserId && u.Status == "ACTIVE", ct))
        {
            throw new ApiException(404, "User not found.");
        }

        var m = await db.CollectionMembers.FindAsync([id, r.UserId], ct);
        if (m == null)
        {
            m = new()
            {
                CollectionId = id,
                UserId = r.UserId
            };
            db.CollectionMembers.Add(m);
        }

        m.Role = r.Role;
        m.Status = "INVITED";
        m.JoinedAt = null;
        await db.SaveChangesAsync(ct);
        return Ok(m);
    }

    /// <summary>
    /// Accepts or declines the authenticated user's pending invitation.
    /// </summary>
    [HttpPut("{id:long}/membership")]
    public async Task<IActionResult> Respond(long id, MembershipRequest r, CancellationToken ct)
    {
        var m = await db.CollectionMembers.FindAsync([id, UserId], ct) ?? throw new ApiException(404, "Invitation not found.");
        Require(m.Status == "INVITED", "Invitation is not pending.");
        m.Status = r.Status;
        m.JoinedAt = r.Status == "ACCEPTED" ? DateTimeOffset.UtcNow : null;
        await db.SaveChangesAsync(ct);
        return Ok(m);
    }

    /// <summary>
    /// Revokes a member's collection access when requested by its owner.
    /// </summary>
    [HttpDelete("{id:long}/members/{memberId:long}")]
    public async Task<IActionResult> Revoke(long id, long memberId, CancellationToken ct)
    {
        await access.Owner(id, UserId, ct);
        var m = await db.CollectionMembers.FindAsync([id, memberId], ct);
        if (m != null)
        {
            m.Status = "REVOKED";
            await db.SaveChangesAsync(ct);
        }

        return NoContent();
    }

    /// <summary>
    /// Lists the authenticated user's pending collection invitations.
    /// </summary>
    [HttpGet("invitations")]
    public async Task<IActionResult> Invitations(CancellationToken ct) => Ok(await db.CollectionMembers.AsNoTracking().Where(m => m.UserId == UserId && m.Status == "INVITED").ToListAsync(ct));
}

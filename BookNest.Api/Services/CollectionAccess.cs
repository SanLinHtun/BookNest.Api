using BookNest.Api.Data;
using BookNest.Api.Domain;
using Microsoft.EntityFrameworkCore;

namespace BookNest.Api.Services;

public sealed class CollectionAccess(BookNestDbContext db)
{
    /// <summary>
    /// Allows access through ownership, public visibility or accepted shared membership.
    /// </summary>
    public async Task<Collection> Require(long id, long userId, bool write, CancellationToken ct)
    {
        var c = await db.Collections.SingleOrDefaultAsync(x => x.CollectionId == id, ct) ?? throw new ApiException(404, "Collection not found.");
        if (c.UserId == userId)
        {
            return c;
        }

        if (!write && c.Visibility == "PUBLIC")
        {
            return c;
        }

        if (c.Visibility == "SHARED" && await db.CollectionMembers.AnyAsync(x => x.CollectionId == id && x.UserId == userId && x.Status == "ACCEPTED" && (!write || x.Role == "EDITOR"), ct))
        {
            return c;
        }

        throw new ApiException(404, "Collection not found.");
    }

    /// <summary>
    /// Returns a collection only if the requesting user owns it.
    /// </summary>
    public async Task<Collection> Owner(long id, long userId, CancellationToken ct) => await db.Collections.SingleOrDefaultAsync(x => x.CollectionId == id && x.UserId == userId, ct) ?? throw new ApiException(404, "Collection not found.");
}

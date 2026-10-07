using System.Security.Claims;
using BookNest.Api.Data;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace BookNest.Api.Services;

public sealed class ApiException(int status, string message) : Exception(message)
{
    public int Status
    {
        get;
    } = status;
}

public sealed class ApiExceptionHandler(IProblemDetailsService problems, ILogger<ApiExceptionHandler> logger) : IExceptionHandler
{
    /// <summary>
    /// Maps request and database failures to safe HTTP Problem Details responses.
    /// </summary>
    public async ValueTask<bool> TryHandleAsync(HttpContext context, Exception exception, CancellationToken ct)
    {
        var (status, title) = exception switch
        {
            ApiException e => (e.Status, e.Message),
            DbUpdateException
            {
                InnerException: PostgresException
                {
                    SqlState: "23505"
                }
            } => (409, "A conflicting record already exists."),
            DbUpdateException
            {
                InnerException: PostgresException
                {
                    SqlState: "23503" or "23514" or "22001"
                }
            } => (400, "The request violates a database constraint."),
            PostgresException
            {
                SqlState: "40001" or "40P01"
            } => (409, "Concurrent change. Retry the request."),
            _ => (500, "An unexpected error occurred.")
        };
        if (status == 500)
        {
            logger.LogError(exception, "Request failed");
        }

        context.Response.StatusCode = status;
        return await problems.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = context,
            ProblemDetails = new ProblemDetails
            {
                Status = status,
                Title = title
            }
        });
    }
}

public abstract class UserController : ControllerBase
{
    protected long UserId => long.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

    /// <summary>
    /// Rejects an invalid request condition with a client-visible validation message.
    /// </summary>
    protected static void Require(bool valid, string message)
    {
        if (!valid)
        {
            throw new ApiException(400, message);
        }
    }

    /// <summary>
    /// Validates pagination limits and calculates the database offset and page size.
    /// </summary>
    protected static (int Skip, int Take) Page(int page, int pageSize)
    {
        Require(page > 0 && page <= 100000 && pageSize is > 0 and <= 100, "page must be 1–100000 and pageSize 1–100.");
        return ((page - 1) * pageSize, pageSize);
    }
}

public sealed class AccessService(BookNestDbContext db)
{
    /// <summary>
    /// Checks paid or premium book access; returns null for permanent access or its expiry.
    /// </summary>
    public async Task<DateTimeOffset?> RequireAsync(long userId, long bookId, bool offline = false, CancellationToken ct = default)
    {
        var now = DateTimeOffset.UtcNow;
        var book = await db.Books.SingleOrDefaultAsync(x => x.BookId == bookId && x.Status == "PUBLISHED", ct) ?? throw new ApiException(404, "Book not found.");
        var grants = await db.BookEntitlements.Where(e => e.UserId == userId && e.BookId == bookId && e.RevokedAt == null && e.StartsAt <= now && (e.ExpiresAt == null || e.ExpiresAt > now)).ToListAsync(ct);
        DateTimeOffset? best = DateTimeOffset.MinValue;
        foreach (var e in grants.Where(e => e.SourceType is "PURCHASE" or "RENTAL"))
        {
            var valid = await (
                from i in db.OrderItems
                join o in db.Orders on i.OrderId equals o.OrderId
                where i.OrderItemId == e.OrderItemId && i.BookId == bookId && i.AccessType == e.SourceType && o.UserId == userId && o.Status == "PAID"
                select i.OrderItemId).AnyAsync(ct);
            if (!valid)
            {
                continue;
            }

            if (e.SourceType == "PURCHASE" && e.ExpiresAt == null)
            {
                return null;
            }

            if (e.ExpiresAt > best)
            {
                best = e.ExpiresAt;
            }
        }

        if (book.PremiumEligible)
        {
            var end = await (
                from s in db.UserSubscriptions
                join p in db.SubscriptionPlans on s.PlanId equals p.PlanId
                where s.UserId == userId && s.StartedAt <= now && s.EndsAt > now && (s.Status == "ACTIVE" || (s.Status == "CANCELLED" && s.CancelAtPeriodEnd)) && (!offline || p.OfflineAccess)
                select (DateTimeOffset?)s.EndsAt).MaxAsync(ct);
            if (end > best)
            {
                best = end;
            }
        }

        if (best > now)
        {
            return best;
        }

        throw new ApiException(403, "A valid purchase, rental or premium subscription is required.");
    }

    /// <summary>
    /// Checks that a page is within the book and a chapter belongs to that book.
    /// </summary>
    public async Task ValidateLocation(long bookId, long? chapterId, int? page, CancellationToken ct)
    {
        var book = await db.Books.FindAsync([bookId], ct) ?? throw new ApiException(404, "Book not found.");
        if (page is not null && (page < 0 || page > book.PageCount))
        {
            throw new ApiException(400, "Page is outside this book.");
        }

        if (chapterId is not null && !await db.Chapters.AnyAsync(c => c.BookId == bookId && c.ChapterId == chapterId, ct))
        {
            throw new ApiException(400, "Chapter does not belong to this book.");
        }
    }

    /// <summary>
    /// Adds or restores library membership; the caller saves the pending change.
    /// </summary>
    public async Task EnsureLibrary(long userId, long bookId, CancellationToken ct)
    {
        var row = await db.UserBooks.SingleOrDefaultAsync(x => x.UserId == userId && x.BookId == bookId, ct);
        if (row == null)
        {
            db.UserBooks.Add(new()
            {
                UserId = userId,
                BookId = bookId
            });
        }
        else
        {
            row.ArchivedAt = null;
        }
    }
}

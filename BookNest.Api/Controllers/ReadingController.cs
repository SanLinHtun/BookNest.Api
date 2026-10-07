using System.ComponentModel.DataAnnotations;
using BookNest.Api.Data;
using BookNest.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BookNest.Api.Controllers;

public sealed record ProgressRequest([Range(0, int.MaxValue)] int CurrentPage, long? ChapterId, [MaxLength(500)] string? LocationToken, [Required, RegularExpression("NOT_STARTED|READING|FINISHED")] string Status);
public sealed record SessionRequest([Required, MaxLength(100)] string ClientSessionKey, long BookId, long? ChapterId, DateTimeOffset StartedAt, DateTimeOffset EndedAt, [Range(0, int.MaxValue)] int ActiveSeconds, [Range(0, int.MaxValue)] int StartPage, [Range(0, int.MaxValue)] int EndPage, [Range(0, int.MaxValue)] int PagesRead);
public sealed record GoalRequest([Range(1, int.MaxValue)] int TargetBooks);
[ApiController, Authorize, Route("api/v1/me/reading")]
public sealed class ReadingController(BookNestDbContext db, AccessService access) : UserController
{
    /// <summary>
    /// Returns the user's saved reading position and derived completion percentage.
    /// </summary>
    [HttpGet("progress/{bookId:long}")]
    public async Task<IActionResult> GetProgress(long bookId, CancellationToken ct)
    {
        var progress = await db.ReadingProgress.AsNoTracking().SingleOrDefaultAsync(x => x.UserId == UserId && x.BookId == bookId, ct);
        if (progress == null)
        {
            throw new ApiException(404, "Progress not found.");
        }

        var pages = await db.Books.Where(b => b.BookId == bookId).Select(b => b.PageCount).SingleAsync(ct);
        return Ok(new
        {
            progress,
            percent = Math.Min(100, Math.Round(100m * progress.CurrentPage / pages, MidpointRounding.AwayFromZero))
        });
    }

    /// <summary>
    /// Checks reading access and location before saving progress and completion timestamps.
    /// </summary>
    [HttpPut("progress/{bookId:long}")]
    public async Task<IActionResult> Progress(long bookId, ProgressRequest r, CancellationToken ct)
    {
        await access.RequireAsync(UserId, bookId, ct: ct);
        await access.ValidateLocation(bookId, r.ChapterId, r.CurrentPage, ct);
        await access.EnsureLibrary(UserId, bookId, ct);
        var p = await db.ReadingProgress.SingleOrDefaultAsync(x => x.UserId == UserId && x.BookId == bookId, ct);
        if (p == null)
        {
            p = new()
            {
                UserId = UserId,
                BookId = bookId
            };
            db.ReadingProgress.Add(p);
        }

        p.CurrentPage = r.CurrentPage;
        p.ChapterId = r.ChapterId;
        p.LocationToken = r.LocationToken;
        p.Status = r.Status;
        var now = DateTimeOffset.UtcNow;
        if (r.Status != "NOT_STARTED")
        {
            p.StartedAt ??= now;
        }

        p.LastReadAt = now;
        p.FinishedAt = r.Status == "FINISHED" ? p.FinishedAt ?? now : null;
        await db.SaveChangesAsync(ct);
        return Ok(p);
    }

    /// <summary>
    /// Records a short active reading interval, accepting retries and rejecting overlaps.
    /// </summary>
    [HttpPost("sessions")]
    public async Task<IActionResult> Session(SessionRequest r, CancellationToken ct)
    {
        var existing = await db.ReadingSessions.SingleOrDefaultAsync(x => x.ClientSessionKey == r.ClientSessionKey, ct);
        if (existing != null)
        {
            if (existing.UserId != UserId || existing.BookId != r.BookId || existing.StartedAt != r.StartedAt.ToUniversalTime() || existing.EndedAt != r.EndedAt.ToUniversalTime() || existing.ActiveSeconds != r.ActiveSeconds || existing.PagesRead != r.PagesRead || existing.StartPage != r.StartPage || existing.EndPage != r.EndPage || existing.ChapterId != r.ChapterId)
            {
                throw new ApiException(409, "Session key was already used with different data.");
            }

            return Ok(existing);
        }

        Require(r.StartedAt.Offset == TimeSpan.Zero && r.EndedAt.Offset == TimeSpan.Zero, "Session timestamps must use UTC.");
        Require(r.EndedAt > r.StartedAt && r.EndedAt <= DateTimeOffset.UtcNow && r.ActiveSeconds <= (r.EndedAt - r.StartedAt).TotalSeconds, "Invalid session interval.");
        // Each heartbeat reports a short contiguous active interval. This permits precise local-day splitting.
        Require((r.EndedAt - r.StartedAt).TotalSeconds <= 60 && r.ActiveSeconds == (r.EndedAt - r.StartedAt).TotalSeconds, "Send contiguous active heartbeat intervals of at most 60 seconds; omit idle time.");
        await access.RequireAsync(UserId, r.BookId, ct: ct);
        await access.ValidateLocation(r.BookId, r.ChapterId, r.StartPage, ct);
        await access.ValidateLocation(r.BookId, r.ChapterId, r.EndPage, ct);
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        // Serialize submissions for this user to prevent concurrent overlapping session inserts.
        await db.Database.ExecuteSqlInterpolatedAsync($"SELECT pg_advisory_xact_lock({UserId})", ct);
        if (await db.ReadingSessions.AnyAsync(x => x.UserId == UserId && x.StartedAt < r.EndedAt && x.EndedAt > r.StartedAt, ct))
        {
            throw new ApiException(409, "Session overlaps previously recorded activity.");
        }

        await access.EnsureLibrary(UserId, r.BookId, ct);
        var row = new Domain.ReadingSession
        {
            UserId = UserId,
            BookId = r.BookId,
            ChapterId = r.ChapterId,
            ClientSessionKey = r.ClientSessionKey,
            StartedAt = r.StartedAt,
            EndedAt = r.EndedAt,
            ActiveSeconds = r.ActiveSeconds,
            StartPage = r.StartPage,
            EndPage = r.EndPage,
            PagesRead = r.PagesRead
        };
        db.ReadingSessions.Add(row);
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        return Ok(row);
    }

    /// <summary>
    /// Creates or updates the user's annual reading target.
    /// </summary>
    [HttpPut("goals/{year:int}")]
    public async Task<IActionResult> SetGoal(int year, GoalRequest r, CancellationToken ct)
    {
        Require(year is >= 1 and < 9999, "Invalid year.");
        var g = await db.ReadingGoals.SingleOrDefaultAsync(x => x.UserId == UserId && x.TargetYear == year, ct);
        if (g == null)
        {
            g = new()
            {
                UserId = UserId,
                TargetYear = year
            };
            db.ReadingGoals.Add(g);
        }

        g.TargetBooks = r.TargetBooks;
        await db.SaveChangesAsync(ct);
        return Ok(g);
    }

    /// <summary>
    /// Returns the annual target and finished-book count within the user's local year.
    /// </summary>
    [HttpGet("goals/{year:int}")]
    public async Task<IActionResult> Goal(int year, CancellationToken ct)
    {
        Require(year is >= 1 and < 9999, "Invalid year.");
        var user = await db.Users.FindAsync([UserId], ct);
        var zone = TimeZoneInfo.FindSystemTimeZoneById(user!.Timezone);
        var start = new DateTimeOffset(TimeZoneInfo.ConvertTimeToUtc(new DateTime(year, 1, 1), zone));
        var end = new DateTimeOffset(TimeZoneInfo.ConvertTimeToUtc(new DateTime(year + 1, 1, 1), zone));
        return Ok(new
        {
            goal = await db.ReadingGoals.AsNoTracking().SingleOrDefaultAsync(x => x.UserId == UserId && x.TargetYear == year, ct),
            finishedBooks = await db.ReadingProgress.CountAsync(x => x.UserId == UserId && x.Status == "FINISHED" && x.FinishedAt >= start && x.FinishedAt < end, ct)
        });
    }

    /// <summary>
    /// Calculates reading statistics for the selected period in the user's timezone.
    /// </summary>
    [HttpGet("statistics")]
    public async Task<IActionResult> Statistics(string period = "week", CancellationToken ct = default)
    {
        Require(period is "week" or "month" or "year" or "all", "Invalid period.");
        var user = await db.Users.FindAsync([UserId], ct);
        var zone = TimeZoneInfo.FindSystemTimeZoneById(user!.Timezone);
        var sessions = await db.ReadingSessions.AsNoTracking().Where(x => x.UserId == UserId && x.EndedAt != null).ToListAsync(ct);
        return Ok(ReadingStatistics.Calculate(sessions, zone, period, DateTimeOffset.UtcNow));
    }
}

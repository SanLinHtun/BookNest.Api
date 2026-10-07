using System.ComponentModel.DataAnnotations;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using BookNest.Api.Controllers;
using BookNest.Api.Data;
using BookNest.Api.Domain;
using BookNest.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Npgsql;

if (args.Contains("--init-test-db"))
{
    var connection = Environment.GetEnvironmentVariable("BOOKNEST_TEST_CONNECTION") ?? throw new Exception("Set BOOKNEST_TEST_CONNECTION to a dedicated empty test database.");
    await using var testDb = new BookNestDbContext(new DbContextOptionsBuilder<BookNestDbContext>().UseNpgsql(connection).Options);
    await using var conn = new NpgsqlConnection(connection);
    await conn.OpenAsync();
    await using var inspect = new NpgsqlCommand("SELECT count(*) FROM information_schema.tables WHERE table_schema = 'public' AND table_type = 'BASE TABLE'", conn);
    if (Convert.ToInt64(await inspect.ExecuteScalarAsync()) != 0)
    {
        throw new Exception("Refusing to initialize a nonempty test database.");
    }

    await using var transaction = await conn.BeginTransactionAsync();
    var script = testDb.Database.GenerateCreateScript() + File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Data", "business-rules.sql"));
    await using var apply = new NpgsqlCommand(script, conn, transaction)
    {
        CommandTimeout = 60
    };
    await apply.ExecuteNonQueryAsync();
    await transaction.CommitAsync();
    Console.WriteLine("Initialized empty test database. No existing data was modified.");
    return;
}

var count = 0;
void Check(bool ok, string description)
{
    if (!ok)
    {
        throw new Exception(description);
    }

    Console.WriteLine("PASS " + description);
    count++;
}

using var db = new BookNestDbContext(new DbContextOptionsBuilder<BookNestDbContext>().UseNpgsql("Host=localhost;Database=booknest;Username=booknest").Options);
Check(db.Model.GetEntityTypes().Count() == 27, "27 mapped tables");
Check(db.Model.GetEntityTypes().Sum(t => t.GetProperties().Count()) == 227, "227 mapped columns without shadow foreign keys");
var sql = db.Database.GenerateCreateScript();
Check(sql.Contains("is_primary = TRUE") && sql.Contains("status = 'ACTIVE'") && sql.Contains("rating BETWEEN 1 AND 5"), "Partial uniqueness and rating constraints");
Check(sql.Contains("FOREIGN KEY (book_id, chapter_id)") && sql.Contains("FOREIGN KEY (user_id, book_id)"), "Composite chapter and library foreign keys");
Check(sql.Contains("DEFAULT (CURRENT_TIMESTAMP)") && sql.Contains("DEFAULT TRUE") && sql.Contains("DEFAULT ('Asia/Tokyo')"), "Workbook SQL defaults retained");
var userJson = JsonSerializer.Serialize(new User
{
    PasswordHash = "secret"
});
var bookJson = JsonSerializer.Serialize(new Book
{
    ContentStorageKey = "private"
});
Check(!userJson.Contains("secret") && !bookJson.Contains("private"), "Password hashes and content keys excluded from JSON");
var ratingValidator = typeof(ReviewRequest).GetConstructors()[0].GetParameters()[0].GetCustomAttributes(typeof(RangeAttribute), false).Cast<RangeAttribute>().Single();
Check(!ratingValidator.IsValid(6) && ratingValidator.IsValid(5), "Out-of-range review rejected");
var zone = TimeZoneInfo.FindSystemTimeZoneById("Asia/Tokyo");
var sessions = new[]
{
    new ReadingSession
    {
        StartedAt = DateTimeOffset.Parse("2026-10-06T14:59:30Z"),
        EndedAt = DateTimeOffset.Parse("2026-10-06T15:00:30Z"),
        ActiveSeconds = 60,
        PagesRead = 3
    }
};
var stats = JsonSerializer.SerializeToElement(ReadingStatistics.Calculate(sessions, zone, "week", DateTimeOffset.Parse("2026-10-07T03:00:00Z")));
Check(stats.GetProperty("totalSeconds").GetDouble() == 60 && stats.GetProperty("daysRead").GetInt32() == 2 && stats.GetProperty("longestStreak").GetInt32() == 2, "Local midnight split preserves seconds and streak");
Check(stats.GetProperty("pagesRead").GetInt32() == 3 && stats.GetProperty("averageSeconds").GetDouble() == 20, "Page turns counted once; averages include elapsed calendar days");
var empty = JsonSerializer.SerializeToElement(ReadingStatistics.Calculate([], zone, "all", DateTimeOffset.Parse("2026-10-07T03:00:00Z")));
Check(empty.GetProperty("averageSeconds").GetDouble() == 0 && empty.GetProperty("percentageChange").ValueKind == JsonValueKind.Null, "Empty statistics avoid division by zero");
var yearSessions = new[]
{
    new ReadingSession
    {
        StartedAt = DateTimeOffset.Parse("2025-10-07T00:00:00Z"),
        EndedAt = DateTimeOffset.Parse("2025-10-07T00:01:00Z"),
        ActiveSeconds = 60
    },
    new ReadingSession
    {
        StartedAt = DateTimeOffset.Parse("2026-10-07T00:00:00Z"),
        EndedAt = DateTimeOffset.Parse("2026-10-07T00:01:00Z"),
        ActiveSeconds = 60
    }
};
var yearStats = JsonSerializer.SerializeToElement(ReadingStatistics.Calculate(yearSessions, zone, "year", DateTimeOffset.Parse("2026-10-07T03:00:00Z")));
Check(yearStats.GetProperty("percentageChange").GetDouble() == 0, "Year comparison uses equal elapsed periods");
var key = "01234567890123456789012345678901";
var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
{

{
 "Payments:WebhookSecret",
 key
}
}).Build();
var payment = new PaymentService(db, new AccessService(db), config);
var body = Encoding.UTF8.GetBytes("{\"amount\":10}");
payment.Verify(body, Convert.ToHexString(HMACSHA256.HashData(Encoding.UTF8.GetBytes(key), body)));
Check(true, "Verified payment adapter HMAC accepted");
try
{
    payment.Verify(Encoding.UTF8.GetBytes("{\"amount\":11}"), Convert.ToHexString(HMACSHA256.HashData(Encoding.UTF8.GetBytes(key), body)));
    throw new Exception("Tampering accepted");
}
catch (ApiException e)
{
    Check(e.Status == 401, "Tampered payment payload rejected");
}

Check(typeof(AdminController).GetCustomAttributes(typeof(AuthorizeAttribute), true).Cast<AuthorizeAttribute>().Single().Policy == "Admin", "Catalog writes require server-assigned admin claim");
Check(typeof(AnnotationsController).IsDefined(typeof(AuthorizeAttribute), true) && typeof(ReadingController).IsDefined(typeof(AuthorizeAttribute), true), "Personal data controllers require authentication");
// Translation checks use the real PostgreSQL provider, without making a database connection.
var q =
    from b in db.Books
    let viewed = db.BookViewHistory.Where(v => v.UserId == 1 && v.BookId == b.BookId).Max(v => (DateTimeOffset?)v.ViewedAt)
    where viewed != null
    orderby viewed descending
    select new
    {
        book = b,
        viewedAt = viewed
    };
Check(q.Take(20).ToQueryString().Contains("max("), "Recently-viewed query translates to PostgreSQL");
var library =
    from l in db.UserBooks
    join b in db.Books on l.BookId equals b.BookId
    where l.UserId == 1 && l.ArchivedAt == null
    select new
    {
        book = b,
        progress = db.ReadingProgress.FirstOrDefault(p => p.UserId == 1 && p.BookId == b.BookId)
    };
Check(library.Take(20).ToQueryString().Contains("reading_progress"), "Library progress query translates to PostgreSQL");
Console.WriteLine($"{count} checks passed.");

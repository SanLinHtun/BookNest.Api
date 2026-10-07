using BookNest.Api.Data;
using BookNest.Api.Services;
using Microsoft.AspNetCore.Authentication.BearerToken;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using BookNest.Api.Domain;
using System.Threading.RateLimiting;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddControllers();
builder.Services.AddOpenApi();
builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<ApiExceptionHandler>();
builder.Services.AddDbContext<BookNestDbContext>(o => o.UseNpgsql(builder.Configuration.GetConnectionString("BookNest") ?? throw new InvalidOperationException("ConnectionStrings:BookNest is required.")));
builder.Services.AddAuthentication(BearerTokenDefaults.AuthenticationScheme).AddBearerToken(o =>
{
    o.BearerTokenExpiration = TimeSpan.FromHours(1);
    o.RefreshTokenExpiration = TimeSpan.FromDays(14);
});
builder.Services.AddAuthorization(o => o.AddPolicy("Admin", p => p.RequireClaim("booknest_admin", "true")));
builder.Services.AddScoped<IPasswordHasher<User>, PasswordHasher<User>>();
builder.Services.AddScoped<AccessService>();
builder.Services.AddScoped<PaymentService>();
builder.Services.AddScoped<CollectionAccess>();
builder.Services.AddRateLimiter(o =>
{
    o.RejectionStatusCode = 429;
    o.AddPolicy("auth", context => RateLimitPartition.GetFixedWindowLimiter(context.Connection.RemoteIpAddress?.ToString() ?? "unknown", _ => new FixedWindowRateLimiterOptions
    {
        PermitLimit = 20,
        Window = TimeSpan.FromMinutes(1),
        QueueLimit = 0
    }));
});
var app = builder.Build();
if (args.Contains("--schema"))
{
    using var scope = app.Services.CreateScope();
    Console.Write(scope.ServiceProvider.GetRequiredService<BookNestDbContext>().Database.GenerateCreateScript());
    Console.Write(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Data", "business-rules.sql")));
    return;
}

app.UseExceptionHandler();
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();
app.UseRateLimiter();
app.UseAuthentication();
// Existing tokens cannot be used by deleted or suspended accounts. Admin claims come from server configuration.
app.Use(async (context, next) =>
{
    if (context.User.Identity?.IsAuthenticated == true)
    {
        var db = context.RequestServices.GetRequiredService<BookNestDbContext>();
        if (!long.TryParse(context.User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value, out var id) || !await db.Users.AnyAsync(u => u.UserId == id && u.Status == "ACTIVE", context.RequestAborted))
        {
            context.Response.StatusCode = 401;
            return;
        }
    }

    await next(context);
});
app.UseAuthorization();
app.MapControllers();
app.MapGet("/health", () => Results.Ok(new
{
    status = "ok"
})).AllowAnonymous();
app.Run();
public partial class Program;

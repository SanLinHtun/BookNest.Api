using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using BookNest.Api.Data;
using BookNest.Api.Domain;
using BookNest.Api.Services;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.BearerToken;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace BookNest.Api.Controllers;

public sealed record RegisterRequest([Required, EmailAddress, MaxLength(254)] string Email, [Required, MinLength(12), MaxLength(128)] string Password, [Required, MaxLength(150)] string DisplayName);
public sealed record LoginRequest([Required, EmailAddress] string Email, [Required, MaxLength(128)] string Password);
public sealed record RefreshRequest([Required] string RefreshToken);
public sealed record ProfileRequest([Required, MaxLength(150)] string DisplayName, [MaxLength(1000)] string? AvatarUrl, [MaxLength(100)] string? Bio, [Required, MaxLength(64)] string Timezone);
[ApiController, Route("api/v1/auth"), EnableRateLimiting("auth")]
public sealed class AuthController(BookNestDbContext db, IPasswordHasher<User> hasher, IConfiguration config, IOptionsMonitor<BearerTokenOptions> options) : UserController
{
    /// <summary>
    /// Creates an account with a hashed password and issues bearer tokens.
    /// </summary>
    [HttpPost("register"), AllowAnonymous]
    public async Task<IActionResult> Register(RegisterRequest request, CancellationToken ct)
    {
        Require(!string.IsNullOrWhiteSpace(request.DisplayName), "Display name is required.");
        var email = request.Email.Trim().ToLowerInvariant();
        if (await db.Users.AnyAsync(x => x.Email == email, ct))
        {
            throw new ApiException(409, "Email already registered.");
        }

        var user = new User
        {
            Email = email,
            DisplayName = request.DisplayName.Trim()
        };
        user.PasswordHash = hasher.HashPassword(user, request.Password);
        db.Users.Add(user);
        await db.SaveChangesAsync(ct);
        return SignIn(Principal(user), BearerTokenDefaults.AuthenticationScheme);
    }

    /// <summary>
    /// Verifies credentials for an active account and issues bearer tokens.
    /// </summary>
    [HttpPost("login"), AllowAnonymous]
    public async Task<IActionResult> Login(LoginRequest request, CancellationToken ct)
    {
        var email = request.Email.Trim().ToLowerInvariant();
        var user = await db.Users.SingleOrDefaultAsync(x => x.Email == email && x.Status == "ACTIVE", ct);
        if (user?.PasswordHash == null)
        {
            throw new ApiException(401, "Invalid credentials.");
        }

        var result = hasher.VerifyHashedPassword(user, user.PasswordHash, request.Password);
        if (result == PasswordVerificationResult.Failed)
        {
            throw new ApiException(401, "Invalid credentials.");
        }

        if (result == PasswordVerificationResult.SuccessRehashNeeded)
        {
            user.PasswordHash = hasher.HashPassword(user, request.Password);
            await db.SaveChangesAsync(ct);
        }

        return SignIn(Principal(user), BearerTokenDefaults.AuthenticationScheme);
    }

    /// <summary>
    /// Validates the refresh token and reissues tokens for an active account.
    /// </summary>
    [HttpPost("refresh"), AllowAnonymous]
    public async Task<IActionResult> Refresh(RefreshRequest request, CancellationToken ct)
    {
        var ticket = options.Get(BearerTokenDefaults.AuthenticationScheme).RefreshTokenProtector.Unprotect(request.RefreshToken);
        if (ticket?.Properties.ExpiresUtc is not
            {
            }
 expires || expires <= DateTimeOffset.UtcNow || !long.TryParse(ticket.Principal.FindFirstValue(ClaimTypes.NameIdentifier), out var id))
        {
            throw new ApiException(401, "Invalid refresh token.");
        }

        var user = await db.Users.SingleOrDefaultAsync(x => x.UserId == id && x.Status == "ACTIVE", ct) ?? throw new ApiException(401, "Account unavailable.");
        return SignIn(Principal(user), BearerTokenDefaults.AuthenticationScheme);
    }

    /// <summary>
    /// Builds identity claims and assigns admin access from server configuration.
    /// </summary>
    private ClaimsPrincipal Principal(User user)
    {
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, user.UserId.ToString()),
            new(ClaimTypes.Name, user.DisplayName)
        };
        if ((config.GetSection("AdminUserIds").Get<long[]>() ?? []).Contains(user.UserId))
        {
            claims.Add(new("booknest_admin", "true"));
        }

        return new(new ClaimsIdentity(claims, BearerTokenDefaults.AuthenticationScheme));
    }
}

[ApiController, Authorize, Route("api/v1/me")]
public sealed class ProfileController(BookNestDbContext db) : UserController
{
    /// <summary>
    /// Returns the authenticated user profile without the password hash.
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> Get(CancellationToken ct) => Ok(await db.Users.AsNoTracking().SingleAsync(x => x.UserId == UserId, ct));
    /// <summary>
    /// Updates editable profile fields after validating the timezone.
    /// </summary>
    [HttpPut]
    public async Task<IActionResult> Update(ProfileRequest request, CancellationToken ct)
    {
        Require(!string.IsNullOrWhiteSpace(request.DisplayName), "Display name required.");
        try
        {
            TimeZoneInfo.FindSystemTimeZoneById(request.Timezone);
        }
        catch (TimeZoneNotFoundException)
        {
            throw new ApiException(400, "Unknown timezone.");
        }

        var user = await db.Users.SingleAsync(x => x.UserId == UserId, ct);
        user.DisplayName = request.DisplayName.Trim();
        user.AvatarUrl = request.AvatarUrl;
        user.Bio = request.Bio;
        user.Timezone = request.Timezone;
        await db.SaveChangesAsync(ct);
        return Ok(user);
    }

    /// <summary>
    /// Deactivates the account while retaining its purchase history.
    /// </summary>
    [HttpDelete]
    public async Task<IActionResult> Delete(CancellationToken ct)
    {
        var user = await db.Users.SingleAsync(x => x.UserId == UserId, ct);
        user.Status = "DELETED";
        await db.SaveChangesAsync(ct);
        return NoContent();
    }
}

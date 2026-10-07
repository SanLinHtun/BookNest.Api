using System.ComponentModel.DataAnnotations;
using System.Text.Json;
using BookNest.Api.Data;
using BookNest.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BookNest.Api.Controllers;

public sealed record CheckoutLine(long BookId, [Required, RegularExpression("PURCHASE|RENTAL")] string AccessType);
public sealed record CheckoutRequest([Required, MinLength(1), MaxLength(100)] CheckoutLine[] Items);
public sealed record SubscriptionRequest(long PlanId);
public sealed record PaymentCallback([Required, RegularExpression("ORDER_PAID|ORDER_REFUNDED|SUBSCRIPTION_PAID")] string Event, long ResourceId, [Required, MaxLength(150)] string Reference, [Range(typeof(decimal), "0", "9999999999.99")] decimal Amount, [Required, StringLength(3, MinimumLength = 3)] string CurrencyCode);
[ApiController, Authorize, Route("api/v1")]
public sealed class CommerceController(BookNestDbContext db) : UserController
{
    /// <summary>
    /// Creates a pending order using server prices and snapshots its access terms.
    /// </summary>
    [HttpPost("me/orders")]
    public async Task<IActionResult> Checkout(CheckoutRequest r, CancellationToken ct)
    {
        Require(r.Items.All(x => x != null && x.AccessType is "PURCHASE" or "RENTAL"), "Invalid checkout item.");
        Require(r.Items.Select(x => x.BookId).Distinct().Count() == r.Items.Length, "Only one line per book allowed.");
        var ids = r.Items.Select(x => x.BookId).ToArray();
        var books = await db.Books.Where(b => ids.Contains(b.BookId) && b.Status == "PUBLISHED").ToDictionaryAsync(b => b.BookId, ct);
        Require(books.Count == ids.Length, "One or more books unavailable.");
        Require(books.Values.Select(b => b.CurrencyCode).Distinct().Count() == 1, "All books must use the same currency.");
        var order = new Domain.Order
        {
            UserId = UserId,
            OrderNumber = "BN-" + Guid.NewGuid().ToString("N"),
            CurrencyCode = books.Values.First().CurrencyCode
        };
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        db.Orders.Add(order);
        await db.SaveChangesAsync(ct);
        foreach (var i in r.Items)
        {
            var b = books[i.BookId];
            var price = i.AccessType == "PURCHASE" ? b.PurchasePrice : b.RentalPrice;
            order.TotalAmount += price;
            db.OrderItems.Add(new()
            {
                OrderId = order.OrderId,
                BookId = b.BookId,
                AccessType = i.AccessType,
                UnitPrice = price,
                LineTotal = price,
                RentalDays = i.AccessType == "RENTAL" ? b.RentalDays : null
            });
        }

        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        return CreatedAtAction(nameof(Order), new
        {
            id = order.OrderId
        }, order);
    }

    /// <summary>
    /// Returns a page of the authenticated user's order history.
    /// </summary>
    [HttpGet("me/orders")]
    public async Task<IActionResult> Orders(int page = 1, int pageSize = 20, CancellationToken ct = default)
    {
        var (skip, take) = Page(page, pageSize);
        return Ok(await db.Orders.AsNoTracking().Where(o => o.UserId == UserId).OrderByDescending(o => o.CreatedAt).Skip(skip).Take(take).ToListAsync(ct));
    }

    /// <summary>
    /// Returns an owned order and its historical book lines.
    /// </summary>
    [HttpGet("me/orders/{id:long}")]
    public async Task<IActionResult> Order(long id, CancellationToken ct)
    {
        var order = await db.Orders.AsNoTracking().SingleOrDefaultAsync(o => o.OrderId == id && o.UserId == UserId, ct) ?? throw new ApiException(404, "Order not found.");
        return Ok(new
        {
            order,
            items = await db.OrderItems.AsNoTracking().Where(i => i.OrderId == id).ToListAsync(ct)
        });
    }

    /// <summary>
    /// Cancels an owned order only while it is pending.
    /// </summary>
    [HttpPost("me/orders/{id:long}/cancel")]
    public async Task<IActionResult> CancelOrder(long id, CancellationToken ct)
    {
        var count = await db.Orders.Where(o => o.OrderId == id && o.UserId == UserId && o.Status == "PENDING").ExecuteUpdateAsync(s => s.SetProperty(o => o.Status, "CANCELLED").SetProperty(o => o.UpdatedAt, DateTimeOffset.UtcNow), ct);
        if (count == 0)
        {
            throw new ApiException(409, "Pending order not found.");
        }

        return NoContent();
    }

    /// <summary>
    /// Lists active subscription plans available for checkout.
    /// </summary>
    [HttpGet("subscription-plans"), AllowAnonymous]
    public async Task<IActionResult> Plans(CancellationToken ct) => Ok(await db.SubscriptionPlans.AsNoTracking().Where(p => p.IsActive).ToListAsync(ct));
    /// <summary>
    /// Lists the authenticated user's subscription contracts.
    /// </summary>
    [HttpGet("me/subscriptions")]
    public async Task<IActionResult> Subscriptions(CancellationToken ct) => Ok(await db.UserSubscriptions.AsNoTracking().Where(s => s.UserId == UserId).OrderByDescending(s => s.CreatedAt).ToListAsync(ct));
    /// <summary>
    /// Creates a pending subscription that grants no access until verified payment.
    /// </summary>
    [HttpPost("me/subscriptions")]
    public async Task<IActionResult> Subscribe(SubscriptionRequest r, CancellationToken ct)
    {
        var p = await db.SubscriptionPlans.SingleOrDefaultAsync(p => p.PlanId == r.PlanId && p.IsActive, ct) ?? throw new ApiException(404, "Plan not found.");
        var now = DateTimeOffset.UtcNow;
        var s = new Domain.UserSubscription
        {
            UserId = UserId,
            PlanId = p.PlanId,
            StartedAt = now,
            EndsAt = p.BillingInterval == "YEAR" ? now.AddYears(1) : now.AddMonths(1)
        };
        db.UserSubscriptions.Add(s);
        await db.SaveChangesAsync(ct);
        return Ok(s);
    }

    /// <summary>
    /// Cancels an owned subscription while retaining any paid access until period end.
    /// </summary>
    [HttpPost("me/subscriptions/{id:long}/cancel")]
    public async Task<IActionResult> CancelSubscription(long id, CancellationToken ct)
    {
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        await db.Database.ExecuteSqlInterpolatedAsync($"SELECT subscription_id FROM user_subscriptions WHERE subscription_id = {id} AND user_id = {UserId} FOR UPDATE", ct);
        var s = await db.UserSubscriptions.SingleOrDefaultAsync(s => s.SubscriptionId == id && s.UserId == UserId, ct) ?? throw new ApiException(404, "Subscription not found.");
        Require(s.Status is "ACTIVE" or "PENDING" or "CANCELLED", "Subscription cannot be cancelled.");
        s.CancelAtPeriodEnd = s.Status == "ACTIVE" || s.CancelAtPeriodEnd;
        s.Status = "CANCELLED";
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        return Ok(s);
    }

    /// <summary>
    /// Lists the user's recorded access grants, including expired or revoked grants.
    /// </summary>
    [HttpGet("me/entitlements")]
    public async Task<IActionResult> Entitlements(CancellationToken ct) => Ok(await db.BookEntitlements.AsNoTracking().Where(e => e.UserId == UserId).ToListAsync(ct));
}

[ApiController, Route("api/v1/payments")]
public sealed class PaymentCallbacksController(PaymentService payments) : ControllerBase
{
    /// <summary>
    /// Verifies the signed callback body and processes its payment or refund event.
    /// </summary>
    [HttpPost("callback"), AllowAnonymous, RequestSizeLimit(16384)]
    public async Task<IActionResult> Callback(CancellationToken ct)
    {
        using var buffer = new MemoryStream();
        await Request.Body.CopyToAsync(buffer, ct);
        var body = buffer.ToArray();
        payments.Verify(body, Request.Headers["X-BookNest-Signature"].FirstOrDefault());
        PaymentCallback? r;
        try
        {
            r = JsonSerializer.Deserialize<PaymentCallback>(body, new JsonSerializerOptions(JsonSerializerDefaults.Web));
        }
        catch (JsonException)
        {
            throw new ApiException(400, "Invalid callback JSON.");
        }

        if (r == null || !TryValidateModel(r))
        {
            return ValidationProblem(ModelState);
        }

        if (r.Event == "SUBSCRIPTION_PAID")
        {
            return Ok(await payments.Activate(r.ResourceId, r.Reference, r.Amount, r.CurrencyCode, ct));
        }

        return Ok(await payments.ConfirmOrder(r.ResourceId, r.Reference, r.Amount, r.CurrencyCode, r.Event == "ORDER_REFUNDED", ct));
    }
}

using System.Security.Cryptography;
using System.Text;
using BookNest.Api.Data;
using BookNest.Api.Domain;
using Microsoft.EntityFrameworkCore;

namespace BookNest.Api.Services;

public sealed class PaymentService(BookNestDbContext db, AccessService access, IConfiguration config)
{
    // A payment adapter verifies the provider callback then signs the exact UTF-8 JSON body for this endpoint.
    /// <summary>
    /// Validates the exact callback body using the trusted payment adapter's HMAC signature.
    /// </summary>
    public void Verify(byte[] body, string? signature)
    {
        var secret = config["Payments:WebhookSecret"];
        if (string.IsNullOrWhiteSpace(secret) || Encoding.UTF8.GetByteCount(secret) < 32)
        {
            throw new ApiException(503, "Payment callbacks are not configured.");
        }

        byte[] provided;
        try
        {
            provided = Convert.FromHexString(signature ?? "");
        }
        catch (FormatException)
        {
            throw new ApiException(401, "Invalid callback signature.");
        }

        var expected = HMACSHA256.HashData(Encoding.UTF8.GetBytes(secret), body);
        if (!CryptographicOperations.FixedTimeEquals(expected, provided))
        {
            throw new ApiException(401, "Invalid callback signature.");
        }
    }

    /// <summary>
    /// Idempotently records verified payment or refund and updates access in one transaction.
    /// </summary>
    public async Task<Order> ConfirmOrder(long id, string reference, decimal amount, string currency, bool refund, CancellationToken ct)
    {
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        // Serialize callbacks by order, including duplicate deliveries and refunds.
        await db.Database.ExecuteSqlInterpolatedAsync($"SELECT order_id FROM orders WHERE order_id = {id} FOR UPDATE", ct);
        var order = await db.Orders.SingleOrDefaultAsync(x => x.OrderId == id, ct) ?? throw new ApiException(404, "Order not found.");
        var lines = await db.OrderItems.Where(x => x.OrderId == id).ToListAsync(ct);
        if (order.TotalAmount != amount || order.CurrencyCode != currency || lines.Sum(x => x.LineTotal) != amount)
        {
            throw new ApiException(409, "Payment amount or currency mismatch.");
        }

        if (refund)
        {
            if (order.Status == "REFUNDED" && order.PaymentReference == reference)
            {
                return order;
            }

            if (order.Status != "PAID" || order.PaymentReference != reference)
            {
                throw new ApiException(409, "Only the matching paid order can be refunded.");
            }

            order.Status = "REFUNDED";
            var ids = lines.Select(x => x.OrderItemId).ToList();
            var grants = await db.BookEntitlements.Where(x => ids.Contains(x.OrderItemId ?? 0)).ToListAsync(ct);
            foreach (var g in grants)
                g.RevokedAt = DateTimeOffset.UtcNow;
            foreach (var bookId in lines.Select(x => x.BookId))
                await db.Downloads.Where(x => x.UserId == order.UserId && x.BookId == bookId).ExecuteUpdateAsync(s => s.SetProperty(x => x.AccessExpiresAt, DateTimeOffset.UtcNow), ct);
        }
        else
        {
            if (order.Status == "PAID" && order.PaymentReference == reference)
            {
                return order;
            }

            if (order.Status != "PENDING")
            {
                throw new ApiException(409, "Order is not pending.");
            }

            order.Status = "PAID";
            order.PaymentReference = reference;
            order.PaidAt = DateTimeOffset.UtcNow;
            // Persist the verified source before grants; both saves remain inside the same transaction.
            await db.SaveChangesAsync(ct);
            foreach (var i in lines)
            {
                db.BookEntitlements.Add(new()
                {
                    UserId = order.UserId,
                    BookId = i.BookId,
                    OrderItemId = i.OrderItemId,
                    SourceType = i.AccessType,
                    StartsAt = order.PaidAt.Value,
                    ExpiresAt = i.AccessType == "RENTAL" ? order.PaidAt.Value.AddDays(i.RentalDays!.Value) : null
                });
                await access.EnsureLibrary(order.UserId, i.BookId, ct);
            }
        }

        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        return order;
    }

    /// <summary>
    /// Activates a verified paid subscription while preventing overlapping current contracts.
    /// </summary>
    public async Task<UserSubscription> Activate(long id, string reference, decimal amount, string currency, CancellationToken ct)
    {
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        await db.Database.ExecuteSqlInterpolatedAsync($"SELECT subscription_id FROM user_subscriptions WHERE subscription_id = {id} FOR UPDATE", ct);
        var sub = await db.UserSubscriptions.SingleOrDefaultAsync(s => s.SubscriptionId == id, ct) ?? throw new ApiException(404, "Subscription not found.");
        await db.Database.ExecuteSqlInterpolatedAsync($"SELECT user_id FROM users WHERE user_id = {sub.UserId} FOR UPDATE", ct);
        await db.Entry(sub).ReloadAsync(ct);
        var plan = await db.SubscriptionPlans.SingleAsync(p => p.PlanId == sub.PlanId, ct);
        if (amount != plan.Price || currency != plan.CurrencyCode)
        {
            throw new ApiException(409, "Subscription payment mismatch.");
        }

        if (sub.Status == "ACTIVE" && sub.ProviderReference == reference)
        {
            return sub;
        }

        if (sub.Status != "PENDING")
        {
            throw new ApiException(409, "Subscription not pending.");
        }

        var now = DateTimeOffset.UtcNow;
        if (await db.UserSubscriptions.AnyAsync(s => s.UserId == sub.UserId && s.SubscriptionId != id && s.EndsAt > now && (s.Status == "ACTIVE" || (s.Status == "CANCELLED" && s.CancelAtPeriodEnd)), ct))
        {
            throw new ApiException(409, "User already has a current subscription.");
        }

        await db.UserSubscriptions.Where(s => s.UserId == sub.UserId && s.Status == "ACTIVE" && s.EndsAt <= now).ExecuteUpdateAsync(s => s.SetProperty(x => x.Status, "EXPIRED"), ct);
        sub.Status = "ACTIVE";
        sub.StartedAt = now;
        sub.EndsAt = plan.BillingInterval == "YEAR" ? now.AddYears(1) : now.AddMonths(1);
        sub.ProviderReference = reference;
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        return sub;
    }
}

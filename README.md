# BookNest backend

ASP.NET Core 10 REST API and PostgreSQL/EF Core backend based on the two supplied BookNest design workbooks. All 27 tables and 227 columns are mapped, including composite keys, foreign keys, lengths, price precision, defaults, status checks and partial unique indexes. Both workbooks were compared; the per-table workbook's display length of 255 for `books.description` is treated as unbounded PostgreSQL TEXT, matching its declared data type.

The mobile API uses camelCase JSON, numeric 64-bit IDs, UTC ISO 8601 timestamps, uppercase status strings, and RFC Problem Details errors. Private endpoints derive user identity from the bearer token. Password hashes and private storage keys never appear in responses. Swift and Android Kotlin connection samples are in `mobile/`; complete frontend screens are outside this backend implementation.

## Start locally

Requirements: .NET 10 SDK and PostgreSQL (or Docker).

1. Set `BOOKNEST_DB_PASSWORD` in your environment and run `docker compose up -d db`, or create a PostgreSQL database named `booknest` with its own application user.
2. Set `ConnectionStrings__BookNest` to `Host=localhost;Port=5432;Database=booknest;Username=booknest;Password=YOUR_PASSWORD`. Keep credentials outside committed settings.
3. Apply `database/001_initial.sql` to the **empty** database with a PostgreSQL SQL client. This script includes cross-table validation triggers. It is an initial schema, not an upgrade script; do not apply it to an existing populated database.
4. Run `dotnet run --project BookNest.Api --launch-profile https`. Local API: `https://localhost:7099`; OpenAPI document in development: `https://localhost:7099/openapi/v1.json`.
5. Register a user via `POST /api/v1/auth/register`. Add its ID to `AdminUserIds` in server configuration (e.g. `AdminUserIds__0=1`), restart and log in again to obtain an admin token. Registration cannot assign admin privileges.
6. Use admin endpoints to add catalog data. Set `Content__RootPath` to a private directory and store book files under relative `contentStorageKey` paths. Content is streamed only after access checks, with HTTP range support.

The server does not create or modify the database on startup. Generate a new initial SQL script after an intentional model change using `dotnet run --project BookNest.Api -- --schema`; production upgrades require reviewed migrations. No invented book data or subscription prices are seeded.

## API routes

All routes below start with `/api/v1`. `page` starts at 1 and `pageSize` is 1–100 for paginated endpoints. Books and library return `{page,pageSize,total,items}`; other paginated lists return arrays. OpenAPI describes request fields and validation.

| Area | Routes |
|---|---|
| Authentication | `POST auth/register`, `POST auth/login`, `POST auth/refresh` |
| Profile | `GET/PUT/DELETE me` (delete deactivates account) |
| Catalog | `GET books`, `GET books/popular`, `GET books/{id}`, `GET books/{id}/chapters`, `GET books/{id}/reviews`, `GET authors`, `GET categories` |
| Discovery | `POST books/{id}/views` with a unique `eventKey`; `GET me/recently-viewed` |
| Library | `GET me/library`, `PUT/DELETE me/library/{bookId}`, `GET me/library/counts?deviceKey=...` |
| Wishlist | `GET me/saved`, `PUT/DELETE me/saved/{bookId}` |
| Reading | `GET/PUT me/reading/progress/{bookId}`, `POST me/reading/sessions`, `GET me/reading/statistics?period=week\|month\|year\|all`, `GET/PUT me/reading/goals/{year}` |
| Annotations | `GET/POST me/annotations/{kind}`, `PUT/DELETE me/annotations/{kind}/{id}`; kind is `notes`, `bookmarks` or `highlights` |
| Reviews | `PUT/DELETE me/reviews/{bookId}` |
| Content/offline | `GET books/{bookId}/access?offline=true`, `GET books/{bookId}/content?offline=true`, `GET me/downloads?deviceKey=...`, `PUT me/downloads/{bookId}` |
| Collections | `GET/POST collections`, `GET/PUT/DELETE collections/{id}`, `PUT/DELETE collections/{id}/books/{bookId}`, `PUT/DELETE collections/{id}/favorite` |
| Sharing | `GET collections/invitations`, `GET/PUT collections/{id}/members`, `PUT collections/{id}/membership`, `DELETE collections/{id}/members/{userId}` |
| Orders | `GET/POST me/orders`, `GET me/orders/{id}`, `POST me/orders/{id}/cancel` |
| Membership | `GET subscription-plans`, `GET/POST me/subscriptions`, `POST me/subscriptions/{id}/cancel`, `GET me/entitlements` |
| Admin | `GET/POST admin/books`, `PUT/DELETE admin/books/{id}`, `POST admin/authors`, `PUT admin/authors/{id}`, `POST admin/categories`, `PUT admin/categories/{id}` |
| Admin content | `POST admin/books/{bookId}/chapters`, `PUT admin/books/{bookId}/chapters/{id}`, `PUT/DELETE admin/books/{bookId}/authors/{authorId}`, `PUT/DELETE admin/books/{bookId}/categories/{categoryId}` |
| Admin membership/moderation | `POST admin/subscription-plans`, `PUT admin/subscription-plans/{id}`, `PUT admin/reviews/{id}/status` |

`BookNest.Api/BookNest.Api.http` contains request examples. Public book endpoints exclude drafts and archived books. Published reviews alone contribute to rating aggregates. Shared collections grant access only after an invitation is accepted; only owners can manage membership, editors can manage books, and personal annotations remain private.

## Mobile authentication and reading

Login/register/refresh return `{tokenType,accessToken,expiresIn,refreshToken}`. Send `Authorization: Bearer ACCESS_TOKEN` on private requests. Tokens are opaque ASP.NET bearer tokens, not JWTs. Access tokens last one hour; refresh tokens last fourteen days. Store refresh tokens in iOS Keychain or Android encrypted private storage. Remove tokens on logout. Refresh tokens can be reused until expiry; per-device revocation and password reset are not implemented. Persist and share ASP.NET Data Protection keys for production instances so issued tokens survive deployment.

Use `Int64` in Swift and `Long` in Kotlin for IDs. Android requires INTERNET permission. Emulator and physical-device base URLs differ from the server's localhost; use reachable HTTPS and a trusted certificate. Mobile sample clients are source examples and have not been compiled in Xcode/Android Studio here. Kotlin calls are blocking and must run off the UI thread. The Swift generic example decodes JSON responses; use a separate no-content request handler for `204` endpoints and streaming download handling for book files.

Adding or saving a book does not grant reading access. Progress, sessions, annotations, content, and downloads check purchase/rental or active premium access. Premium availability is evaluated from the subscription and book eligibility; it does not pre-create a grant for every catalog book. `book_entitlements` supports explicit premium grants in the schema. Offline clients must renew the returned license at least every seven days, and immediately when subscription/rental access expires. They must compare content versions and enforce expiry locally. An API response is not DRM; previously copied content cannot be remotely erased.

Send reading heartbeat intervals containing contiguous active time, lasting at most 60 seconds, with whole-second UTC start/end times. Use a globally unique `clientSessionKey` for retries. Idle time should not be included. Server rejects overlaps across a user's books/devices. Seconds split precisely at local midnight; page turns are assigned to the interval completion day. Statistics use user timezone, Monday weeks, elapsed calendar-day averages, equal elapsed previous-period comparisons, and `null` comparison when the prior period has zero activity. Sessions should be uploaded promptly while the entitlement is valid.

## Payment integration

Order creation uses catalog prices on the server, captures rental terms, and remains `PENDING`. There is no client endpoint that sets paid state. A real payment provider and mobile store receipt verification have **not** been connected. Choose and implement a trusted payment adapter before using live purchases; pending orders/subscriptions alone never grant access.

The adapter must first verify the provider's callback/receipt, successful payment state, user/resource linkage, amount and currency. It then signs the exact UTF-8 JSON body with HMAC-SHA256 using `Payments__WebhookSecret` (at least 32 bytes) and calls `POST /api/v1/payments/callback` with `X-BookNest-Signature` set to the hex digest:

```json
{"event":"ORDER_PAID","resourceId":1,"reference":"verified-provider-reference","amount":1000,"currencyCode":"JPY"}
```

Events: `ORDER_PAID`, `ORDER_REFUNDED`, `SUBSCRIPTION_PAID`. References are stable provider payment references. Refund events reuse the original reference and full paid amount. This is an internal adapter contract, not a direct Stripe/Apple/Google webhook format. Keep its secret on trusted servers. Successful callbacks are idempotent; financial changes, entitlements and library membership commit together. Refund revokes grants and expires cached download licenses. Subscription cancel retains access until its paid period ends. Recurring renewals, subscription refunds, tax, discounts and provider checkout redirects require the chosen provider integration.

## Verification

Run `dotnet run --project BookNest.Verification` to check schema coverage/constraints/defaults, secret exclusion, validation, local-day statistics, payment HMAC tampering, auth policies and PostgreSQL query translation. This suite does not require a database. Run `tools/http-smoke.ps1` to briefly start the API and check health, OpenAPI, anonymous access restrictions and registration validation.

Set `BOOKNEST_TEST_CONNECTION` to a **dedicated empty PostgreSQL test database** and run `tools/integration-check.ps1` to apply the schema and exercise HTTP account/catalog/ownership/payment/refund paths. Never use an existing development or production database for that check. Test records remain for inspection; the script does not drop the database. PostgreSQL/Docker was unavailable in the authoring environment, so this database integration script and the cross-table triggers were not executed here.

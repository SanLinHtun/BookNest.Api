# BookNest Login / Sign Up API

Prepared: 2026-10-20 (Asia/Tokyo).

Base URL: `https://booknest-api-3jl3.onrender.com`

This handoff maps the supplied Login Home, Login and Sign Up screenshot to the deployed API. Email/password register, login and refresh already exist on the server. They issue the same bearer token used by the existing profile, library, notes, collections, goals and reading-statistics APIs. No separate login token system or database migration is needed.

## 1. Screen → action

| Screen control | App action / API |
|---|---|
| Login Home carousel, logo, slogan | App assets and navigation; no API needed |
| Log In button | Navigate to Login, then `POST /api/v1/auth/login` |
| Sign Up button | Navigate to Sign Up, then `POST /api/v1/auth/register` |
| Full Name | Send as `displayName`, not `fullName` |
| Email | Send as `email`; server trims and lowercases it |
| Password | Send as `password`; do not trim or change its case |
| Confirm Password | Compare with Password in the app before signup; not an API field |
| Password eye icon | Toggle locally; no API |
| Email social-row icon | Open the email/password form |
| Google / Facebook buttons | Not implemented. Do not display an active sign-in flow until provider verification is implemented |
| Forgot Password | No reset endpoint or email delivery configured; do not show a successful reset message |
| Terms / Privacy | App links to the actual published documents; do not invent URLs |

Signup requires an email, a nonblank displayName (maximum 150 characters), and a password of **12–128 characters**. Login verifies the existing password. Confirm-password mismatch should stop signup in the app.

## 2. Login request and response

```http
POST /api/v1/auth/login
Content-Type: application/json
Accept: application/json
```

```json
{
  "email": "figma.reader.20261017@example.invalid",
  "password": "<password-from-local-private-credentials-file>"
}
```

HTTP **200** response shape:

```json
{
  "tokenType": "Bearer",
  "accessToken": "<access-token>",
  "expiresIn": 3600,
  "refreshToken": "<refresh-token>"
}
```

The login response contains tokens, not a user/profile object. Call `GET /api/v1/me` next. Access tokens are opaque strings, not JWTs; do not decode them or assume an ID is embedded in a readable form.

## 3. Sign Up request and response

```http
POST /api/v1/auth/register
Content-Type: application/json
```

```json
{
  "displayName": "Your Reader Name",
  "email": "your-own-email@example.com",
  "password": "<your-password-of-at-least-12-characters>"
}
```

HTTP **200** uses the same token response shape as login. The account is already signed in after registration: store the returned tokens, then call `/api/v1/me`. The deployed register endpoint does not perform email verification. A new real account initially has its own empty library; it does not inherit another reader's sample data.

## 4. Connect the existing APIs after login

Every private request must use the **same returned accessToken**:

```http
GET /api/v1/me
Authorization: Bearer <access-token>
Accept: application/json
```

| App area | Request | Auth |
|---|---|---|
| Profile / determine current account | `GET /api/v1/me` | Bearer |
| Home banner / categories / popular books | `GET /api/v1/home` | Public |
| Explore taxonomy | `GET /api/v1/screens/explore` | Public |
| Catalog / search | `GET /api/v1/screens/catalog?search=James%20Clear` | Public |
| My Books | `GET /api/v1/screens/library?tab=all` | Bearer |
| Continue Reading / Reading Now | `GET /api/v1/screens/library?tab=reading` | Bearer |
| Saved / Finished / Notes screens | Same path, `tab=saved`, `finished`, or `notes` | Bearer |
| Collections cards and counts | `GET /api/v1/screens/collections?tab=all` | Bearer |
| Library counters | `GET /api/v1/me/library/counts?deviceKey=<device-id>` | Bearer |
| Notes list | `GET /api/v1/me/annotations/notes?bookId=<id>` | Bearer |
| Bookmarks / Highlights | `/api/v1/me/annotations/bookmarks` or `/highlights` | Bearer |
| Reading chart | `GET /api/v1/me/reading/statistics?period=week` | Bearer |
| Reading goal | `GET /api/v1/me/reading/goals/2026` | Bearer |

Do not send userId to select another account's data. The server obtains it from the authenticated token. Data belongs to the **user account**, not to an individual token string. Signing in again or refreshing tokens retains the same user's existing data.

Recommended sequence: login/register → store tokens → fetch profile → navigate Home → load private screen data when that screen opens. Fetch counts when needed; avoid requesting every note, collection and chart just to display the Login screen.

## 5. Refresh, restore session and sign out

Access token lifetime: **1 hour**. Refresh token lifetime: **14 days** from issuance.

```http
POST /api/v1/auth/refresh
Content-Type: application/json
```

```json
{
  "refreshToken": "<stored-refresh-token>"
}
```

HTTP **200**:

```json
{
  "tokenType": "Bearer",
  "accessToken": "<new-access-token>",
  "expiresIn": 3600,
  "refreshToken": "<new-refresh-token>"
}
```

Replace both stored tokens with the new response. On app startup, restore the stored session and validate it with `/api/v1/me`. If access returns 401, perform one refresh, then retry the original request once. If refresh also returns 401, clear tokens and return to Login. Serialize concurrent refresh attempts. Never retry indefinitely.

Local sign-out clears access/refresh tokens, current profile and cached private screen data. The server currently has no logout/revocation endpoint; clearing local tokens does not revoke copies already issued. Do not use `DELETE /api/v1/me` for logout: that deactivates the account.

Render currently has no persistent Data Protection key store. A server restart/deployment can invalidate existing access and refresh tokens; sign in again when needed. Token persistence across deployments requires a separate durable key-storage setup.

Keep credentials/tokens in secure device storage. The Android sample holds only the current access token in memory; the real app must add secure persistence for the refresh token. Do not save them in logs, source control, screenshots or plain preferences. Allow a slow initial request on the free server; the sample uses 90-second network timeouts.

## 6. Screen error handling

| Status | Meaning / app behavior |
|---|---|
| 400 | Invalid email, missing field or registration password shorter than 12 characters; show field validation |
| 401 on login | Incorrect credentials or unavailable account; show a generic login error |
| 401 on a private API | Missing, invalid or expired token; attempt refresh once |
| 401 on refresh | Session no longer valid; require login |
| 409 on signup | Email already registered; offer Login |
| 429 | Authentication rate limit; wait before trying again |
| 5xx / timeout | Server/network issue; preserve user input and offer retry |

Wrong-password response example (additional Problem Details properties may be returned):

```json
{
  "status": 401,
  "title": "Invalid credentials."
}
```

Duplicate-email response example:

```json
{
  "status": 409,
  "title": "Email already registered."
}
```

Model validation returns a validation Problem Details response with an `errors` dictionary. Missing-token 401 and rate-limit 429 may have no JSON body; check the HTTP status before decoding. Do not show raw server messages containing operational details. For creation APIs returning 201, read the response body directly; do not automatically follow Location links or downgrade an HTTPS request to HTTP.

## 7. Demo accounts and data

**Populated Figma reader**: use the credentials in local ignored `outputs/figma-integration/demo-login.private.json`. This account has the original 31-book library, 6 Reading, 12 Finished, 8 Saved, 5 books with Notes, 6 collections, bookmark/highlight examples and reading history. A fresh login token reads those same records.

**Signup test reader**: local ignored `outputs/login-api/signup-demo.private.json`. Created through the normal register API, with one demo book in its library and saved list, one private collection and a 24-book goal. Its data is separate from the populated reader. Credentials are intentionally omitted from this shareable document.

These fixtures grant no paid ebook access or subscription. Reading existing metadata/notes is different from acquiring permission to create content annotations or download a commercial ebook. Licensed files, payment activation, Google/Facebook verification and password-reset delivery still require separate integration. No database schema change was needed.

## 8. Android sample integration

Updated sample: `mobile/BookNestClient.kt` (Android-specific; not the full mobile app).

```kotlin
// Run blocking sample calls on Dispatchers.IO / a worker thread.
val api = BookNestClient("https://booknest-api-3jl3.onrender.com")
val session = api.login(email, password)
// Store session.refreshToken in secure device storage.
val profile = api.profile()
val home = api.home()
val myBooks = api.library("all")
val readingNow = api.library("reading")
val notes = api.library("notes")
val collections = api.collections()

// Signup checks Confirm Password in the client before calling the API:
// val session = api.register(fullName, email, password, confirmPassword)

// Refresh after access expiry:
// val renewed = api.refresh(storedRefreshToken)
// Store renewed.refreshToken and retry the failed request once.

// Local sign-out:
// api.clearSession()
// Also remove securely stored tokens and cached private data in the app.
```

The same `BookNestClient` instance retains the issued access token and sends it on private requests. Creating a new instance loses its in-memory token; restore a valid stored access token or sign in again. Full response examples follow; arrays are shortened for readability, and IDs/dates must not be hardcoded.

## 9. Verified response examples

Responses below were verified on 2026-10-20 using the deployed server and a fresh login token. Arrays are shortened to one record; total counts retain their actual values. Private credential files and actual token strings are excluded.

### Profile

`GET /api/v1/me`

HTTP 200:

```json
{
  "userId": 1,
  "email": "figma.reader.20261017@example.invalid",
  "displayName": "BookNest Demo Reader",
  "avatarUrl": null,
  "bio": null,
  "timezone": "Asia/Tokyo",
  "status": "ACTIVE",
  "createdAt": "2026-10-17T17:01:41.440772+09:00",
  "updatedAt": "2026-10-17T17:01:41.440773+09:00"
}
```

### Library after login

`GET /api/v1/screens/library?pageSize=100`

HTTP 200:

```json
{
  "page": 1,
  "pageSize": 100,
  "total": 31,
  "items": [
    {
      "card": {
        "book": {
          "bookId": 1,
          "title": "Atomic Habits",
          "description": "DEMO catalog entry for UI integration. Prices and page count are sample values; no ebook content is provided.",
          "coverImageUrl": "https://booknest-api-3jl3.onrender.com/images/figma/atomic-habits.png",
          "isbn": null,
          "publisherName": null,
          "publishedDate": null,
          "languageCode": "en",
          "pageCount": 100,
          "purchasePrice": 1200.0,
          "rentalPrice": 300.0,
          "currencyCode": "JPY",
          "rentalDays": 30,
          "previewUrl": null,
          "fileFormat": "PDF",
          "fileSizeBytes": null,
          "contentVersion": 1,
          "premiumEligible": false,
          "status": "PUBLISHED",
          "createdAt": "2026-10-17T12:51:05.568329+09:00",
          "updatedAt": "2026-10-17T17:05:32.804816+09:00"
        },
        "authors": [
          {
            "authorId": 1,
            "authorName": "James Clear"
          }
        ],
        "categories": [
          {
            "categoryId": 4,
            "name": "Self-help",
            "slug": "self-help",
            "isPrimary": true
          },
          {
            "categoryId": 30,
            "name": "Productivity",
            "slug": "self-help-productivity",
            "isPrimary": false
          }
        ],
        "rating": null,
        "reviewCount": 0,
        "isDemo": true
      },
      "progress": {
        "progressId": 1,
        "userId": 1,
        "bookId": 1,
        "chapterId": 77,
        "currentPage": 33,
        "locationToken": null,
        "status": "READING",
        "startedAt": "2026-10-03T17:02:14.361062+09:00",
        "lastReadAt": "2026-10-17T16:02:14.361062+09:00",
        "finishedAt": null,
        "updatedAt": "2026-10-17T17:02:14.361062+09:00"
      },
      "progressPercent": 33,
      "currentChapter": {
        "chapterId": 77,
        "bookId": 1,
        "chapterNo": 2,
        "title": "Demo section 2",
        "startPage": 26,
        "endPage": 50,
        "sortOrder": 2,
        "createdAt": "2026-10-17T17:01:05.972172+09:00",
        "updatedAt": "2026-10-17T17:01:05.972172+09:00"
      },
      "totalChapters": 4,
      "isSaved": false,
      "latestNote": {
        "noteId": 1,
        "userId": 1,
        "bookId": 1,
        "chapterId": 55,
        "pageNo": 1,
        "locationToken": null,
        "title": "Figma demo note",
        "noteText": "Original demo note: remember to make time for reading today.",
        "createdAt": "2026-10-17T17:02:14.361062+09:00",
        "updatedAt": "2026-10-17T17:02:14.361062+09:00"
      },
      "noteCount": 1
    }
  ]
}
```

### Counts

`GET /api/v1/me/library/counts`

HTTP 200:

```json
{
  "totalBooks": 31,
  "readingNow": 6,
  "finished": 12,
  "saved": 8,
  "downloads": 0
}
```

### Collections

`GET /api/v1/screens/collections?tab=all`

HTTP 200:

```json
{
  "page": 1,
  "pageSize": 20,
  "total": 6,
  "items": [
    {
      "collection": {
        "collectionId": 1,
        "userId": 1,
        "name": "Personal Development",
        "description": "Figma demo collection",
        "coverImageUrl": null,
        "visibility": "PRIVATE",
        "createdAt": "2026-10-17T17:02:14.361062+09:00",
        "updatedAt": "2026-10-17T17:02:14.361062+09:00"
      },
      "bookCount": 8,
      "isFavorite": false
    }
  ]
}
```

### Notes list

`GET /api/v1/me/annotations/notes`

HTTP 200:

```json
[
  {
    "noteId": 1,
    "userId": 1,
    "bookId": 1,
    "chapterId": 55,
    "pageNo": 1,
    "locationToken": null,
    "title": "Figma demo note",
    "noteText": "Original demo note: remember to make time for reading today.",
    "createdAt": "2026-10-17T17:02:14.361062+09:00",
    "updatedAt": "2026-10-17T17:02:14.361062+09:00"
  }
]
```

### Reading statistics

`GET /api/v1/me/reading/statistics?period=week`

HTTP 200:

```json
{
  "period": "week",
  "timezone": "Asia/Tokyo",
  "start": "2026-10-19",
  "end": "2026-10-20",
  "totalSeconds": 0,
  "daysRead": 0,
  "averageSeconds": 0,
  "pagesRead": 0,
  "longestStreak": 0,
  "percentageChange": -100,
  "breakdown": []
}
```

### Annual goal

`GET /api/v1/me/reading/goals/2026`

HTTP 200:

```json
{
  "goal": {
    "goalId": 1,
    "userId": 1,
    "targetYear": 2026,
    "targetBooks": 24,
    "createdAt": "2026-10-17T17:02:14.361062+09:00",
    "updatedAt": "2026-10-17T17:02:14.361062+09:00"
  },
  "finishedBooks": 12
}
```

### Separate signup account library

`GET /api/v1/screens/library`

HTTP 200:

```json
{
  "page": 1,
  "pageSize": 20,
  "total": 1,
  "items": [
    {
      "card": {
        "book": {
          "bookId": 1,
          "title": "Atomic Habits",
          "description": "DEMO catalog entry for UI integration. Prices and page count are sample values; no ebook content is provided.",
          "coverImageUrl": "https://booknest-api-3jl3.onrender.com/images/figma/atomic-habits.png",
          "isbn": null,
          "publisherName": null,
          "publishedDate": null,
          "languageCode": "en",
          "pageCount": 100,
          "purchasePrice": 1200.0,
          "rentalPrice": 300.0,
          "currencyCode": "JPY",
          "rentalDays": 30,
          "previewUrl": null,
          "fileFormat": "PDF",
          "fileSizeBytes": null,
          "contentVersion": 1,
          "premiumEligible": false,
          "status": "PUBLISHED",
          "createdAt": "2026-10-17T12:51:05.568329+09:00",
          "updatedAt": "2026-10-17T17:05:32.804816+09:00"
        },
        "authors": [
          {
            "authorId": 1,
            "authorName": "James Clear"
          }
        ],
        "categories": [
          {
            "categoryId": 4,
            "name": "Self-help",
            "slug": "self-help",
            "isPrimary": true
          },
          {
            "categoryId": 30,
            "name": "Productivity",
            "slug": "self-help-productivity",
            "isPrimary": false
          }
        ],
        "rating": null,
        "reviewCount": 0,
        "isDemo": true
      },
      "progress": null,
      "progressPercent": 0,
      "currentChapter": null,
      "totalChapters": 4,
      "isSaved": true,
      "latestNote": null,
      "noteCount": 0
    }
  ]
}
```

## 10. Files and verification

- `LOGIN_API.md`: this shareable login/signup handoff with response shapes.
- `mobile/BookNestClient.kt`: compiled Android helper example; register/login/refresh and authenticated screen calls use the same client.
- `outputs/login-api/Login_API_Requests.http`: copyable requests with token placeholders.
- `outputs/login-api/verify-login.ps1`: repeatable integration verification; reads ignored local demo credentials, verifies existing API access/refresh/isolation and ensures the signup fixture has sample records. It does not print real tokens/passwords.
- `outputs/login-api/*.json`: full verified responses, with token-response artifacts redacted.
- `FIGMA_API_AUDIT.md`: full catalog/Home/library/collections response examples and remaining feature limitations.

Validation: live login, signup, refresh, wrong-password 401, invalid-refresh 401, unauthenticated private-request 401, ten existing authenticated endpoints and separate-account data isolation passed. The updated Android helper compiles against the installed Android SDK. No new authentication endpoint was needed; the deployed endpoints already support this workflow. The mobile application itself has not been rebuilt or wired to these sample helpers in this task.

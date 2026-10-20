# BookNest Figma → API handoff

Design: https://www.figma.com/design/QyCgu0IiouHSf7QKYM9hLY/BookNest

Reviewed 2026-10-17. Metadata covers 29 screen frames. Detailed design context was obtained for Home Bottom, Explore Top/Bottom, BookDetail, Library, My Books Top/Bottom, Notes, More, Edit Profile, Profile Bottom and reading-time Month/Year/All Time. The Figma Starter MCP quota blocked further detailed fetches; remaining frames were assessed from their text/structure, not a complete visual review.

Base URL: `https://booknest-api-3jl3.onrender.com`

Login / Sign Up screen integration, token refresh, demo accounts and authenticated response shapes: [LOGIN_API.md](LOGIN_API.md). Use the token returned by that login for the private screen endpoints below.

## New screen endpoints

| Screen | Request | Notes |
|---|---|---|
| Explore/category grid | `GET /api/v1/screens/explore` | Parent IDs, icons, banner URL, actual distinct published book counts. Filter roots with `parentCategoryId == null`. “All” and “More” are app controls, not fake categories. |
| All Books / category / Popular / search | `GET /api/v1/screens/catalog?sort=popular&page=1&pageSize=20` | `sort=popular|recent|title|price`; optional `search`, `categoryId`. Search matches author/title case-insensitively. Parent category filters include direct children. |
| Library/My Books/Reading Now/Saved/Finished/Notes | `GET /api/v1/screens/library?tab=reading` | Bearer required. Tabs `all|reading|saved|finished|downloads|notes`. Optional title/author search. `downloads` requires `deviceKey`. Most recently added library first. |
| Collections grid | `GET /api/v1/screens/collections?tab=all` | Bearer required. `all|owned|shared|favorites`, optional search; updated first; actual book count and favorite state. Only own/accepted shared memberships. |

Pagination: page >= 1, pageSize 1–100. Catalog response: `{page,pageSize,total,items:[{book,authors,categories,rating,reviewCount,isDemo}]}`. Library items: `{card,progress,progressPercent,currentChapter,totalChapters,isSaved,latestNote,noteCount}`. Ratings are null with zero reviews; never fabricated from the mockup.

## Existing endpoints to combine

| Figma feature | Existing API |
|---|---|
| Home banners/categories/popular | `GET /api/v1/home` (four root categories; Explore returns full taxonomy) |
| Continue Reading | authenticated screen library `tab=reading`; use `progress.lastReadAt` to select the most recent card |
| Book information/reviews/chapters | `GET /api/v1/books/{id}`, `/reviews`, `/chapters` |
| Recent views | `GET /api/v1/me/recently-viewed`; record using `POST /api/v1/books/{id}/views` with unique eventKey |
| Profile/edit | `GET/PUT /api/v1/me`; email is immutable; avatar is a URL |
| Library counts | `GET /api/v1/me/library/counts?deviceKey=...` |
| Week/Month/Year/All Time | `GET /api/v1/me/reading/statistics?period=week|month|year|all` |
| Reading goal | `GET/PUT /api/v1/me/reading/goals/{year}` |
| Reader progress | `GET/PUT /api/v1/me/reading/progress/{bookId}` |
| Reader notes/highlights/bookmarks | `/api/v1/me/annotations/{notes|highlights|bookmarks}` |
| Downloads/access | `/api/v1/me/downloads`, `/api/v1/books/{id}/access`, `/content` |
| Collection membership/favorites | `/api/v1/collections` routes in README/OpenAPI |

All `/me` endpoints and the two private screen endpoints require `Authorization: Bearer <accessToken>`. Login with `POST /api/v1/auth/login` JSON `{email,password}`. Access token lasts 1 hour; refresh through `/api/v1/auth/refresh`. Tokens are opaque: do not decode as JWT. After deployment/restart, sign in again if the token is rejected.

## Neon fixtures

31 clearly marked demo books (4 existing Home + 27 additional Figma titles), 16 root categories + 16 subcategories, 124 synthetic chapters (four per book). New covers use an explicit placeholder except the four Home covers, which were exported from the provided Figma design. Prices/page counts are UI examples, not a verified commercial catalog. Chapter titles/page ranges are synthetic, not real book contents.

Separate demo user: **BookNest Demo Reader**. Credentials are in the local ignored `outputs/figma-integration/demo-login.private.json`; they are not published to GitHub. Its fixtures contain 31 library entries, 6 Reading, 12 Finished, 8 Saved, 5 Notes, 6 private collections, 7 reading sessions and a 24-book annual goal. Empty collections are valid; UI counts must come from actual rows, not the inconsistent mockup numbers. These fixtures grant no purchases, subscription or ebook entitlement. Existing users were not edited.

Catalog/taxonomy scripts in `database/002_figma_demo_catalog.sql` and `003_figma_demo_taxonomy.sql` are repeatable and only add missing rows. Do not run the initial schema again on an initialized database.

## Database changes / remaining integration

**No table or column migration is required for the inspected screens.** Existing tables support the catalog, taxonomy, user library, collections, annotations, progress, goals and reading statistics.

- Licensed PDF/EPUB content, preview files and persistent content storage still need to be supplied. Demo catalog metadata is not a readable commercial ebook. `/content` remains unavailable without storage and valid entitlement.
- Download fixtures are intentionally not marked completed: no ebook files have been downloaded. The app should show the real device download state.
- Payments and subscription activation need a real payment-provider integration; no fake paid orders were inserted.
- Avatar upload/storage, notification feed, server-synchronized reader/download preferences and backup/restore are not implemented. The inspected designs show menus, not detailed requirements. If these must be server features, define their behavior before adding settings/notification tables and endpoints. Device-local preferences need no DB change.
- Reading-statistic buckets contain days with activity. The app should insert zero buckets for missing chart labels. Compute percentages/counts from responses; the design contains inconsistent page percentages and time totals.
- Some category filters intentionally have zero books. Empty-state handling is required. This is an integration fixture set, not a populated production store.

## Validation

Build succeeds. 20 verification checks cover existing behavior, private screen authorization and PostgreSQL query translation. HTTP smoke checks cover health, OpenAPI (85 operations), authentication and validation. Live endpoint checks and exported sample JSON are saved under `outputs/figma-integration/`.

## Request and response examples

Examples below use the verified demo responses saved on 2026-10-17. IDs, dates, totals and prices are sample values; always use the current response. Arrays are shortened to one item (Explore shows one parent and one child). All properties on the shown item are preserved. `total` is the full matching count, not the shortened example's array length. Null is a valid value, not a missing field.

### Login and token

`POST /api/v1/auth/login` — public; `Content-Type: application/json`.

Request (replace both example values):

```json
{
  "email": "your_demo_email@example.invalid",
  "password": "your_demo_password"
}
```

Successful response shape (token strings are placeholders):

```json
{
  "tokenType": "Bearer",
  "accessToken": "<access-token>",
  "expiresIn": 3600,
  "refreshToken": "<refresh-token>"
}
```

For every protected request below, send `Authorization: Bearer <accessToken>`. GET requests have no JSON body. Keep tokens in secure app storage; do not hardcode them. Refresh with `POST /api/v1/auth/refresh`, request `{"refreshToken":"<refresh-token>"}`; its successful response has the same token shape. Invalid/expired access tokens return HTTP 401; do not assume a JSON error body for that response.

### Explore — category grid

`GET /api/v1/screens/explore`

Public. Render roots in the grid; render categories with a parentCategoryId as that parent category's filter chips. bookCount is computed from published books.

HTTP 200 response example:

```json
{
  "categories": [
    {
      "categoryId": 1,
      "parentCategoryId": null,
      "name": "Fiction",
      "slug": "fiction",
      "iconKey": "book-open",
      "bannerImageUrl": null,
      "bookCount": 6
    },
    {
      "categoryId": 20,
      "parentCategoryId": 1,
      "name": "Fantasy",
      "slug": "fiction-fantasy",
      "iconKey": "book-open",
      "bannerImageUrl": null,
      "bookCount": 2
    }
  ]
}
```

### Catalog — All Books / Popular / category search

`GET /api/v1/screens/catalog?page=1&pageSize=100&sort=price`

Public. Optional: search=CAL%20NEWPORT, categoryId=<id>, sort=popular|recent|title|price. Use items[].book for title/cover/prices and items[].authors for author labels. rating may be null.

HTTP 200 response example:

```json
{
  "page": 1,
  "pageSize": 100,
  "total": 31,
  "items": [
    {
      "book": {
        "bookId": 4,
        "title": "Ikigai",
        "description": "DEMO catalog entry for UI integration. Prices and page count are sample values; no ebook content is provided.",
        "coverImageUrl": "https://booknest-api-3jl3.onrender.com/images/figma/ikigai.png",
        "isbn": null,
        "publisherName": null,
        "publishedDate": null,
        "languageCode": "en",
        "pageCount": 100,
        "purchasePrice": 800.0,
        "rentalPrice": 200.0,
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
          "authorId": 4,
          "authorName": "Hector Garcia"
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
          "categoryId": 31,
          "name": "Happiness",
          "slug": "self-help-happiness",
          "isPrimary": false
        }
      ],
      "rating": null,
      "reviewCount": 0,
      "isDemo": true
    }
  ]
}
```

### Library — Reading Now / Continue Reading

`GET /api/v1/screens/library?tab=reading`

Bearer required. Other tabs: all, saved, finished, downloads, notes. Optional page, pageSize, search. For downloads, also pass deviceKey=<device-id>. The book card is nested under items[].card; progress and latestNote can be null, currentChapter can be null, and counts may be zero. progressPercent is already 0–100.

HTTP 200 response example:

```json
{
  "page": 1,
  "pageSize": 20,
  "total": 6,
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

### Notes — latest note grouped by book

`GET /api/v1/screens/library?tab=notes`

Bearer required. Same item shape as the Reading Now example. latestNote provides title, noteText, pageNo and updatedAt; noteCount is the count for that book. This endpoint pages books, not individual notes. Fetch /api/v1/me/annotations/notes?bookId=<id> to list the notes for one book.

HTTP 200 response example:

```json
{
  "page": 1,
  "pageSize": 20,
  "total": 5,
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

### Collections — collection cards

`GET /api/v1/screens/collections?tab=all`

Bearer required. Other tabs: owned, shared, favorites. Optional search, page, pageSize. Use collection.name / coverImageUrl, actual bookCount, and isFavorite. A null cover should render the app placeholder.

HTTP 200 response example:

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

### Home — banners, four categories and popular books

`GET /api/v1/home`

Public. Resolve relative imageUrl values against the base URL. The banner is SVG; the four exported demo book covers are PNG. The complete original Home workflow is also described in HOME_API.md.

HTTP 200 response example:

```json
{
  "banners": [
    {
      "bannerId": "home-reading",
      "title": "Good Books.\nGreat moments.",
      "subtitle": "Buy, rent and read your favorite books anytime, anywhere.",
      "imageUrl": "/images/home/reading-banner.svg",
      "buttonText": "Explore Now",
      "action": "OPEN_EXPLORE"
    }
  ],
  "categories": [
    {
      "categoryId": 1,
      "name": "Fiction",
      "slug": "fiction",
      "iconKey": "book-open"
    }
  ],
  "popularBooks": [
    {
      "bookId": 1,
      "title": "Atomic Habits",
      "coverImageUrl": "https://booknest-api-3jl3.onrender.com/images/figma/atomic-habits.png",
      "authors": [
        {
          "authorId": 1,
          "authorName": "James Clear"
        }
      ],
      "purchasePrice": 1200.0,
      "rentalPrice": 300.0,
      "currencyCode": "JPY",
      "rentalDays": 30,
      "pageCount": 100,
      "languageCode": "en",
      "fileFormat": "PDF",
      "premiumEligible": false,
      "isDemo": true
    }
  ]
}
```

### Library/Profile — counters

`GET /api/v1/me/library/counts`

Bearer required. Add deviceKey=<device-id> for that device's download count; without it downloads is zero. Counts are independent of the current screen search or page.

HTTP 200 response example:

```json
{
  "totalBooks": 31,
  "readingNow": 6,
  "finished": 12,
  "saved": 8,
  "downloads": 0
}
```

### Reading Time — chart and totals

`GET /api/v1/me/reading/statistics?period=week`

Bearer required. period=week|month|year|all. Times are seconds, not minutes. start/end and bucket labels use the returned timezone. percentageChange can be null when there is no prior activity. Insert zero chart buckets for missing labels.

HTTP 200 response example:

```json
{
  "period": "week",
  "timezone": "Asia/Tokyo",
  "start": "2026-10-12",
  "end": "2026-10-17",
  "totalSeconds": 6000,
  "daysRead": 5,
  "averageSeconds": 1000,
  "pagesRead": 20,
  "longestStreak": 5,
  "percentageChange": 400,
  "breakdown": [
    {
      "label": "2026-10-12",
      "seconds": 1200,
      "pages": 4
    },
    {
      "label": "2026-10-13",
      "seconds": 1200,
      "pages": 4
    },
    {
      "label": "2026-10-14",
      "seconds": 1200,
      "pages": 4
    },
    {
      "label": "2026-10-15",
      "seconds": 1200,
      "pages": 4
    },
    {
      "label": "2026-10-16",
      "seconds": 1200,
      "pages": 4
    }
  ]
}
```

### Reader — bookmarks

`GET /api/v1/me/annotations/bookmarks?bookId=1`

Bearer required. A plain JSON array, not a page/items envelope. Supports page and pageSize. Use pageNo/chapterId or locationToken to navigate; nullable location fields depend on the content format.

HTTP 200 response example:

```json
[
  {
    "bookmarkId": 1,
    "userId": 1,
    "bookId": 1,
    "chapterId": 55,
    "pageNo": 1,
    "locationToken": null,
    "label": "Figma demo bookmark",
    "createdAt": "2026-10-17T17:06:57.543296+09:00",
    "updatedAt": "2026-10-17T17:06:57.543296+09:00"
  }
]
```

### Reader — highlights

`GET /api/v1/me/annotations/highlights?bookId=1`

Bearer required. A plain JSON array. startAnchor/endAnchor identify the selected range; colorCode is YELLOW, GREEN, BLUE or PINK. Demo anchors and selected text are synthetic examples.

HTTP 200 response example:

```json
[
  {
    "highlightId": 1,
    "userId": 1,
    "bookId": 1,
    "chapterId": 55,
    "pageNo": 1,
    "locationToken": null,
    "highlightedText": "Original demo text: make a little time to read each day.",
    "startAnchor": "demo:p1:0",
    "endAnchor": "demo:p1:54",
    "colorCode": "YELLOW",
    "createdAt": "2026-10-17T17:06:57.543296+09:00",
    "updatedAt": "2026-10-17T17:06:57.543296+09:00"
  }
]
```

### Explore — Recently Viewed

`GET /api/v1/me/recently-viewed`

Bearer required. A plain JSON array of book/viewedAt objects. This existing endpoint returns raw books without the enriched catalog card's authors/categories. Record a view via POST /api/v1/books/{id}/views with a unique eventKey.

HTTP 200 response example:

```json
[
  {
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
    "viewedAt": "2026-10-17T17:06:57.543296+09:00"
  }
]
```

### Empty results and client types

Paginated screen endpoints keep the envelope when no records match:

```json
{
  "page": 1,
  "pageSize": 20,
  "total": 0,
  "items": []
}
```

Plain-list endpoints return `[]` when empty. Library fields `progress`, `currentChapter` and `latestNote` may be null; `rating`, `coverImageUrl`, `previewUrl`, `fileSizeBytes`, and dates can also be null as shown. Use Int64/Long for IDs, decimal-compatible values for prices, and timezone-aware timestamps for date/time fields. Currency formatting must use currencyCode. Do not confuse the catalog's `items[].book` with the library's `items[].card.book`.

Examples are successful response shapes; check HTTP status before parsing. For non-2xx responses, handle the status and the API's Problem Details body when present. All examples are documentation only and grant no ebook or payment entitlement.

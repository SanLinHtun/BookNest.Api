# BookNest Figma → API handoff

Design: https://www.figma.com/design/QyCgu0IiouHSf7QKYM9hLY/BookNest

Reviewed 2026-10-17. Metadata covers 29 screen frames. Detailed design context was obtained for Home Bottom, Explore Top/Bottom, BookDetail, Library, My Books Top/Bottom, Notes, More, Edit Profile, Profile Bottom and reading-time Month/Year/All Time. The Figma Starter MCP quota blocked further detailed fetches; remaining frames were assessed from their text/structure, not a complete visual review.

Base URL: `https://booknest-api-3jl3.onrender.com`

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

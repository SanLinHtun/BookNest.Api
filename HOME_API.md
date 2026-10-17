# Home screen API

Base URL: `https://booknest-api-3jl3.onrender.com`

## Initial screen: one request

`GET /api/v1/home` is public and requires no token. It returns:

| Screenshot section | Response field | Client behavior |
|---|---|---|
| Hero carousel | `banners[]` | Render title, subtitle, image and button. Render one indicator per returned banner. The starter response has one banner. |
| Category row | `categories[]` | Render name and iconKey. `All` and `More` are client controls, not database categories. |
| Popular Books | `popularBooks[]` | Render title, coverImageUrl, authors[].authorName, purchasePrice, rentalPrice and currencyCode. |
| Explore Now | `banners[].action` | `OPEN_EXPLORE` navigates to the catalog screen. |

Resolve relative image URLs against the base URL. Sample artwork uses SVG, so the mobile image loader must support SVG. The artwork is a demo placeholder, not the original book cover from the screenshot. Prices are numeric; format with `currencyCode` rather than concatenating a hardcoded currency symbol. `rentalDays` is the rental duration. Book IDs are Int64.

Popular books are limited to 20 published entries, ordered by recorded detail views, then book ID. No fake view history has been inserted. Banner copy is currently defined in the API source. Categories, authors, book metadata and prices come from Neon.

## Existing APIs used after an action

| Action | Request | Authentication |
|---|---|---|
| Category tap / See all / Explore | `GET /api/v1/books?categoryId=ID&page=1&pageSize=20` | Public. Omit categoryId for All. |
| Search by title or author | `GET /api/v1/books?search=James%20Clear&page=1&pageSize=20` | Public. Current substring matching is case-sensitive. URL-encode the search term. |
| Open book | `GET /api/v1/books/ID` | Public |
| Buy | `POST /api/v1/me/orders` with `{"items":[{"bookId":ID,"accessType":"PURCHASE"}]}` | Bearer token |
| Rent | Same order endpoint with `"accessType":"RENTAL"` | Bearer token |

This means **one call on initial load** and **four distinct endpoint paths for the complete home workflow**: `/home`, `/books`, `/books/{id}`, and `/me/orders`. Login/register are separate authentication flows. Bottom navigation, notification icon, category control icons and search-filter button are UI controls. There is no notification API or advanced filter API implemented by this change.

Orders are pending until trusted payment confirmation. A checkout/receipt provider and usable private ebook files are not configured. The sample entries exist to test the catalog UI, not actual paid reading. Do not treat an order response as a successful purchase.

## Neon demo data

| Book | Author | Category | Buy (JPY) | Rent (JPY) |
|---|---|---|---:|---:|
| Atomic Habits | James Clear | Self-help | 1200 | 300 |
| The Psychology of Money | Morgan Housel | Business | 1000 | 250 |
| Rich Dad Poor Dad | Robert T. Kiyosaki | Business | 900 | 200 |
| Ikigai | Hector Garcia | Self-help | 800 | 200 |

These are demo prices, a sample 100-page length, and a 30-day rental duration. The API marks these cards `isDemo: true`. No real ebook content, purchases, entitlements, accounts, reviews or view events were seeded. Fiction and Education are available categories with no linked demo books. Existing records are not overwritten by the seed operation.

## Response shape

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
    { "categoryId": 4, "name": "Self-help", "slug": "self-help", "iconKey": "sprout" }
  ],
  "popularBooks": [
    {
      "bookId": 1,
      "title": "Atomic Habits",
      "coverImageUrl": "https://booknest-api-3jl3.onrender.com/images/home/atomic-habits.svg",
      "authors": [{ "authorId": 1, "authorName": "James Clear" }],
      "purchasePrice": 1200,
      "rentalPrice": 300,
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

The excerpt above is illustrative. Use returned IDs and the full live response; do not hardcode the example IDs. A saved full response is provided with the handoff artifacts.

On the free server, allow for a slow cold start. Fetch the home payload once per screen refresh rather than fetching each book's author separately. Handle an empty array, non-200 Problem Details response and retry/loading states.

# Company Directory

A small full-stack exercise: an ASP.NET Core Web API that stores companies in memory, and an
Angular page that submits, lists, searches, edits and deletes them.

A company is a **name** and a **website URL**. The API only accepts a company whose name
plausibly belongs to its website (for example "Acme Tools Ltd" and `acme-tools.co.uk`).

## Run it

Requirements: .NET SDK 10 and a Node.js version supported by Angular 22 (built and tested with Node 24).

**Backend**

```bash
cd src/Companies.Api
dotnet run
```

The API listens on `http://localhost:5000` and `https://localhost:7000`.
The OpenAPI document is at `http://localhost:5000/openapi/v1.json`.

**Frontend** (second terminal)

```bash
cd client
npm install
npm start
```

Open `http://localhost:4200`. The dev server proxies `/api` to the backend on port 5000.

**Tests**

```bash
dotnet test
```

```bash
cd client
npm test -- --watch=false
```

There are 118 backend tests (97 unit, 21 through the real HTTP pipeline) and 19 frontend tests.

## API

| Method | Route | Success | Errors |
|---|---|---|---|
| `POST` | `/api/companies` | `201` + `Location` + the company | `400` invalid, `409` website already registered |
| `GET` | `/api/companies` | `200` + a page of companies | `400` invalid paging |
| `GET` | `/api/companies/{id}` | `200` + the company | `404` unknown id, `400` malformed id |
| `PUT` | `/api/companies/{id}` | `200` + the company | `400`, `404`, `409` |
| `DELETE` | `/api/companies/{id}` | `204` | `404` |

Query parameters of `GET /api/companies` (all optional, and they combine):

| Parameter | Meaning |
|---|---|
| `name` | Filter: the name contains this text, ignoring case. |
| `domain` | Filter: the website is on this domain. Accepts `acme.com`, `www.acme.com` or a full URL, and includes subdomains. |
| `q` | Free-text search over name and domain. Results are ordered by relevance and carry a `relevanceScore`. |
| `page`, `pageSize` | Paging. Defaults 1 and 50; `pageSize` is at most 100. |

A search with no match is `200` with an empty `items` array, not `404`.

Request and response:

```jsonc
// POST /api/companies
{ "name": "Acme Tools Ltd", "websiteUrl": "https://www.acme-tools.co.uk" }

// 201 Created
{
  "id": "6f1c…",
  "name": "Acme Tools Ltd",
  "websiteUrl": "https://www.acme-tools.co.uk/",
  "domain": "acme-tools.co.uk",
  "createdAt": "2026-01-15T09:30:00+00:00",
  "updatedAt": null
}
```

Every error is an RFC 7807 problem document. `detail` is a single readable sentence, and
validation errors add `errors`, keyed by JSON field name so a form can place each message:

```jsonc
// 400 Bad Request
{
  "title": "The request is invalid.",
  "status": 400,
  "detail": "Company name 'Globex' does not appear to match the website 'acme.com'.",
  "errors": { "name": ["Company name 'Globex' does not appear to match the website 'acme.com'."] }
}
```

[`src/Companies.Api/Companies.Api.http`](src/Companies.Api/Companies.Api.http) has a ready-made
request for every case.

## Validation

| Field | Rule |
|---|---|
| Name | Required; 3 to 100 characters after trimming. |
| Website URL | Absolute `http` or `https` URL whose host is a domain name (no IP addresses, no `localhost`). |
| Both | The name must be relevant to the website (below). |
| Website | Unique: a second company on the same host is a `409`. |

### Relevance: does the name belong to the website?

Strategy: **domain matching**. The name is compared with the domain label, the part of the
host that a company chooses (`acme-tools` in `www.acme-tools.co.uk`).

Both sides are normalised first: lower case, accents and punctuation removed, a leading "the"
and trailing legal suffixes (Inc, LLC, Ltd, GmbH, S.A. …) dropped. Then five signals each
produce a score from 0 to 1, and the highest wins:

| Signal | Example | Score |
|---|---|---|
| Exact | "Bank of America Corp." and `bankofamerica.com` | 1.0 |
| Leading words | "Microsoft Azure Cloud Services" and `microsoft.com` | 0.9 |
| Acronym | "International Business Machines" and `ibm.com` | 0.85 |
| Word coverage | "Verus" and `verusllc.com` | average of: share of the name found in the domain, share of the domain explained by the name |
| Similar spelling | "Tumbler" and `tumblr.com` | Levenshtein similarity (names of 5+ letters only) |

The company is accepted when the score reaches the threshold, **0.7** by default
(`Relevance:Threshold` in `appsettings.json`).

Why this strategy:

- **Explainable.** Every decision names the signal behind it, and the rejection message tells
  the user which name and domain did not match.
- **Deterministic and offline.** No network call, so it is fast, testable, and cannot be used to
  make the server fetch arbitrary URLs.
- **Replaceable.** It sits behind `ICompanyRelevanceStrategy`. A content-inspection strategy
  (fetch the home page, compare its title) could be added as a fallback without touching callers.

Known limits, accepted for this exercise:

- A brand whose domain shares no text with its name is rejected (Alphabet and `abc.xyz`).
- A name that happens to contain most of a domain can pass ("Apple" and `pineapple.com`).
- Two-level public suffixes (`co.uk`, `com.au` …) come from a short built-in list. Production
  code would use the full Public Suffix List.

### Search ranking

`q` scores each company by the best tier it reaches, so the ordering is easy to explain:
exact name (1.0), exact domain (0.95), name starts with the query (0.9), a word in the name
starts with it (0.8), domain starts with it (0.75), name contains it (0.6), domain contains it
(0.55), near-miss spelling (below 0.5). Ties are ordered by name.

## Structure

```
src/
  Companies.Domain          Company, WebsiteAddress. No dependencies.
  Companies.Application     Use cases: CompanyService, validation, relevance, search ranking,
                            DTOs, and the ICompanyRepository port.
  Companies.Infrastructure  InMemoryCompanyRepository.
  Companies.Api             Controller, problem-details mapping, exception handler, composition.
tests/
  Companies.UnitTests       Domain, relevance, validation, ranking, service, repository.
  Companies.ApiTests        Status codes and contracts through WebApplicationFactory.
client/                     Angular app.
```

Dependencies point inwards: Api → Application → Domain, and Infrastructure → Application.

Design decisions:

- **Thin controller.** Each action calls one service method and maps the result. Rules live in
  the application layer.
- **Result instead of exceptions for expected failures.** The service returns `Result<T>` with an
  error kind (validation, not found, conflict) that knows nothing about HTTP.
  `ProblemResults` is the one place that turns kinds into status codes. Exceptions are reserved
  for bugs, and a global handler turns those into a `500` problem document.
- **DTOs at the edge.** Requests and responses are records. `Company` and the storage dictionary
  never leave the application.
- **One source of truth for validation.** `CompanyValidator` holds every rule. Data annotations on
  the request records are only a cheap first guard for missing or oversized input.
- **Storage behind a port.** `ICompanyRepository` is asynchronous and speaks domain types.
  The in-memory version uses a lock so the uniqueness check and the write are atomic, as a
  unique index would be. `Company` is immutable, so handing instances out is safe.
- **Time is injected** (`TimeProvider`), so timestamps are testable.
- **Logging** uses source-generated `LoggerMessage` methods: creations, updates and deletions at
  Information, rejections at Warning, unhandled errors at Error.

## Extending it

- **Database.** Implement `ICompanyRepository` (for example with EF Core) and change one line in
  `Companies.Infrastructure/DependencyInjection.cs`. The name and host filters already arrive
  as a `CompanyFilter`, ready to become a `WHERE` clause. Ranking and paging currently run in
  memory in the service; with a large table they would move into the query (full-text search).
- **Sorting.** Add a `sort` parameter to `CompanyQuery` and apply it where the service orders.
- **Another relevance technique.** Implement `ICompanyRelevanceStrategy`, or compose several.
- **Partial updates.** Add `PATCH` beside `PUT`; validation and the repository are reusable.

## Frontend

One page built from three standalone components and a typed API client:

- `CompanyForm` adds or edits a company. It checks the basic rules in the browser, then shows the
  API's own message under the field it concerns (or as an alert for a conflict or an unreachable server).
- `CompanyList` is presentational: a table, with empty and loading states.
- `App` owns the page state in signals and reloads the list through one stream, so a slow
  response can never overwrite a newer one.

State management and styling are intentionally minimal: signals, no store, one small stylesheet.

## Notes

- Data lives in memory and is lost when the API stops.
- CORS allows `http://localhost:4200` (`Cors:AllowedOrigins`), so the client also works without the proxy.
- Built with AI assistance (Claude Code).

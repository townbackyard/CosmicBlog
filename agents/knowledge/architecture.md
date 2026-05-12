# Architecture

## Repository layout

```
CosmicBlog/
├── BlogWebApp/               # ASP.NET Core MVC web frontend (.NET 10)
│   ├── Controllers/
│   ├── Models/               # Cosmos DB document shapes
│   ├── ViewModels/           # MVC view models
│   ├── Services/             # Cosmos data access, image storage manager
│   ├── Views/                # Razor views (Bootstrap 5, no jQuery)
│   ├── CosmosDbScripts/      # JS sprocs and triggers deployed at startup
│   ├── wwwroot/
│   ├── AppSettings.cs        # Strongly-typed settings
│   ├── Program.cs            # Composition root + Cosmos bootstrap
│   └── BlogWebApp.csproj
├── BlogFunctionApp/          # Azure Functions, change-feed worker (.NET 10, isolated worker, v4)
│   └── BlogFunctionApp.csproj
├── CosmicBlog.slnx           # Solution (new XML format)
├── AGENTS.md                 # AI agent index (this directory tree's purpose)
├── CLAUDE.md                 # Claude Code-specific config (imports AGENTS.md)
├── README.md                 # Human-facing project description
└── LICENSE                   # MIT
```

## Two projects, one responsibility split

- **`BlogWebApp`** — the public site + the admin surface. Reads/writes posts in the `Posts` Cosmos container. Reads the homepage activity stream (post-Phase-1c: directly from `Posts`; current code: from `Feed` container). Serves blob-stored images via `BlogPostImageController` at `/img/post/<blob path>`.

- **`BlogFunctionApp`** — Azure Functions change-feed worker. Consumes the Cosmos change feed on `Posts` and projects/transforms documents into the `Feed` container, plus maintains a per-user post index in `Users`. The split exists because the upstream `AzureCosmosDbBlogExample` teaches the change-feed partition-strategy pattern; CosmicBlog inherits it.

Run both for full-stack local dev. The web app works in isolation if you don't exercise the homepage feed.

## Cosmos containers (current state)

All in the `cosmicblog` database (or whatever `CosmosDbBlog:DatabaseName` is set to in `appsettings.json` — defaults to `MyBlog` for emulator parity with the upstream sample).

| Container | Partition key | Contents |
|---|---|---|
| `Users` | `/userId` | User documents. Has a unique-key constraint on `/username` (set up in `Program.cs` at startup). |
| `Posts` | `/postId` | Blog posts. Today every doc has `type="post"` (hardcoded getter); Phase 1c makes `type` settable to support `note` and `now` values. |
| `Feed` | `/type` | Homepage feed projection produced by the change-feed Functions. |

**Phase 1c (Plan 3) adds:**
- `Subscribers` container, partition key `/id` (id = lowercased email). For newsletter signups.
- Posts container documents grow to include `slug`, `linkUrl` (notes), `dateUpdated`.

## Cosmos bootstrap at startup

`Program.cs` → `InitializeCosmosBlogClientInstanceAsync` runs synchronously at app startup:

1. Creates the database if missing.
2. Creates `Users`, `Posts`, `Feed` containers if missing (idempotent — Phase 1c adds `Subscribers` here).
3. Upserts the sprocs from `CosmosDbScripts/sprocs/*.js` into `Posts`.
4. Upserts the trigger from `CosmosDbScripts/triggers/truncateFeed.js` onto `Feed`.
5. On a fresh database (Posts container didn't pre-exist), seeds a "Hello World!" post so the homepage isn't empty.

This is deliberate teaching-example-derived behavior — keeps the run-locally story to "clone, run emulator, F5." Don't replace with EF Core migrations / Terraform / Bicep without a strong reason.

## Change-feed flow (BlogFunctionApp)

The Functions worker subscribes to the change feed on `Posts` and projects/transforms documents into `Feed` for cross-partition queryability of the homepage. The `Feed` container's `truncateFeed.js` post-trigger keeps it bounded.

When the post schema changes (Phase 1c — adding `note` and `now` types, plus `slug`/`linkUrl`/`dateUpdated` fields), verify the change-feed Functions don't silently drop the new types. If the homepage feed reads directly from `Posts` (as Plan 3 plans), the Feed container becomes legacy for that path and the Function projections don't need to be touched.

## Build and run

| Command | Effect |
|---|---|
| `dotnet build CosmicBlog.slnx` | Builds both projects. Warnings carry over from the upstream modernization; treat the count as the baseline. |
| `dotnet run --project BlogWebApp` | Web app at `https://localhost:5001`. Needs Cosmos Emulator (8081) + Azurite (10000). |
| `func start --project BlogFunctionApp` | Functions worker. Needs Azurite and Cosmos Emulator. |

Pre-Phase-1c, login is passwordless (`/Register` then `/Login` with a username). Username matching `AdminUsername` in `appsettings.json` (default: `jsmith`) grants Admin role. Phase 1c replaces this with ASP.NET Core Identity + email/password.

## Hosting target

Azure App Service Linux (new instance per spec), custom domain `jeffwidmer.me`, App Service managed cert. Cosmos lives in Jeff's existing Azure Cosmos DB account (database `cosmicblog`). Azure Blob Storage for images. Azure Communication Services Email for Contact (Phase 1c) and newsletter sending (Phase 2).

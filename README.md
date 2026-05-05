# CosmicBlog

A learn-in-public blog engine on **.NET 10 + Azure Cosmos DB**. CosmicBlog runs [jeffwidmer.me](https://jeffwidmer.me) and is open-sourced under the MIT license so you can self-host your own.

CosmicBlog began as a fork of [jwidmer/AzureCosmosDbBlogExample](https://github.com/jwidmer/AzureCosmosDbBlogExample) (the sample for the Microsoft docs article [How to model and partition data on Azure Cosmos DB using a real-world example](https://docs.microsoft.com/en-us/azure/cosmos-db/how-to-model-partition-example)). The upstream remains a clean teaching example; CosmicBlog is the production engine that builds on it with additional features and continues to evolve.

## Features beyond the upstream sample

- **Configurable blog name** via `appsettings.json` — drop in your own brand without forking code.
- **Base64 image extraction on save** — when you paste a screenshot into the editor, CosmicBlog extracts it from the post body, uploads it to Azure Blob Storage, and rewrites the body with a URL. Keeps Cosmos documents small.
- **Modern stack throughout** — .NET 10, Azure Functions v4 isolated worker, Azure Cosmos DB SDK 3.x, Bootstrap 5, TinyMCE 7, no jQuery.

## Architecture

Two projects in one solution:

- **`BlogWebApp`** — ASP.NET Core MVC web frontend. Reads/writes posts from the `Posts` container; reads recent posts from the `Feed` container for the homepage feed.
- **`BlogFunctionApp`** — Azure Functions (isolated worker) that consume the Cosmos DB change feed on `Posts` and project documents into the `Feed` container, plus maintain a per-user post index in `Users`. See the upstream Microsoft docs article for the partition-strategy reasoning.

## Prerequisites

1. **Visual Studio 2026 (or current latest)** with the .NET 10 SDK — [download](https://visualstudio.microsoft.com/downloads/).
2. **.NET 10 SDK** — [download](https://dotnet.microsoft.com/download).
3. **Azure Functions Core Tools v4** — [install instructions](https://learn.microsoft.com/en-us/azure/azure-functions/functions-run-local).
4. **Azure Cosmos DB Emulator** (Linux Docker container recommended) or a live Cosmos DB account.
5. **Azurite** (Azure Storage Emulator) for the Functions runtime and image-upload pipeline.

## Running locally

1. Clone this repository.
2. Start the Cosmos DB Emulator and Azurite.
3. Open the solution in Visual Studio (or run each project with `dotnet run` / `func start` from the command line).
4. Set both `BlogWebApp` and `BlogFunctionApp` as startup projects, then F5.
5. Register a user (no password needed in local dev — see "Authentication notes" below).

## Configuration

Settings of interest in `BlogWebApp/appsettings.json`:

- **`BlogName`** — the title shown in the page `<title>`, navbar brand, footer, and homepage h1.
- **`AdminUsername`** — the username that gets the Admin role on login (default `jsmith`).
- **`CosmosDbBlog.Account` / `Key` / `DatabaseName`** — Cosmos endpoint configuration.
- **`StorageBlobConnectionString`** — Azure Blob connection string used by the image-upload pipeline. Defaults to `UseDevelopmentStorage=true` (Azurite). Override at deploy time via environment variables or Key Vault — never commit a real key.

## Authentication notes

The current implementation uses a passwordless registration shortcut for local development convenience. **Replace this with real authentication before deploying to production.** Suggested: ASP.NET Core Identity with email/password, or Microsoft Entra ID with personal Microsoft account sign-in.

To log in as the blog admin (the role that can create new posts), register the username matching `AdminUsername` in `appsettings.json` (default: `jsmith`).

## License

MIT. See `LICENSE`.

## Credits

- [jwidmer/AzureCosmosDbBlogExample](https://github.com/jwidmer/AzureCosmosDbBlogExample) — the upstream sample CosmicBlog forks from. The change-feed flow and partition strategy come from the Microsoft docs article it accompanies.

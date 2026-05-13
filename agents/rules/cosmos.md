# Cosmos DB conventions

## SDK

- **Microsoft.Azure.Cosmos 3.46** (or current latest 3.x). SDK 4.x does not exist.
- Newtonsoft-backed serialization is the project default. See `agents/rules/csharp.md` → Newtonsoft.Json section.
- The `CosmosClient` is built via `CosmosClientBuilder` in `Program.cs` with `CosmosPropertyNamingPolicy.CamelCase`. Don't change the naming policy — it pairs with the `[JsonProperty]` attributes on models.

## Partition keys (current)

| Container | Partition key | Why |
|---|---|---|
| `Users` | `/userId` | One-doc-per-partition; unique-key constraint on `/username`. |
| `Posts` | `/postId` | One-doc-per-partition. Cross-partition queries for activity feed are acceptable at v1 scale (≤ a few hundred docs). |
| `Feed` | `/type` | Cross-partition queryability for the legacy homepage projection (post-Phase-1c, the homepage feed bypasses this container). |
| `Subscribers` (Phase 1c) | `/id` | id = lowercased email; one-doc-per-partition. |

**Don't change a container's partition key** without a migration plan — Cosmos doesn't let you alter partition keys on an existing container; you'd have to create a sibling container, copy data, swap references.

## Query patterns

### Point reads (preferred when you know id + partition)

```csharp
var resp = await container.ReadItemAsync<BlogPost>(
    postId, new PartitionKey(postId));
return resp.Resource;
```

Wrap in a try/catch on `CosmosException` for `NotFound`:

```csharp
try
{
    var resp = await container.ReadItemAsync<BlogPost>(id, new PartitionKey(id));
    return resp.Resource;
}
catch (CosmosException ex) when (ex.StatusCode == HttpStatusCode.NotFound)
{
    return null;
}
```

### Query iterators (for filters across partitions)

```csharp
var query = new QueryDefinition(
    $"SELECT TOP {count} * FROM p WHERE p.type = @type ORDER BY p.dateCreated DESC")
    .WithParameter("@type", type);

// Note: Cosmos DB SQL's TOP operator requires a literal integer, NOT
// a parameter — `SELECT TOP @count` is invalid and will fail at
// execution time. Interpolating an `int` value is safe from injection
// because the type doesn't accept arbitrary strings.

var results = new List<BlogPost>();
var iterator = container.GetItemQueryIterator<BlogPost>(query);
while (iterator.HasMoreResults)
{
    var resp = await iterator.ReadNextAsync();
    results.AddRange(resp);
}
return results;
```

**Always parameterize.** Never concatenate user input into the SQL string — Cosmos SQL is SQL injection-able. The `QueryDefinition` + `WithParameter` pattern is the only acceptable form for string/user-supplied values. The `TOP N` literal is the one exception: it must be an interpolated `int`, not a `@param`.

## Upserts and writes

`UpsertItemAsync` is preferred over `CreateItemAsync` + `ReplaceItemAsync` for the common case (model already has a stable id):

```csharp
await container.UpsertItemAsync(post, new PartitionKey(post.PostId));
```

Pass the partition key explicitly even when the SDK could derive it — defensive, easier to read.

## Sprocs and triggers

Sprocs live in `BlogWebApp/CosmosDbScripts/sprocs/` and the trigger in `BlogWebApp/CosmosDbScripts/triggers/`. They're upserted at app startup (`Program.cs` → `UpsertStoredProcedureAsync` / `UpsertTriggerAsync`).

- Edit the `.js` files directly; `Program.cs` reads from disk on startup.
- Don't add new sprocs without a clear reason — sprocs are operationally heavier than client-side queries and v1 already has more sprocs (comment, like, updateUsernames) than v1 surface area justifies.
- The `truncateFeed.js` post-trigger on `Feed` keeps that container bounded.

## Error handling

- `CosmosException` is the catch-all. Use `when (ex.StatusCode == HttpStatusCode.X)` filters; don't catch-rethrow.
- `HttpStatusCode.NotFound` — convert to a `null` return at the data layer; let the controller decide whether to 404.
- `HttpStatusCode.Conflict` (409) — usually means a unique-key violation. Surface to the user as a friendly validation message.
- Don't swallow exceptions silently. The data layer rethrows what it doesn't translate; the controller logs and decides.

## Local dev: Cosmos Emulator

- HTTPS endpoint: `https://localhost:8081`.
- Well-known emulator key: `C2y6yDjf5/R+ob0N8A7Cgv30VRDJIWEHLM+4QDU5DE2nQ9nDuVTqobD4b8mGGyPMbIZnqyMsEcaGQy67XIw/Jw==` — already in `appsettings.json` as the default. Safe to commit (it's a Microsoft-published emulator-only key).
- For production, the connection comes from env vars / App Service configuration / Key Vault — never from `appsettings.json`.

## What to NOT add

- **Azure Cognitive Search / Azure AI Search.** Spec defers full-text search to "Cosmos SQL queries against post body" for v1. Don't introduce a search service.
- **Cosmos DB SDK 4.x preview.** Doesn't exist yet at the time of writing.
- **EF Core for Cosmos.** The SDK is fluent and direct; EF Core adds an abstraction layer the project doesn't need.
- **A second database (SQL Server, PostgreSQL, SQLite).** The engine is single-data-store on purpose. Identity gets a custom Cosmos-backed `IUserStore` rather than a SQL sidecar.

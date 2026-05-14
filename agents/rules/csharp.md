# C# conventions

## Target framework

- Both projects target **`.NET 10`** (`<TargetFramework>net10.0</TargetFramework>`).
- `BlogFunctionApp` is on **Azure Functions Worker SDK 2.x** (v4 isolated worker). Don't downgrade to the in-process model.
- `<Nullable>enable</Nullable>` is on in both projects. Honor nullability.

## Newtonsoft.Json — deliberate choice

Cosmos DB SDK 3.46's LINQ provider transitively requires `Newtonsoft.Json` 13.x. Even a custom `CosmosSerializer` based on `System.Text.Json` would leave Newtonsoft in the dependency tree (hidden but present). The cost-benefit of a partial migration was evaluated; the conclusion is to keep Newtonsoft as an **explicit `PackageReference`** in both projects and use `[JsonProperty(PropertyName = "…")]` on Cosmos models.

When writing Cosmos models or anything serialized into Cosmos:
- Use `Newtonsoft.Json.JsonProperty` attributes — not `System.Text.Json.JsonPropertyName`.
- Camel-case the JSON property name; the C# property name stays Pascal-case.
- Don't mix the two serializers on the same type.

`System.Text.Json` is fine for **non-Cosmos paths**: JSON Feed output, ad-hoc API JSON, anything that doesn't round-trip through Cosmos. Don't introduce STJ migration PRs on Cosmos models without a separate spec decision.

## Nullable patterns

Patterns established during the Phase 0 modernization:

- Default reference-type properties to `string.Empty` (not `null`) for the "intentionally empty string" case:
  ```csharp
  public string Title { get; set; } = string.Empty;
  ```
- Use `string?` when null is a meaningful state (e.g., `LinkUrl` on a note that doesn't link to anything).
- For "must-not-be-null" invariants in controllers (claims, IDs), prefer throw-on-null over silent fallback:
  ```csharp
  AuthorId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value
      ?? throw new InvalidOperationException("Authenticated user has no NameIdentifier claim."),
  ```
  This pattern is in place throughout `BlogPostController` and should be matched in new controllers.
- Don't suppress `CS86xx` warnings with `!` (null-forgiving operator) unless the alternative is clearly worse. If you reach for `!`, justify it in a comment or surface it as an Open Question.

## Async

- All Cosmos and ACS calls are async. Don't synchronously block them with `.Result` or `.Wait()`.
- Controller actions returning `IActionResult` should be `async Task<IActionResult>` when they touch the data layer.

## DI lifetimes

- `BlogCosmosDbService` and `ImageStorageManager` are singletons. Match this pattern for new long-lived services.
- Identity stores (Phase 1c) are also singletons in this app — `UserManager<T>` itself is scoped (default), but the underlying `IUserStore<T>` registration is singleton because Cosmos clients are thread-safe.

## File layout

- One public type per file, **except for service interfaces** — see Interface Location below.
- Filename matches the public type name (use the implementation class name when interface and implementation share a file).
- Namespaces match the folder tree (`Controllers/` → `BlogWebApp.Controllers`, etc.).
- ViewModels in `BlogWebApp.ViewModels`. Models in `BlogWebApp.Models`. Services + interfaces in `BlogWebApp.Services`.

## Interface Location

Interfaces for services should be defined at the top of the **same file** as the implementation class, not in separate files:

```csharp
namespace BlogWebApp.Services;

public interface IMyService
{
    Task DoWorkAsync();
}

public class MyService : IMyService
{
    public async Task DoWorkAsync() { /* ... */ }
}
```

Filename is the implementation class name (`MyService.cs`), not the interface name. Rationale: keeps the contract and the implementation reviewable in one buffer; reduces file count and folder noise.

## Things to avoid

- Don't add a SQL Server / EF Core / SQLite dependency without explicit scope confirmation — the engine is intentionally single-data-store (Cosmos only). ASP.NET Core Identity in Phase 1c gets a custom Cosmos-backed `IUserStore`, not EF Core.
- Don't introduce a new logging framework. The defaults from `Microsoft.Extensions.Logging` are fine.
- Don't introduce a new HTTP client. `HttpClientFactory` is already wired by default when needed.
- Don't reintroduce jQuery, Knockout, AngularJS, or RequireJS. The site is server-rendered Razor + Bootstrap 5; small islands of vanilla JS only.

# CosmicBlog — AI Agent Guide

CosmicBlog is a learn-in-public blog engine on **.NET 10 + Azure Cosmos DB**. It runs `jeffwidmer.me` and is open-sourced (MIT) for self-hosters. The engine is the modernized continuation of `jwidmer/AzureCosmosDbBlogExample` (the sample for the Microsoft docs article on Cosmos partition strategy) plus the features that make it a real blog rather than a teaching example.

This file is the orientation for AI coding agents. Detailed knowledge lives in `agents/`.

## Quick Reference

| Command | Description |
|---------|-------------|
| `dotnet build CosmicBlog.slnx` | Build both projects |
| `dotnet run --project BlogWebApp` | Run the web app (https://localhost:5001) |
| `func start --project BlogFunctionApp` | Run the change-feed Functions worker |
| `dotnet sln CosmicBlog.slnx list` | List projects in the solution |

**Local infra required:** Azure Cosmos DB Emulator (Linux Docker container, HTTPS 8081) + Azurite (Azure Storage Emulator, HTTP 10000) — both for everything except trivial UI work.

## Project Knowledge

Detailed docs in `agents/`:

### Knowledge
- [architecture.md](agents/knowledge/architecture.md) — Repo layout, two .NET projects (BlogWebApp + BlogFunctionApp), Cosmos containers and partition keys, change-feed flow.
- [content-model.md](agents/knowledge/content-model.md) — `BlogPost` shape, planned multi-type discriminator (post / note / now), slug rules, Now singleton.

### Rules
- [csharp.md](agents/rules/csharp.md) — .NET 10 conventions, nullable patterns, Newtonsoft.Json (deliberate, not pending migration).
- [cosmos.md](agents/rules/cosmos.md) — Cosmos SDK 3.x patterns: partition keys, queries, sprocs/triggers, error handling.

## Stack Constraints

- **.NET 10** for both projects. Functions on the v4 isolated worker.
- **Azure Cosmos DB SDK 3.x (currently 3.46).** SDK 4.x does not exist. The SDK 3.x LINQ provider transitively requires `Newtonsoft.Json`, so **`Newtonsoft.Json` 13.x stays as an explicit reference** and post/user/etc. models use `[JsonProperty]` for property naming. Do not propose `System.Text.Json` migration — the cost-benefit was evaluated and the decision is documented; revisit only when SDK 4.x ships.
- **Bootstrap 5.3.3, TinyMCE 7.6.1, no jQuery.** Don't reintroduce jQuery.
- **Newtonsoft.Json for Cosmos model serialization.** `System.Text.Json` is fine for non-Cosmos paths (e.g., the JSON Feed endpoint).

## Planning & Scope Rules

- **The active plan defines scope.** Plans live in `D:\Projects\CosmicBlog-planning\docs\superpowers\plans\`. Acceptance criteria in a plan must mirror what the spec calls for — don't expand scope based on codebase exploration.
- If you discover related work that *could* be done but isn't in the active plan, surface it as an **Open Question** in your response — never add it to a task silently.
- Phase 1c is the current focus (engine hardening: real auth, Notes/Now content types, Contact, Newsletter, feeds, SEO). Phases 1d–1g cover authoring UI, post seeder, visual pass, and deploy — each is a separate plan.

## Keeping the Agent Docs Current

`agents/` docs must stay accurate to be useful. Update them in the same change that makes them stale.

**Update triggers:**
- `agents/knowledge/architecture.md` — when a project is added/removed, a Cosmos container is added/removed/repartitioned, the change-feed flow changes shape, or the build/run commands change.
- `agents/knowledge/content-model.md` — when `BlogPost` (or a sibling model) gains/loses a field, when the post-type discriminator's valid values change, or when slug rules change.
- `agents/rules/csharp.md` — when a project-wide convention changes (nullable strictness, Newtonsoft decision, target framework).
- `agents/rules/cosmos.md` — when a partition-key strategy, sproc/trigger pattern, or error-handling convention changes.
- `AGENTS.md` (this file) — when a new file is added under `agents/knowledge/` or `agents/rules/`.

**Not triggers (don't edit docs for these):**
- Bug fixes that don't change a name or shape the docs call out.
- New private methods, new view files, internal refactors.
- Test additions.

When unsure, ask — a stale doc is worse than no doc.

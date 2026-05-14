using System.IO;
using System.Linq;
using BlogWebApp;
using BlogWebApp.Models;
using BlogWebApp.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.Azure.Cosmos;
using Microsoft.Azure.Cosmos.Fluent;
using Microsoft.Azure.Cosmos.Scripts;

var builder = WebApplication.CreateBuilder(args);

// Bind AppSettings to root configuration (preserves prior 3.x behavior: services.Configure<AppSettings>(Configuration);)
builder.Services.Configure<AppSettings>(builder.Configuration);

// Cosmos DB initialization — same blocking initialization as the 3.x version,
// preserved intentionally for teaching clarity.
var (cosmosClient, cosmosService) = await InitializeCosmosBlogClientInstanceAsync(
    builder.Configuration.GetSection("CosmosDbBlog"));
builder.Services.AddSingleton(cosmosClient);
builder.Services.AddSingleton<IBlogCosmosDbService>(cosmosService);

// Image storage manager — uploads embedded base64 images out of post content into Azure Blob Storage
// (prevents Cosmos DB document bloat when authors paste screenshots into TinyMCE).
var storageBlobConnectionString = builder.Configuration.GetValue<string>("StorageBlobConnectionString")
    ?? throw new InvalidOperationException("StorageBlobConnectionString is not configured.");
builder.Services.AddSingleton<IImageStorageManager>(new ImageStorageManager(storageBlobConnectionString));
builder.Services.AddSingleton<IEmailSender, AcsEmailSender>();

// Identity wiring — Cosmos-backed UserStore (no SQL sidecar).
builder.Services.AddSingleton<IUserStore<CosmicBlogUser>>(sp =>
{
    var client = sp.GetRequiredService<CosmosClient>();
    var dbName = builder.Configuration.GetValue<string>("CosmosDbBlog:DatabaseName")
        ?? throw new InvalidOperationException("CosmosDbBlog:DatabaseName missing");
    return new CosmosUserStore(client, dbName);
});

builder.Services.AddIdentityCore<CosmicBlogUser>(options =>
{
    options.Password.RequiredLength = 12;
    options.Password.RequireNonAlphanumeric = false;  // length over complexity
    options.User.RequireUniqueEmail = true;
})
// .AddRoles<IdentityRole>() is intentionally omitted -- it would require an
// IRoleStore<IdentityRole> registration the engine doesn't have (roles live
// as strings inside CosmicBlogUser.Roles, not as separate role entities).
// Instead, CosmicBlogUserClaimsPrincipalFactory (registered below) extends
// the base UserClaimsPrincipalFactory<TUser> to emit role claims from
// IUserRoleStore<CosmicBlogUser>.
.AddSignInManager();

// Override the default claims factory so the auth cookie carries role claims.
builder.Services.AddScoped<IUserClaimsPrincipalFactory<CosmicBlogUser>, CosmicBlogUserClaimsPrincipalFactory>();

// Store usernameNormalized / emailNormalized in lowercase (matches the
// project's lowercase-id convention; see Subscribers partition key).
builder.Services.AddSingleton<ILookupNormalizer, LowerInvariantLookupNormalizer>();

builder.Services
    .AddAuthentication(IdentityConstants.ApplicationScheme)
    .AddCookie(IdentityConstants.ApplicationScheme, options =>
    {
        options.LoginPath = "/login";
        options.AccessDeniedPath = "/login";
        options.SlidingExpiration = true;
        options.ExpireTimeSpan = TimeSpan.FromDays(14);
    });

builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("RequireAdmin", policy => policy.RequireRole("Admin"));
});

builder.Services.AddHttpContextAccessor();
builder.Services.AddControllersWithViews();

var app = builder.Build();

// Bootstrap admin user on first run (only if no admin exists with that email).
using (var scope = app.Services.CreateScope())
{
    var userManager = scope.ServiceProvider.GetRequiredService<UserManager<CosmicBlogUser>>();
    var bootstrapEmail = Environment.GetEnvironmentVariable("COSMICBLOG_BOOTSTRAP_ADMIN_EMAIL");
    var bootstrapPassword = Environment.GetEnvironmentVariable("COSMICBLOG_BOOTSTRAP_ADMIN_PASSWORD");

    if (!string.IsNullOrWhiteSpace(bootstrapEmail) && !string.IsNullOrWhiteSpace(bootstrapPassword))
    {
        var existing = await userManager.FindByEmailAsync(bootstrapEmail);
        if (existing == null)
        {
            var user = new CosmicBlogUser
            {
                Email = bootstrapEmail,
                Username = bootstrapEmail,  // Identity treats Username as the canonical login key
                Roles = new List<string> { "Admin" },  // canonical-case; do NOT route through
                                                       // UserManager.AddToRoleAsync (which would
                                                       // normalize to "ADMIN" and break the cookie
                                                       // role claim — see CosmosUserStore comments).
            };
            var createResult = await userManager.CreateAsync(user, bootstrapPassword);
            if (createResult.Succeeded)
            {
                app.Logger.LogWarning(
                    "Bootstrapped admin user {Email}. Rotate the bootstrap password via /admin/account.",
                    bootstrapEmail);
            }
            else
            {
                app.Logger.LogError(
                    "Failed to bootstrap admin {Email}: {Errors}",
                    bootstrapEmail,
                    string.Join("; ", createResult.Errors.Select(e => e.Description)));
            }
        }
    }
}

if (app.Environment.IsDevelopment())
{
    app.UseDeveloperExceptionPage();
}
else
{
    app.UseExceptionHandler("/BlogError");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseRouting();

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

app.Run();


// --- Cosmos DB bootstrap (moved verbatim from Startup.cs) ---

static async Task<(CosmosClient, BlogCosmosDbService)> InitializeCosmosBlogClientInstanceAsync(IConfigurationSection configurationSection)
{
    string databaseName = configurationSection.GetSection("DatabaseName").Value!;
    string account = configurationSection.GetSection("Account").Value!;
    string key = configurationSection.GetSection("Key").Value!;

    var clientBuilder = new CosmosClientBuilder(account, key);
    CosmosClient client = clientBuilder
        .WithApplicationName(databaseName)
        .WithApplicationName(Regions.EastUS)
        .WithConnectionModeDirect()
        .WithSerializerOptions(new CosmosSerializationOptions { PropertyNamingPolicy = CosmosPropertyNamingPolicy.CamelCase })
        .Build();

    var blogCosmosDbService = new BlogCosmosDbService(client, databaseName);
    DatabaseResponse database = await client.CreateDatabaseIfNotExistsAsync(databaseName);

    // IMPORTANT: container names are also referenced in BlogCosmosDbService.
    await database.Database.DefineContainer(name: "Users", partitionKeyPath: "/userId")
                    .WithUniqueKey()
                        .Path("/username")
                    .Attach()
                    .CreateIfNotExistsAsync();

    // Detect a fresh database (Posts container does not yet exist) so we can seed a Hello World post below.
    bool insertHelloWorldPost = false;
    try
    {
        await client.GetContainer(databaseName, "Posts").ReadContainerAsync();
    }
    catch (CosmosException ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
    {
        insertHelloWorldPost = true;
    }

    await database.Database.CreateContainerIfNotExistsAsync("Posts", "/postId");

    // Note: as of Phase 1c, the "Feed" container is no longer the source of
    // the public homepage activity feed (that query reads Posts directly via
    // GetActivityFeedAsync). Feed is retained because BlogFunctionApp still
    // projects to it and removing it would touch the change-feed Functions.
    await database.Database.CreateContainerIfNotExistsAsync("Feed", "/type");
    await database.Database.CreateContainerIfNotExistsAsync("Subscribers", "/id");

    // Upsert the sprocs in the posts container.
    var postsContainer = database.Database.GetContainer("Posts");
    await UpsertStoredProcedureAsync(postsContainer, @"CosmosDbScripts\sprocs\createComment.js");
    await UpsertStoredProcedureAsync(postsContainer, @"CosmosDbScripts\sprocs\createLike.js");
    await UpsertStoredProcedureAsync(postsContainer, @"CosmosDbScripts\sprocs\deleteLike.js");
    await UpsertStoredProcedureAsync(postsContainer, @"CosmosDbScripts\sprocs\updateUsernames.js");

    // Add the feed container post-trigger (truncates the Feed container).
    var feedContainer = database.Database.GetContainer("Feed");
    await UpsertTriggerAsync(feedContainer, @"CosmosDbScripts\triggers\truncateFeed.js", TriggerOperation.All, TriggerType.Post);

    // Seed a Hello World post on first run so the home page is never empty.
    if (insertHelloWorldPost)
    {
        const string helloWorldPostHtml = @"
                <p>Hi there!</p>
                <p>Welcome to CosmicBlog — a learn-in-public blog engine on .NET 10 + Azure Cosmos DB. The Cosmos partition strategy is based on the Microsoft docs article <a target='_blank' href='https://docs.microsoft.com/en-us/azure/cosmos-db/how-to-model-partition-example'>How to model and partition data on Azure Cosmos DB using a real-world example</a>.</p>
                <p>To login as the Blog Administrator, set COSMICBLOG_BOOTSTRAP_ADMIN_EMAIL and COSMICBLOG_BOOTSTRAP_ADMIN_PASSWORD environment variables on first run to bootstrap the admin account.</p>
                <p>Please post any issues that you have with this code to the repository at <a target='_blank' href='https://github.com/townbackyard/CosmicBlog/issues'>https://github.com/townbackyard/CosmicBlog/issues</a></p>
        ";

        var helloWorldPost = new BlogPost
        {
            PostId = Guid.NewGuid().ToString(),
            Type = "post",
            Slug = "hello-world",
            Format = "html",            // Seed content is HTML; existing markup uses <p>/<a>.
            Status = "published",
            PublishedAtUtc = DateTime.UtcNow,
            Title = "Hello World!",
            Content = helloWorldPostHtml,
            AuthorId = Guid.NewGuid().ToString(),
            AuthorUsername = "HelloWorldAdmin",
            DateCreated = DateTime.UtcNow,
        };

        await postsContainer.UpsertItemAsync(helloWorldPost, new PartitionKey(helloWorldPost.PostId));
    }

    // Phase 1d migration: backfill Format / Status / PublishedAtUtc on legacy
    // BlogPost docs that don't have those fields set. Iterates as JObject so
    // the C# property initializer defaults (Format="markdown", Status="published")
    // don't mask which fields were actually missing in the stored JSON. Filters
    // by p.type so comment/like docs (which share the Posts partition) are
    // untouched -- their absence would otherwise project into half-empty
    // BlogPost shapes and overwrite their parent posts on upsert.
    //
    // Idempotent: once a doc has been backfilled, the IS_DEFINED filter
    // excludes it from subsequent runs. A partial failure mid-loop leaves
    // already-upserted docs migrated; a re-run picks up the rest.
    var migrationQuery = new QueryDefinition(
        "SELECT * FROM p WHERE p.type IN ('post', 'note', 'now') AND (NOT IS_DEFINED(p.format) OR NOT IS_DEFINED(p.status) OR NOT IS_DEFINED(p.publishedAtUtc))");
    var migrationIter = postsContainer.GetItemQueryIterator<Newtonsoft.Json.Linq.JObject>(migrationQuery);
    int migrated = 0;
    while (migrationIter.HasMoreResults)
    {
        var resp = await migrationIter.ReadNextAsync();
        foreach (var doc in resp)
        {
            // Legacy docs are all considered HTML and currently-published.
            // PublishedAtUtc defaults to DateCreated for backward-compatible ordering.
            if (doc["format"] == null) doc["format"] = "html";
            if (doc["status"] == null) doc["status"] = "published";
            if (doc["publishedAtUtc"] == null) doc["publishedAtUtc"] = doc["dateCreated"];
            var postId = doc["postId"]?.ToString()
                ?? throw new InvalidOperationException("Legacy Posts doc missing postId during Phase 1d migration");
            await postsContainer.UpsertItemAsync(doc, new PartitionKey(postId));
            migrated++;
        }
    }
    if (migrated > 0)
    {
        Console.WriteLine($"Phase 1d migration: backfilled {migrated} legacy Posts docs with Format/Status/PublishedAtUtc.");
    }

    return (client, blogCosmosDbService);
}

static async Task UpsertStoredProcedureAsync(Container container, string scriptFileName)
{
    string scriptId = Path.GetFileNameWithoutExtension(scriptFileName);
    if (await StoredProcedureExists(container, scriptId))
    {
        await container.Scripts.ReplaceStoredProcedureAsync(
            new StoredProcedureProperties(scriptId, await File.ReadAllTextAsync(scriptFileName)));
    }
    else
    {
        await container.Scripts.CreateStoredProcedureAsync(
            new StoredProcedureProperties(scriptId, await File.ReadAllTextAsync(scriptFileName)));
    }
}

static async Task<bool> StoredProcedureExists(Container container, string sprocId)
{
    Scripts cosmosScripts = container.Scripts;
    try
    {
        await cosmosScripts.ReadStoredProcedureAsync(sprocId);
        return true;
    }
    catch (CosmosException ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
    {
        return false;
    }
}

static async Task UpsertTriggerAsync(Container container, string scriptFileName, TriggerOperation triggerOperation, TriggerType triggerType)
{
    string scriptId = Path.GetFileNameWithoutExtension(scriptFileName);
    if (await TriggerExists(container, scriptId))
    {
        await container.Scripts.ReplaceTriggerAsync(new TriggerProperties
        {
            Id = scriptId,
            Body = await File.ReadAllTextAsync(scriptFileName),
            TriggerOperation = triggerOperation,
            TriggerType = triggerType,
        });
    }
    else
    {
        await container.Scripts.CreateTriggerAsync(new TriggerProperties
        {
            Id = scriptId,
            Body = await File.ReadAllTextAsync(scriptFileName),
            TriggerOperation = triggerOperation,
            TriggerType = triggerType,
        });
    }
}

static async Task<bool> TriggerExists(Container container, string triggerId)
{
    Scripts cosmosScripts = container.Scripts;
    try
    {
        await cosmosScripts.ReadTriggerAsync(triggerId);
        return true;
    }
    catch (CosmosException ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
    {
        return false;
    }
}

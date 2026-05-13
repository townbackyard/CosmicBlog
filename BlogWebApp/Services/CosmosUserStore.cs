using System;
using System.Collections.Generic;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using BlogWebApp.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.Azure.Cosmos;

namespace BlogWebApp.Services
{
    public class CosmosUserStore :
        IUserStore<CosmicBlogUser>,
        IUserPasswordStore<CosmicBlogUser>,
        IUserEmailStore<CosmicBlogUser>,
        IUserRoleStore<CosmicBlogUser>
    {
        private readonly Container _container;

        public CosmosUserStore(CosmosClient client, string databaseName)
        {
            _container = client.GetContainer(databaseName, "Users");
        }

        // --- IUserStore ---

        public async Task<IdentityResult> CreateAsync(CosmicBlogUser user, CancellationToken ct)
        {
            await _container.UpsertItemAsync(user, new PartitionKey(user.UserId), cancellationToken: ct);
            return IdentityResult.Success;
        }

        public async Task<IdentityResult> UpdateAsync(CosmicBlogUser user, CancellationToken ct)
        {
            await _container.UpsertItemAsync(user, new PartitionKey(user.UserId), cancellationToken: ct);
            return IdentityResult.Success;
        }

        public async Task<IdentityResult> DeleteAsync(CosmicBlogUser user, CancellationToken ct)
        {
            await _container.DeleteItemAsync<CosmicBlogUser>(user.UserId, new PartitionKey(user.UserId), cancellationToken: ct);
            return IdentityResult.Success;
        }

        public async Task<CosmicBlogUser?> FindByIdAsync(string userId, CancellationToken ct)
        {
            try
            {
                var resp = await _container.ReadItemAsync<CosmicBlogUser>(userId, new PartitionKey(userId), cancellationToken: ct);
                return resp.Resource;
            }
            catch (CosmosException ex) when (ex.StatusCode == HttpStatusCode.NotFound)
            {
                return null;
            }
        }

        public async Task<CosmicBlogUser?> FindByNameAsync(string normalizedUserName, CancellationToken ct)
        {
            var query = new QueryDefinition(
                "SELECT TOP 1 * FROM u WHERE u.usernameNormalized = @n")
                .WithParameter("@n", normalizedUserName);
            var iterator = _container.GetItemQueryIterator<CosmicBlogUser>(query);
            while (iterator.HasMoreResults)
            {
                var resp = await iterator.ReadNextAsync(ct);
                foreach (var u in resp) return u;
            }
            return null;
        }

        public Task<string> GetUserIdAsync(CosmicBlogUser user, CancellationToken ct) => Task.FromResult(user.UserId);
        public Task<string?> GetUserNameAsync(CosmicBlogUser user, CancellationToken ct) => Task.FromResult<string?>(user.Username);
        public Task SetUserNameAsync(CosmicBlogUser user, string? userName, CancellationToken ct)
        {
            user.Username = userName ?? string.Empty;
            return Task.CompletedTask;
        }
        public Task<string?> GetNormalizedUserNameAsync(CosmicBlogUser user, CancellationToken ct) => Task.FromResult<string?>(user.UsernameNormalized);
        public Task SetNormalizedUserNameAsync(CosmicBlogUser user, string? normalizedName, CancellationToken ct)
        {
            user.UsernameNormalized = normalizedName ?? string.Empty;
            return Task.CompletedTask;
        }

        // --- IUserPasswordStore ---

        public Task SetPasswordHashAsync(CosmicBlogUser user, string? passwordHash, CancellationToken ct)
        {
            user.PasswordHash = passwordHash ?? string.Empty;
            return Task.CompletedTask;
        }
        public Task<string?> GetPasswordHashAsync(CosmicBlogUser user, CancellationToken ct) => Task.FromResult<string?>(user.PasswordHash);
        public Task<bool> HasPasswordAsync(CosmicBlogUser user, CancellationToken ct) => Task.FromResult(!string.IsNullOrEmpty(user.PasswordHash));

        // --- IUserEmailStore ---

        public Task SetEmailAsync(CosmicBlogUser user, string? email, CancellationToken ct)
        {
            user.Email = email ?? string.Empty;
            return Task.CompletedTask;
        }
        public Task<string?> GetEmailAsync(CosmicBlogUser user, CancellationToken ct) => Task.FromResult<string?>(user.Email);
        public Task<bool> GetEmailConfirmedAsync(CosmicBlogUser user, CancellationToken ct) => Task.FromResult(true);  // admin-only; treat as confirmed
        public Task SetEmailConfirmedAsync(CosmicBlogUser user, bool confirmed, CancellationToken ct) => Task.CompletedTask;

        public async Task<CosmicBlogUser?> FindByEmailAsync(string normalizedEmail, CancellationToken ct)
        {
            var query = new QueryDefinition(
                "SELECT TOP 1 * FROM u WHERE u.emailNormalized = @e")
                .WithParameter("@e", normalizedEmail);
            var iterator = _container.GetItemQueryIterator<CosmicBlogUser>(query);
            while (iterator.HasMoreResults)
            {
                var resp = await iterator.ReadNextAsync(ct);
                foreach (var u in resp) return u;
            }
            return null;
        }
        public Task<string?> GetNormalizedEmailAsync(CosmicBlogUser user, CancellationToken ct) => Task.FromResult<string?>(user.EmailNormalized);
        public Task SetNormalizedEmailAsync(CosmicBlogUser user, string? normalizedEmail, CancellationToken ct)
        {
            user.EmailNormalized = normalizedEmail ?? string.Empty;
            return Task.CompletedTask;
        }

        // --- IUserRoleStore ---

        public Task AddToRoleAsync(CosmicBlogUser user, string roleName, CancellationToken ct)
        {
            if (!user.Roles.Contains(roleName)) user.Roles.Add(roleName);
            return Task.CompletedTask;
        }
        public Task RemoveFromRoleAsync(CosmicBlogUser user, string roleName, CancellationToken ct)
        {
            user.Roles.Remove(roleName);
            return Task.CompletedTask;
        }
        public Task<IList<string>> GetRolesAsync(CosmicBlogUser user, CancellationToken ct) => Task.FromResult<IList<string>>(user.Roles);
        public Task<bool> IsInRoleAsync(CosmicBlogUser user, string roleName, CancellationToken ct) => Task.FromResult(user.Roles.Contains(roleName));
        public Task<IList<CosmicBlogUser>> GetUsersInRoleAsync(string roleName, CancellationToken ct)
        {
            // Not used in v1 admin flow; throw NotImplementedException is acceptable.
            throw new NotImplementedException("GetUsersInRoleAsync is not required for the admin-only auth flow in v1.");
        }

        public void Dispose() { }
    }
}

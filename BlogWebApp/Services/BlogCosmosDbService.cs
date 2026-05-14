using BlogWebApp.Models;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Azure.Cosmos;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace BlogWebApp.Services
{
    public interface IBlogCosmosDbService
    {

        Task<List<BlogPost>> GetBlogPostsMostRecentAsync(int numberOfPosts);
        Task<List<BlogPost>> GetBlogPostsForUserId(string userId);

        Task<BlogPost?> GetBlogPostAsync(string postId);
        Task UpsertBlogPostAsync(BlogPost post);

        // Slug-based lookup (single post or note)
        Task<BlogPost?> GetBlogPostBySlugAsync(string type, string slug);

        // Type-filtered most-recent query (used by /posts, /notes pages)
        Task<List<BlogPost>> GetMostRecentByTypeAsync(string type, int count);

        // Unified activity-feed query (posts + notes interleaved by dateCreated DESC).
        // Excludes "now" — the Now page is read by id, not as feed content.
        Task<List<BlogPost>> GetActivityFeedAsync(int count);

        // Now singleton — there is exactly one document with id = "now-singleton"
        Task<BlogPost?> GetNowAsync();

        /// <summary>
        /// Upsert the Now page singleton. The implementation overwrites
        /// <c>PostId</c>, <c>Type</c>, and <c>DateUpdated</c> on the supplied
        /// object before writing (the caller's instance is mutated in place).
        /// <para>On first creation, the caller must set <c>DateCreated</c> —
        /// this method does not initialize it.</para>
        /// </summary>
        Task UpsertNowAsync(BlogPost now);

        Task CreateBlogPostCommentAsync(BlogPostComment comment);
        Task<List<BlogPostComment>> GetBlogPostCommentsAsync(string postId);


        Task CreateBlogPostLikeAsync(BlogPostLike like);
        Task DeleteBlogPostLikeAsync(string postId, string userId);
        Task<List<BlogPostLike>> GetBlogPostLikesAsync(string postId);
        Task<BlogPostLike?> GetBlogPostLikeForUserIdAsync(string postId, string userId);


        Task CreateUserAsync(BlogUser user);
        Task UpdateUsernameAsync(BlogUser userWithUpdatedUsername, string oldUsername);

        Task<BlogUser?> GetUserAsync(string username);

        Task AddSubscriberAsync(Subscriber subscriber);
        Task<Subscriber?> GetSubscriberAsync(string emailNormalized);

    }

    public class BlogCosmosDbService : IBlogCosmosDbService
    {

        private Container _usersContainer;
        private Container _postsContainer;
        private Container _feedContainer;
        private Container _subscribersContainer;

        public BlogCosmosDbService(CosmosClient dbClient, string databaseName)
        {
            _usersContainer = dbClient.GetContainer(databaseName, "Users");
            _postsContainer = dbClient.GetContainer(databaseName, "Posts");
            _feedContainer = dbClient.GetContainer(databaseName, "Feed");
            _subscribersContainer = dbClient.GetContainer(databaseName, "Subscribers");
        }


        public async Task<List<BlogPost>> GetBlogPostsMostRecentAsync(int numberOfPosts)
        {

            var blogPosts = new List<BlogPost>();

            var queryString = $"SELECT TOP {numberOfPosts} * FROM f WHERE f.type='post' ORDER BY f.dateCreated DESC";
            var query = _feedContainer.GetItemQueryIterator<BlogPost>(new QueryDefinition(queryString));
            while (query.HasMoreResults)
            {
                var response = await query.ReadNextAsync();
                var ru = response.RequestCharge;
                blogPosts.AddRange(response.ToList());
            }

            //if there are no posts in the feedcontainer, go to the posts container.
            // There may be one that has not propagated to the feed container yet by the azure function (or the azure function is not running).
            if (!blogPosts.Any())
            {
                var queryFromPostsContainter = _postsContainer.GetItemQueryIterator<BlogPost>(new QueryDefinition(queryString));
                while (queryFromPostsContainter.HasMoreResults)
                {
                    var response = await queryFromPostsContainter.ReadNextAsync();
                    var ru = response.RequestCharge;
                    blogPosts.AddRange(response.ToList());
                }
            }

            return blogPosts;
        }


        public async Task<List<BlogPost>> GetBlogPostsForUserId(string userId)
        {

            var blogPosts = new List<BlogPost>();


            var queryString = $"SELECT * FROM p WHERE p.type='post' AND p.userId = @UserId ORDER BY p.dateCreated DESC";

            var queryDef = new QueryDefinition(queryString);
            queryDef.WithParameter("@UserId", userId);
            var query = this._usersContainer.GetItemQueryIterator<BlogPost>(queryDef);

            while (query.HasMoreResults)
            {
                var response = await query.ReadNextAsync();
                var ru = response.RequestCharge;
                blogPosts.AddRange(response.ToList());
            }

            return blogPosts;
        }


        public async Task<BlogPost?> GetBlogPostAsync(string postId)
        {
            try
            {
                //When getting the blogpost from the Posts container, the id is postId and the partitionKey is also postId.
                //  This will automatically return only the type="post" for this postId (and not the type=comment or any other types in the same partition postId)
                ItemResponse<BlogPost> response = await this._postsContainer.ReadItemAsync<BlogPost>(postId, new PartitionKey(postId));
                var ru = response.RequestCharge;
                return response.Resource;
            }
            catch (CosmosException ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
            {
                return null;
            }
        }


        public async Task UpsertBlogPostAsync(BlogPost post)
        {
            await this._postsContainer.UpsertItemAsync<BlogPost>(post, new PartitionKey(post.PostId));
        }


        public async Task<BlogPost?> GetBlogPostBySlugAsync(string type, string slug)
        {
            var query = new QueryDefinition(
                "SELECT TOP 1 * FROM p WHERE p.type = @type AND p.slug = @slug")
                .WithParameter("@type", type)
                .WithParameter("@slug", slug);

            var iterator = _postsContainer.GetItemQueryIterator<BlogPost>(query);
            while (iterator.HasMoreResults)
            {
                var response = await iterator.ReadNextAsync();
                foreach (var item in response) return item;
            }
            return null;
        }

        public async Task<List<BlogPost>> GetMostRecentByTypeAsync(string type, int count)
        {
            var query = new QueryDefinition(
                $"SELECT TOP {count} * FROM p WHERE p.type = @type ORDER BY p.dateCreated DESC")
                .WithParameter("@type", type);

            var results = new List<BlogPost>();
            var iterator = _postsContainer.GetItemQueryIterator<BlogPost>(query);
            while (iterator.HasMoreResults)
            {
                var response = await iterator.ReadNextAsync();
                results.AddRange(response);
            }
            return results;
        }

        public async Task<List<BlogPost>> GetActivityFeedAsync(int count)
        {
            var query = new QueryDefinition(
                $"SELECT TOP {count} * FROM p WHERE p.type IN ('post', 'note') ORDER BY p.dateCreated DESC");

            var results = new List<BlogPost>();
            var iterator = _postsContainer.GetItemQueryIterator<BlogPost>(query);
            while (iterator.HasMoreResults)
            {
                var response = await iterator.ReadNextAsync();
                results.AddRange(response);
            }
            return results;
        }

        public async Task<BlogPost?> GetNowAsync()
        {
            try
            {
                var response = await _postsContainer.ReadItemAsync<BlogPost>(
                    "now-singleton", new PartitionKey("now-singleton"));
                return response.Resource;
            }
            catch (CosmosException ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
            {
                return null;
            }
        }

        public async Task UpsertNowAsync(BlogPost now)
        {
            now.PostId = "now-singleton";  // enforce the singleton id
            now.Type = "now";
            now.DateUpdated = DateTime.UtcNow;
            await _postsContainer.UpsertItemAsync(now, new PartitionKey("now-singleton"));
        }


        public async Task CreateBlogPostCommentAsync(BlogPostComment comment)
        {
            //string str = JsonConvert.SerializeObject(comment);
            //dynamic obj = JsonConvert.DeserializeObject(str);

            var obj = new dynamic[] { comment.PostId, comment };

            //var result = await _blogDbService.GetContainer("database", "container").Scripts.ExecuteStoredProcedureAsync<string>("spCreateToDoItem", new PartitionKey("Personal"), newItem);
            var result = await _postsContainer.Scripts.ExecuteStoredProcedureAsync<string>("createComment", new PartitionKey(comment.PostId), obj);
            //await this._postsContainer.CreateItemAsync<BlogPostComment>(comment, new PartitionKey(comment.PostId));
        }

        public async Task<List<BlogPostComment>> GetBlogPostCommentsAsync(string postId)
        {
            var queryString = $"SELECT * FROM p WHERE p.type='comment' AND p.postId = @PostId ORDER BY p.dateCreated DESC";

            var queryDef = new QueryDefinition(queryString);
            queryDef.WithParameter("@PostId", postId);
            var query = this._postsContainer.GetItemQueryIterator<BlogPostComment>(queryDef);

            List<BlogPostComment> comments = new List<BlogPostComment>();
            while (query.HasMoreResults)
            {
                var response = await query.ReadNextAsync();
                var ru = response.RequestCharge;
                comments.AddRange(response.ToList());
            }

            return comments;
        }


        public async Task CreateBlogPostLikeAsync(BlogPostLike like)
        {
            //string str = JsonConvert.SerializeObject(comment);
            //dynamic obj = JsonConvert.DeserializeObject(str);

            var obj = new dynamic[] { like.PostId, like };

            //var result = await _blogDbService.GetContainer("database", "container").Scripts.ExecuteStoredProcedureAsync<string>("spCreateToDoItem", new PartitionKey("Personal"), newItem);
            var result = await _postsContainer.Scripts.ExecuteStoredProcedureAsync<string>("createLike", new PartitionKey(like.PostId), obj);
            //await this._postsContainer.CreateItemAsync<BlogPostComment>(comment, new PartitionKey(comment.PostId));
        }
        public async Task DeleteBlogPostLikeAsync(string postId, string userId)
        {
            var obj = new dynamic[] { postId, userId };
            var result = await _postsContainer.Scripts.ExecuteStoredProcedureAsync<string>("deleteLike", new PartitionKey(postId), obj);
        }

        public async Task<List<BlogPostLike>> GetBlogPostLikesAsync(string postId)
        {
            var queryString = $"SELECT * FROM p WHERE p.type='like' AND p.postId = @PostId ORDER BY p.dateCreated DESC";

            var queryDef = new QueryDefinition(queryString);
            queryDef.WithParameter("@PostId", postId);
            var query = this._postsContainer.GetItemQueryIterator<BlogPostLike>(queryDef);

            List<BlogPostLike> likes = new List<BlogPostLike>();
            while (query.HasMoreResults)
            {
                var response = await query.ReadNextAsync();
                var ru = response.RequestCharge;
                likes.AddRange(response.ToList());
            }

            return likes;
        }

        public async Task<BlogPostLike?> GetBlogPostLikeForUserIdAsync(string postId, string userId)
        {
            var queryString = $"SELECT TOP 1 * FROM p WHERE p.type='like' AND p.postId = @PostId AND p.userId = @UserId ORDER BY p.dateCreated DESC";

            var queryDef = new QueryDefinition(queryString);
            queryDef.WithParameter("@PostId", postId);
            queryDef.WithParameter("@UserId", userId);
            var query = this._postsContainer.GetItemQueryIterator<BlogPostLike>(queryDef);

            BlogPostLike? like = null;
            if (query.HasMoreResults)
            {
                var response = await query.ReadNextAsync();
                var ru = response.RequestCharge;
                like = response.FirstOrDefault();
            }

            return like;
        }


        public async Task CreateUserAsync(BlogUser user)
        {

            var uniqueUsername = new UniqueUsername { Username = user.Username };

            //First create a user with a partitionkey as "unique_username" and the new username.  Using the same partitionKey "unique_username" will put all of the username in the same logical partition.
            //  Since there is a Unique Key on /username (per logical partition), trying to insert a duplicate username with partition key "unique_username" will cause a Conflict.
            //  This question/answer https://stackoverflow.com/a/62438454/21579
            await _usersContainer.CreateItemAsync<UniqueUsername>(uniqueUsername, new PartitionKey(uniqueUsername.UserId));

            user.Action = "Create";

            //if we get past adding a new username for partition key "unique_username", then go ahead and insert the new user.
            await _usersContainer.CreateItemAsync<BlogUser>(user, new PartitionKey(user.UserId));
            //await _usersContainer.CreateItemAsync<BlogUser>(user, new PartitionKey(user.UserId), new ItemRequestOptions { PreTriggers = new List<string> { "validateUserUsernameNotExists" } });
        }

        private class UniqueUsername
        {

            [JsonProperty(PropertyName = "id")]
            public string Id => System.Guid.NewGuid().ToString();

            [JsonProperty(PropertyName = "userId")]
            public string UserId => "unique_username";

            [JsonProperty(PropertyName = "type")]
            public string Type => "unique_username";

            [JsonProperty(PropertyName = "username")]
            public string Username { get; set; } = string.Empty;


        }

        public async Task UpdateUsernameAsync(BlogUser userWithUpdatedUsername, string oldUsername)
        {
            //first try to create the username in the partition with partitionKey "unique_username" to confirm the username does not exist already
            var uniqueUsername = new UniqueUsername { Username = userWithUpdatedUsername.Username };

            //First create a user with a partitionkey as "unique_username" and the new username.  Using the same partitionKey "unique_username" will put all of the username in the same logical partition.
            //  Since there is a Unique Key on /username (per logical partition), trying to insert a duplicate username with partition key "unique_username" will cause a Conflict.
            //  See this question/answer https://stackoverflow.com/a/62438454/21579
            await _usersContainer.CreateItemAsync<UniqueUsername>(uniqueUsername, new PartitionKey(uniqueUsername.UserId));

            userWithUpdatedUsername.Action = "Update";

            //if we get past adding a new username for partition key "unique_username", then go ahead and update this user's username
            await _usersContainer.ReplaceItemAsync<BlogUser>(userWithUpdatedUsername, userWithUpdatedUsername.UserId, new PartitionKey(userWithUpdatedUsername.UserId));

            //then we need to delete the old "unique_username" for the username that just changed.
            var queryDefinition = new QueryDefinition("SELECT * FROM u WHERE u.userId = 'unique_username' AND u.type = 'unique_username' AND u.username = @username").WithParameter("@username", oldUsername);
            var query = this._usersContainer.GetItemQueryIterator<BlogUniqueUsername>(queryDefinition);
            while (query.HasMoreResults)
            {
                var response = await query.ReadNextAsync();

                var oldUniqueUsernames = response.ToList();

                foreach (var oldUniqueUsername in oldUniqueUsernames)
                {
                    //Last delete the old unique username entry
                    await _usersContainer.DeleteItemAsync<BlogUser>(oldUniqueUsername.Id, new PartitionKey("unique_username"));
                }
            }

        }


        public async Task AddSubscriberAsync(Subscriber subscriber)
        {
            await _subscribersContainer.UpsertItemAsync(subscriber, new PartitionKey(subscriber.Id));
        }

        public async Task<Subscriber?> GetSubscriberAsync(string emailNormalized)
        {
            try
            {
                var resp = await _subscribersContainer.ReadItemAsync<Subscriber>(
                    emailNormalized, new PartitionKey(emailNormalized));
                return resp.Resource;
            }
            catch (CosmosException ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
            {
                return null;
            }
        }


        public async Task<BlogUser?> GetUserAsync(string username)
        {

            var queryDefinition = new QueryDefinition("SELECT * FROM u WHERE u.type = 'user' AND u.username = @username").WithParameter("@username", username);

            var query = this._usersContainer.GetItemQueryIterator<BlogUser>(queryDefinition);

            List<BlogUser> results = new List<BlogUser>();
            while (query.HasMoreResults)
            {
                var response = await query.ReadNextAsync();

                results.AddRange(response.ToList());
            }

            if (results.Count > 1)
            {
                throw new Exception($"More than one user found for username '{username}'");
            }

            var u = results.SingleOrDefault();
            return u;

        }


    }
}

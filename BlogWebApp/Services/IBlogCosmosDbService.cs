using BlogWebApp.Models;
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
}

using System;
using System.Linq;
using System.Threading.Tasks;
using BlogWebApp.ViewModels;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using BlogWebApp.Services;
using Microsoft.AspNetCore.Authorization;
using BlogWebApp.Models;
using System.Security.Claims;

namespace BlogWebApp.Controllers
{
    public class BlogPostController : Controller
    {

        private readonly ILogger<BlogController> _logger;
        private readonly IBlogCosmosDbService _blogDbService;
        private readonly IImageStorageManager _imageStorageManager;

        public BlogPostController(
            ILogger<BlogController> logger,
            IBlogCosmosDbService blogDbService,
            IImageStorageManager imageStorageManager)
        {
            _logger = logger;
            _blogDbService = blogDbService;
            _imageStorageManager = imageStorageManager;
        }

        [Route("posts/{slug}")]
        public async Task<IActionResult> PostView(string slug)
        {
            var bp = await _blogDbService.GetBlogPostBySlugAsync("post", slug);
            if (bp == null) return View("PostNotFound");

            var m = new BlogPostViewViewModel
            {
                PostId = bp.PostId,
                Slug = bp.Slug,
                Title = bp.Title,
                Content = bp.Content,
                Format = bp.Format,
                Tags = bp.Tags,
                Excerpt = bp.Excerpt,
                AuthorId = bp.AuthorId,
                AuthorUsername = bp.AuthorUsername,
                DateCreated = bp.DateCreated,
            };
            return View(m);
        }


        [Route("admin/posts/new")]
        [Authorize("RequireAdmin")]
        public IActionResult PostNew()
        {

            var m = new BlogPostEditViewModel
            {
                Title = "",
                Content = ""
            };
            return View("PostEdit", m);
        }



        [Route("admin/posts/edit/{postId}")]
        [Authorize("RequireAdmin")]
        public async Task<IActionResult> PostEdit(string postId)
        {
            var bp = await _blogDbService.GetBlogPostAsync(postId);

            if (bp == null)
            {
                return View("PostNotFound");
            }

            var m = new BlogPostEditViewModel
            {
                PostId = bp.PostId,
                Title = bp.Title,
                Content = bp.Content,
                Slug = bp.Slug,
                Status = bp.Status,
                PublishedAtUtc = bp.PublishedAtUtc,
                Tags = bp.Tags,
                Excerpt = bp.Excerpt,
                CoverImageUrl = bp.CoverImageUrl,
            };
            return View(m);
        }


        [Route("admin/posts/new")]
        [Authorize("RequireAdmin")]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> PostNew(BlogPostEditViewModel blogPostChanges)
        {
            if (!ModelState.IsValid) return View("PostEdit", blogPostChanges);

            var postId = Guid.NewGuid().ToString();
            var slug = SlugGenerator.FromTitle(blogPostChanges.Title);

            // Ensure slug uniqueness — append short suffix on collision
            if (!string.IsNullOrEmpty(slug)
                && await _blogDbService.GetBlogPostBySlugAsync("post", slug) != null)
            {
                slug = $"{slug}-{postId.Substring(0, 8)}";
            }

            // If title was empty/whitespace and slug came out empty, fall back to postId for the slug
            // so the post still has a URL. This shouldn't happen in practice (Title is required by the
            // existing view model), but it's defensive.
            if (string.IsNullOrEmpty(slug)) slug = postId.Substring(0, 8);

            // The hidden field arrives as a single comma-separated string; split on commas,
            // trim, drop empties, then enforce the 12-cap server-side with an explicit
            // ModelState error (rather than silently truncating).
            var tags = (Request.Form["Tags"].ToString() ?? "")
                .Split(',', StringSplitOptions.RemoveEmptyEntries)
                .Select(t => t.Trim())
                .Where(t => !string.IsNullOrEmpty(t))
                .ToList();
            if (tags.Count > 12)
            {
                ModelState.AddModelError("Tags", "Maximum 12 tags per post.");
                return View("PostEdit", blogPostChanges);
            }

            var blogPost = new BlogPost
            {
                PostId = postId,
                Type = "post",
                Slug = slug,
                Format = "markdown",
                Status = blogPostChanges.Status,
                PublishedAtUtc = blogPostChanges.PublishedAtUtc
                                 ?? (blogPostChanges.Status == "published" ? DateTime.UtcNow : (DateTime?)null),
                Title = blogPostChanges.Title,
                Content = blogPostChanges.Content,
                Excerpt = blogPostChanges.Excerpt,
                CoverImageUrl = blogPostChanges.CoverImageUrl,
                Tags = tags,
                AuthorId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value
                    ?? throw new InvalidOperationException("Authenticated user has no NameIdentifier claim."),
                AuthorUsername = User.Identity?.Name
                    ?? throw new InvalidOperationException("Authenticated user has no name."),
                DateCreated = DateTime.UtcNow,
            };

            //Insert the new blog post into the database.
            await _blogDbService.UpsertBlogPostAsync(blogPost);

            //Show the view with a message that the blog post has been created.
            ViewBag.Success = true;
            blogPostChanges.Slug = slug;  // populate so the Cancel/preview link in PostEdit.cshtml resolves to /posts/{slug}

            return View("PostEdit", blogPostChanges);
        }


        [Route("admin/posts/edit/{postId}")]
        [Authorize("RequireAdmin")]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> PostEdit(string postId, BlogPostEditViewModel blogPostChanges)
        {
            if (!ModelState.IsValid) return View(blogPostChanges);

            var bp = await _blogDbService.GetBlogPostAsync(postId);

            if (bp == null) return View("PostNotFound");

            var tags = (Request.Form["Tags"].ToString() ?? "")
                .Split(',', StringSplitOptions.RemoveEmptyEntries)
                .Select(t => t.Trim())
                .Where(t => !string.IsNullOrEmpty(t))
                .ToList();
            if (tags.Count > 12)
            {
                ModelState.AddModelError("Tags", "Maximum 12 tags per post.");
                return View(blogPostChanges);
            }

            // Do NOT reassign bp.Slug -- slugs are stable across edits (URL contracts).
            bp.Title = blogPostChanges.Title;
            bp.Content = blogPostChanges.Content;
            bp.Excerpt = blogPostChanges.Excerpt;
            bp.CoverImageUrl = blogPostChanges.CoverImageUrl;
            bp.Tags = tags;
            bp.Status = blogPostChanges.Status;
            bp.PublishedAtUtc = blogPostChanges.PublishedAtUtc
                                ?? (bp.Status == "published" && !bp.PublishedAtUtc.HasValue ? DateTime.UtcNow : bp.PublishedAtUtc);
            bp.DateUpdated = DateTime.UtcNow;

            //Update the database with these changes.
            await _blogDbService.UpsertBlogPostAsync(bp);

            //Show the view with a message that the blog post has been updated.
            ViewBag.Success = true;

            return View(blogPostChanges);
        }



        /// <summary>
        /// Scans the supplied HTML for embedded base64 image data (typical when pasting
        /// screenshots into TinyMCE), uploads each one to Azure Blob Storage, and rewrites
        /// the HTML to reference the blob URLs. Prevents Cosmos DB document bloat.
        /// </summary>
        public async Task<string> UploadAnyBase64Images(string s, string postId)
        {
            if (string.IsNullOrEmpty(s))
            {
                return s;
            }

            var start = s.IndexOf(" src=\"data:image/");
            if (start == -1)
            {
                return s;
            }

            // find the start of the base64 string
            var startBase64 = s.IndexOf(";base64,", start);
            if (startBase64 == -1)
            {
                return s;
            }

            startBase64 += ";base64,".Length;

            var end = s.IndexOf("\" ", startBase64);
            if (end == -1)
            {
                return s;
            }

            if (end <= startBase64)
            {
                return s;
            }

            var newStringStart = s.Substring(0, start);
            var base64String = s.Substring(startBase64, end - startBase64);
            var newStringEnd = s.Substring(end + "\" ".Length);

            // convert the base64 string to bytes
            byte[] imageBytes = Convert.FromBase64String(base64String);

            var blobName = $"{postId}/{Guid.NewGuid()}.png";

            // upload the image to Azure Storage
            await _imageStorageManager.UploadBlob("blog-post-images", blobName, "image/png", imageBytes);

            var newString = newStringStart + "src=\"" + $"/img/post/{blobName}" + "\" " + newStringEnd;

            // recursively call method to check for any additional base64 images
            return await UploadAnyBase64Images(newString, postId);
        }

    }
}
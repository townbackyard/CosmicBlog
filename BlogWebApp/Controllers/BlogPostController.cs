using System;
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

        [Route("post/{postId}")]
        public async Task<IActionResult> PostView(string postId)
        {
            var bp = await _blogDbService.GetBlogPostAsync(postId);

            if (bp == null)
            {
                return View("PostNotFound");
            }

            var m = new BlogPostViewViewModel
            {
                PostId = bp.PostId,
                Title = bp.Title,
                Content = bp.Content,
                AuthorId = bp.AuthorId,
                AuthorUsername = bp.AuthorUsername,
                DateCreated = bp.DateCreated,
            };
            return View(m);
        }


        [Route("post/new")]
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



        [Route("post/edit/{postId}")]
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
                Title = bp.Title,
                Content = bp.Content
            };
            return View(m);
        }


        [Route("post/new")]
        [Authorize("RequireAdmin")]
        [HttpPost]
        public async Task<IActionResult> PostNew(BlogPostEditViewModel blogPostChanges)
        {
            if (!ModelState.IsValid)
            {
                return View("PostEdit", blogPostChanges);
            }

            var postId = Guid.NewGuid().ToString();

            // Check to see if there are any base64 images in the content and, if so,
            // upload them to Azure Blob Storage and rewrite the content to reference the blob URLs.
            blogPostChanges.Content = await UploadAnyBase64Images(blogPostChanges.Content, postId);

            var blogPost = new BlogPost
            {
                PostId = postId,
                Title = blogPostChanges.Title,
                Content = blogPostChanges.Content,
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

            return View("PostEdit", blogPostChanges);
        }


        [Route("post/edit/{postId}")]
        [Authorize("RequireAdmin")]
        [HttpPost]
        public async Task<IActionResult> PostEdit(string postId, BlogPostEditViewModel blogPostChanges)
        {
            if (!ModelState.IsValid)
            {
                return View(blogPostChanges);
            }

            var bp = await _blogDbService.GetBlogPostAsync(postId);

            if (bp == null)
            {
                return View("PostNotFound");
            }

            // Check to see if there are any base64 images in the content and, if so,
            // upload them to Azure Blob Storage and rewrite the content to reference the blob URLs.
            blogPostChanges.Content = await UploadAnyBase64Images(blogPostChanges.Content, postId);

            bp.Title = blogPostChanges.Title;
            bp.Content = blogPostChanges.Content;

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
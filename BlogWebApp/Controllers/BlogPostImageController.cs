using System.Threading;
using System.Threading.Tasks;
using BlogWebApp.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace BlogWebApp.Controllers
{
    public class BlogPostImageController : Controller
    {
        private readonly ILogger<BlogController> _logger;
        private readonly IBlogCosmosDbService _blogDbService;
        private readonly IImageStorageManager _imageStorageManager;

        public BlogPostImageController(
            ILogger<BlogController> logger,
            IBlogCosmosDbService blogDbService,
            IImageStorageManager imageStorageManager)
        {
            _logger = logger;
            _blogDbService = blogDbService;
            _imageStorageManager = imageStorageManager;
        }

        [Route("img/post/{postId}/{filename}")]
        public async Task<IActionResult> PostView(string postId, string filename, CancellationToken ct)
        {
            var blobName = $"{postId}/{filename}";

            var blob = await _imageStorageManager.GetBlobAsStream("blog-post-images", blobName, ct);
            return File(blob.Value.Content, blob.Value.Details.ContentType);
        }
    }
}

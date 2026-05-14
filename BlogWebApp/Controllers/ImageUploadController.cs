using System;
using System.IO;
using System.Threading.Tasks;
using BlogWebApp.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace BlogWebApp.Controllers
{
    public class ImageUploadController : Controller
    {
        private readonly IImageStorageManager _imageStorage;

        public ImageUploadController(IImageStorageManager imageStorage)
        {
            _imageStorage = imageStorage;
        }

        public class UploadResponse
        {
            public string Url { get; set; } = string.Empty;
        }

        [Route("admin/image")]
        [HttpPost]
        [Authorize("RequireAdmin")]
        [IgnoreAntiforgeryToken]  // Admin-only + same-origin AJAX; cookie auth is the boundary.
        [RequestSizeLimit(10_000_000)]  // 10 MB
        public async Task<IActionResult> Upload(IFormFile file, string? postId = null)
        {
            if (file == null || file.Length == 0) return BadRequest("No file");

            var contentType = file.ContentType ?? "image/png";
            var ext = contentType switch
            {
                "image/png" => "png",
                "image/jpeg" => "jpg",
                "image/gif" => "gif",
                "image/webp" => "webp",
                _ => null,
            };
            if (ext == null) return BadRequest("Unsupported image type. Allowed: png, jpeg, gif, webp.");

            // Use the supplied postId as the blob folder, or "shared" if not yet assigned.
            // (Autosave hands us a postId on the first save; image upload may fire before that.)
            var folder = string.IsNullOrEmpty(postId) ? "shared" : postId;
            var blobName = $"{folder}/{Guid.NewGuid()}.{ext}";

            using var ms = new MemoryStream();
            await file.CopyToAsync(ms);

            await _imageStorage.UploadBlob("blog-post-images", blobName, contentType, ms.ToArray());

            return Json(new UploadResponse { Url = $"/img/post/{blobName}" });
        }
    }
}

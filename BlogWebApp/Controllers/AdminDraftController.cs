using System;
using System.Linq;
using System.Threading.Tasks;
using BlogWebApp.Models;
using BlogWebApp.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace BlogWebApp.Controllers
{
    public class AdminDraftController : Controller
    {
        private readonly IBlogCosmosDbService _blogDbService;

        public AdminDraftController(IBlogCosmosDbService blogDbService)
        {
            _blogDbService = blogDbService;
        }

        public class DraftSaveDto
        {
            public string? PostId { get; set; }
            public string PostType { get; set; } = "post";  // "post" | "note"
            public string Title { get; set; } = string.Empty;
            public string Content { get; set; } = string.Empty;
            public string? LinkUrl { get; set; }
            public string? Excerpt { get; set; }
            public string? CoverImageUrl { get; set; }
            public string Tags { get; set; } = string.Empty;  // comma-separated
        }

        public class DraftSaveResponse
        {
            public string PostId { get; set; } = string.Empty;
            public DateTime SavedAtUtc { get; set; }
        }

        [Route("admin/draft/save")]
        [HttpPost]
        [Authorize("RequireAdmin")]
        [IgnoreAntiforgeryToken]  // Autosave is fire-and-forget AJAX; admin-only.
        public async Task<IActionResult> Save([FromBody] DraftSaveDto dto)
        {
            if (dto == null) return BadRequest();

            // Either fetch the existing post (edit-mode autosave) or create a new one
            // (first autosave on a fresh PostNew form).
            BlogPost? bp = null;
            if (!string.IsNullOrEmpty(dto.PostId))
                bp = await _blogDbService.GetBlogPostAsync(dto.PostId);

            if (bp == null)
            {
                bp = new BlogPost
                {
                    PostId = string.IsNullOrEmpty(dto.PostId) ? Guid.NewGuid().ToString() : dto.PostId,
                    Type = dto.PostType,
                    Format = "markdown",
                    Status = "draft",
                    PublishedAtUtc = null,
                    AuthorId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value
                        ?? throw new InvalidOperationException("Authenticated user has no NameIdentifier claim."),
                    AuthorUsername = User.Identity?.Name
                        ?? throw new InvalidOperationException("Authenticated user has no name."),
                    DateCreated = DateTime.UtcNow,
                };
            }

            // Apply form state. Slug is NOT regenerated on autosave -- generated
            // only at the explicit publish moment so the public URL is stable.
            bp.Title = dto.Title;
            bp.Content = dto.Content;
            if (dto.PostType == "note")
                bp.LinkUrl = NoteController.SanitizeLinkUrl(dto.LinkUrl);
            bp.Excerpt = dto.Excerpt;
            bp.CoverImageUrl = dto.CoverImageUrl;
            bp.Tags = (dto.Tags ?? "")
                .Split(',', StringSplitOptions.RemoveEmptyEntries)
                .Select(t => t.Trim())
                .Where(t => !string.IsNullOrEmpty(t))
                .Take(12)
                .ToList();
            bp.DateUpdated = DateTime.UtcNow;

            // Autosave never publishes -- always Status=draft. Explicit "Publish" /
            // "Save changes" buttons go through PostNew/PostEdit POST which set Status
            // and PublishedAtUtc properly.
            // (Don't downgrade an already-published doc to draft, though.)
            if (bp.Status != "published") bp.Status = "draft";

            await _blogDbService.UpsertBlogPostAsync(bp);

            return Json(new DraftSaveResponse { PostId = bp.PostId, SavedAtUtc = bp.DateUpdated ?? DateTime.UtcNow });
        }
    }
}

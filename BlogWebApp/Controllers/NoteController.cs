using System;
using System.Linq;
using System.Threading.Tasks;
using BlogWebApp.Models;
using BlogWebApp.Services;
using BlogWebApp.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace BlogWebApp.Controllers
{
    public class NoteController : Controller
    {
        private readonly IBlogCosmosDbService _blogDbService;

        public NoteController(IBlogCosmosDbService blogDbService)
        {
            _blogDbService = blogDbService;
        }

        [Route("notes")]
        public async Task<IActionResult> Notes()
        {
            var notes = await _blogDbService.GetMostRecentByTypeAsync("note", 50);
            return View(notes);
        }

        [Route("notes/{slugOrId}")]
        public async Task<IActionResult> NoteView(string slugOrId)
        {
            // Try slug lookup first; fall back to id lookup.
            // (Notes without titles have empty slugs and are addressed by postId.)
            var note = await _blogDbService.GetBlogPostBySlugAsync("note", slugOrId);
            if (note == null)
            {
                note = await _blogDbService.GetBlogPostAsync(slugOrId);
                if (note == null || note.Type != "note") return View("NoteNotFound");

                // Public surface — hide drafts and future-scheduled. (GetBlogPostAsync is a
                // point-read that doesn't apply the public status/date filter; admin pages
                // rely on that, so we add the filter here on the public path.)
                if (note.Status != "published" || (note.PublishedAtUtc.HasValue && note.PublishedAtUtc.Value > DateTime.UtcNow))
                    return View("NoteNotFound");
            }

            return View(new NoteViewViewModel
            {
                PostId = note.PostId,
                Slug = note.Slug,
                Title = note.Title,
                Content = note.Content,
                Format = note.Format,
                Tags = note.Tags,
                LinkUrl = note.LinkUrl,
                AuthorId = note.AuthorId,
                AuthorUsername = note.AuthorUsername,
                DateCreated = note.DateCreated,
            });
        }

        [Route("admin/notes/new")]
        [Authorize("RequireAdmin")]
        public IActionResult NoteNew() => View("NoteEdit", new NoteEditViewModel());

        [Route("admin/notes/new")]
        [Authorize("RequireAdmin")]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> NoteNew(NoteEditViewModel m)
        {
            if (!ModelState.IsValid) return View("NoteEdit", m);

            var postId = Guid.NewGuid().ToString();
            var slug = string.IsNullOrWhiteSpace(m.Title)
                ? string.Empty
                : SlugGenerator.FromTitle(m.Title);

            if (!string.IsNullOrEmpty(slug)
                && await _blogDbService.GetBlogPostBySlugAsync("note", slug) != null)
            {
                slug = $"{slug}-{postId.Substring(0, 8)}";
            }

            var tags = (Request.Form["Tags"].ToString() ?? "")
                .Split(',', StringSplitOptions.RemoveEmptyEntries)
                .Select(t => t.Trim())
                .Where(t => !string.IsNullOrEmpty(t))
                .ToList();
            if (tags.Count > 12)
            {
                ModelState.AddModelError("Tags", "Maximum 12 tags per post.");
                return View("NoteEdit", m);
            }

            var note = new BlogPost
            {
                PostId = postId,
                Type = "note",
                Slug = slug,
                Format = "markdown",
                Status = m.Status,
                PublishedAtUtc = m.PublishedAtUtc
                                 ?? (m.Status == "published" ? DateTime.UtcNow : (DateTime?)null),
                Title = m.Title ?? string.Empty,
                Content = m.Content,
                LinkUrl = SanitizeLinkUrl(m.LinkUrl),
                Tags = tags,
                AuthorId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value
                    ?? throw new InvalidOperationException("Authenticated user has no NameIdentifier claim."),
                AuthorUsername = User.Identity?.Name
                    ?? throw new InvalidOperationException("Authenticated user has no name."),
                DateCreated = DateTime.UtcNow,
            };

            await _blogDbService.UpsertBlogPostAsync(note);
            ViewBag.Success = true;
            return View("NoteEdit", m);
        }

        [Route("admin/notes/edit/{postId}")]
        [Authorize("RequireAdmin")]
        public async Task<IActionResult> NoteEdit(string postId)
        {
            var note = await _blogDbService.GetBlogPostAsync(postId);
            if (note == null || note.Type != "note") return View("NoteNotFound");

            return View(new NoteEditViewModel
            {
                PostId = note.PostId,
                Title = note.Title,
                Content = note.Content,
                LinkUrl = note.LinkUrl,
                Status = note.Status,
                PublishedAtUtc = note.PublishedAtUtc,
                Tags = note.Tags,
            });
        }

        [Route("admin/notes/edit/{postId}")]
        [Authorize("RequireAdmin")]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> NoteEdit(string postId, NoteEditViewModel m)
        {
            if (!ModelState.IsValid) return View(m);

            var note = await _blogDbService.GetBlogPostAsync(postId);
            if (note == null || note.Type != "note") return View("NoteNotFound");

            var tags = (Request.Form["Tags"].ToString() ?? "")
                .Split(',', StringSplitOptions.RemoveEmptyEntries)
                .Select(t => t.Trim())
                .Where(t => !string.IsNullOrEmpty(t))
                .ToList();
            if (tags.Count > 12)
            {
                ModelState.AddModelError("Tags", "Maximum 12 tags per post.");
                return View(m);
            }

            // Preserve slug across edits (URL contract).
            note.Title = m.Title ?? string.Empty;
            note.Content = m.Content;
            note.LinkUrl = SanitizeLinkUrl(m.LinkUrl);
            note.Tags = tags;
            note.Status = m.Status;
            note.PublishedAtUtc = m.PublishedAtUtc
                                  ?? (note.Status == "published" && !note.PublishedAtUtc.HasValue ? DateTime.UtcNow : note.PublishedAtUtc);
            note.DateUpdated = DateTime.UtcNow;

            await _blogDbService.UpsertBlogPostAsync(note);
            ViewBag.Success = true;
            return View(m);
        }

        /// <summary>
        /// Returns the input URL if it's an absolute http/https/mailto URL,
        /// or null otherwise. The DataAnnotations <c>[Url]</c> attribute
        /// permits non-HTTP schemes (e.g. <c>javascript:alert(1)</c>) which
        /// would later render as an XSS payload inside a rendered link.
        /// </summary>
        private static string? SanitizeLinkUrl(string? linkUrl)
        {
            if (string.IsNullOrWhiteSpace(linkUrl)) return null;
            if (!Uri.TryCreate(linkUrl.Trim(), UriKind.Absolute, out var uri)) return null;
            return uri.Scheme is "http" or "https" or "mailto" ? uri.ToString() : null;
        }
    }
}

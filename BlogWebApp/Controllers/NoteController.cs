using System;
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
            }

            return View(new NoteViewViewModel
            {
                PostId = note.PostId,
                Slug = note.Slug,
                Title = note.Title,
                Content = note.Content,
                LinkUrl = note.LinkUrl,
                AuthorId = note.AuthorId,
                AuthorUsername = note.AuthorUsername,
                DateCreated = note.DateCreated,
            });
        }

        [Route("notes/new")]
        [Authorize("RequireAdmin")]
        public IActionResult NoteNew() => View("NoteEdit", new NoteEditViewModel());

        [Route("notes/new")]
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

            var note = new BlogPost
            {
                PostId = postId,
                Type = "note",
                Slug = slug,
                Title = m.Title ?? string.Empty,
                Content = m.Content,
                LinkUrl = string.IsNullOrWhiteSpace(m.LinkUrl) ? null : m.LinkUrl,
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

        [Route("notes/edit/{postId}")]
        [Authorize("RequireAdmin")]
        public async Task<IActionResult> NoteEdit(string postId)
        {
            var note = await _blogDbService.GetBlogPostAsync(postId);
            if (note == null || note.Type != "note") return View("NoteNotFound");

            return View(new NoteEditViewModel
            {
                Title = note.Title,
                Content = note.Content,
                LinkUrl = note.LinkUrl,
            });
        }

        [Route("notes/edit/{postId}")]
        [Authorize("RequireAdmin")]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> NoteEdit(string postId, NoteEditViewModel m)
        {
            if (!ModelState.IsValid) return View(m);

            var note = await _blogDbService.GetBlogPostAsync(postId);
            if (note == null || note.Type != "note") return View("NoteNotFound");

            // Preserve slug across edits (URL contract).
            note.Title = m.Title ?? string.Empty;
            note.Content = m.Content;
            note.LinkUrl = string.IsNullOrWhiteSpace(m.LinkUrl) ? null : m.LinkUrl;
            note.DateUpdated = DateTime.UtcNow;

            await _blogDbService.UpsertBlogPostAsync(note);
            ViewBag.Success = true;
            return View(m);
        }
    }
}

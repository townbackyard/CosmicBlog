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
    public class NowController : Controller
    {
        private readonly IBlogCosmosDbService _blogDbService;

        public NowController(IBlogCosmosDbService blogDbService)
        {
            _blogDbService = blogDbService;
        }

        [Route("now")]
        public async Task<IActionResult> Now()
        {
            var now = await _blogDbService.GetNowAsync();
            return View(now);
        }

        [Route("admin/now")]
        [Authorize("RequireAdmin")]
        public async Task<IActionResult> NowEdit()
        {
            var now = await _blogDbService.GetNowAsync();
            return View(new NowEditViewModel { Content = now?.Content ?? string.Empty });
        }

        [Route("admin/now")]
        [Authorize("RequireAdmin")]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> NowEdit(NowEditViewModel m)
        {
            if (!ModelState.IsValid) return View(m);

            var now = await _blogDbService.GetNowAsync() ?? new BlogPost
            {
                AuthorId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value
                    ?? throw new InvalidOperationException("Authenticated user has no NameIdentifier claim."),
                AuthorUsername = User.Identity?.Name
                    ?? throw new InvalidOperationException("Authenticated user has no name."),
                DateCreated = DateTime.UtcNow,
            };

            now.Content = m.Content;
            now.Format = "markdown";
            await _blogDbService.UpsertNowAsync(now);

            ViewBag.Success = true;
            return View(m);
        }
    }
}

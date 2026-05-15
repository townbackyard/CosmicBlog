using System.Linq;
using System.Threading.Tasks;
using BlogWebApp.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BlogWebApp.Controllers
{
    [Authorize("RequireAdmin")]
    public class AdminController : Controller
    {
        private readonly IBlogCosmosDbService _blogDbService;

        public AdminController(IBlogCosmosDbService blogDbService)
        {
            _blogDbService = blogDbService;
        }

        [Route("admin/posts")]
        public async Task<IActionResult> Posts(string? status = null)
        {
            var all = await _blogDbService.GetAllByTypeAsync("post", 200);
            if (!string.IsNullOrEmpty(status))
            {
                var nowUtc = System.DateTime.UtcNow;
                if (status == "scheduled")
                {
                    all = all.Where(p => p.Status == "published" && p.PublishedAtUtc.HasValue && p.PublishedAtUtc.Value > nowUtc).ToList();
                }
                else if (status == "published")
                {
                    // Currently-published = status=published AND publish-time has arrived.
                    // The "scheduled" filter is the complementary case.
                    all = all.Where(p => p.Status == "published" && (!p.PublishedAtUtc.HasValue || p.PublishedAtUtc.Value <= nowUtc)).ToList();
                }
                else
                {
                    all = all.Where(p => p.Status == status).ToList();
                }
            }
            ViewBag.StatusFilter = status;
            return View(all);
        }

        [Route("admin/notes")]
        public async Task<IActionResult> Notes(string? status = null)
        {
            var all = await _blogDbService.GetAllByTypeAsync("note", 200);
            if (!string.IsNullOrEmpty(status))
            {
                var nowUtc = System.DateTime.UtcNow;
                if (status == "scheduled")
                {
                    all = all.Where(p => p.Status == "published" && p.PublishedAtUtc.HasValue && p.PublishedAtUtc.Value > nowUtc).ToList();
                }
                else if (status == "published")
                {
                    // Currently-published = status=published AND publish-time has arrived.
                    // The "scheduled" filter is the complementary case.
                    all = all.Where(p => p.Status == "published" && (!p.PublishedAtUtc.HasValue || p.PublishedAtUtc.Value <= nowUtc)).ToList();
                }
                else
                {
                    all = all.Where(p => p.Status == status).ToList();
                }
            }
            ViewBag.StatusFilter = status;
            return View(all);
        }
    }
}

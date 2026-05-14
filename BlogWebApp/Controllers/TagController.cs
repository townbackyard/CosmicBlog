using System.Threading.Tasks;
using BlogWebApp.Services;
using Microsoft.AspNetCore.Mvc;

namespace BlogWebApp.Controllers
{
    public class TagController : Controller
    {
        private readonly IBlogCosmosDbService _blogDbService;

        public TagController(IBlogCosmosDbService blogDbService)
        {
            _blogDbService = blogDbService;
        }

        [Route("tag/{tag}")]
        public async Task<IActionResult> Tag(string tag)
        {
            var items = await _blogDbService.GetByTagAsync(tag, 100);
            ViewBag.Tag = tag;
            return View(items);
        }
    }
}

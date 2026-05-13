using System;
using System.Threading.Tasks;
using BlogWebApp.Models;
using BlogWebApp.Services;
using BlogWebApp.ViewModels;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace BlogWebApp.Controllers
{
    public class NewsletterController : Controller
    {
        private readonly IBlogCosmosDbService _blogDbService;
        private readonly ILogger<NewsletterController> _logger;

        public NewsletterController(IBlogCosmosDbService blogDbService, ILogger<NewsletterController> logger)
        {
            _blogDbService = blogDbService;
            _logger = logger;
        }

        [Route("newsletter")]
        public IActionResult Index() => View(new NewsletterSignupViewModel());

        [Route("newsletter")]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Index(NewsletterSignupViewModel m)
        {
            if (!string.IsNullOrWhiteSpace(m.Website))
            {
                _logger.LogInformation("Newsletter form rejected: honeypot tripped from {Ip}",
                    HttpContext.Connection.RemoteIpAddress);
                return View("Signed");  // silent confirmation -- don't reveal trap
            }

            if (!ModelState.IsValid) return View(m);

            var normalized = m.Email.Trim().ToLowerInvariant();
            var subscriber = new Subscriber
            {
                Id = normalized,
                Email = m.Email.Trim(),
                DateSubscribed = DateTime.UtcNow,
                Confirmed = false,
            };

            // Upsert -- re-signups are no-ops in v1 (idempotent).
            await _blogDbService.AddSubscriberAsync(subscriber);
            return View("Signed");
        }
    }
}

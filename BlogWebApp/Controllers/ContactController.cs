using System;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using BlogWebApp;
using BlogWebApp.Services;
using BlogWebApp.ViewModels;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace BlogWebApp.Controllers
{
    public class ContactController : Controller
    {
        private readonly IEmailSender _email;
        private readonly AppSettings _appSettings;
        private readonly ILogger<ContactController> _logger;

        public ContactController(
            IEmailSender email,
            IOptions<AppSettings> appSettings,
            ILogger<ContactController> logger)
        {
            _email = email;
            _appSettings = appSettings.Value;
            _logger = logger;
        }

        [Route("contact")]
        public IActionResult Index()
        {
            var m = new ContactFormViewModel
            {
                Subject = "Contact",
                FormLoadedAt = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
            };
            return View(m);
        }

        [Route("contact")]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Index(ContactFormViewModel m)
        {
            m.Message = (m.Message ?? string.Empty).Trim();
            m.Subject = string.IsNullOrWhiteSpace(m.Subject) ? "Contact" : m.Subject;

            // Only Message is required (matches reference policy)
            if (string.IsNullOrEmpty(m.Message))
            {
                ModelState.AddModelError(nameof(m.Message), "Message is required.");
                m.DisplayConfirmationMessage = false;
                return View(m);
            }

            // --- Spam detection: silent drop on every branch (do not reveal trap to attacker) ---

            // 1) Honeypot: hidden 'website' field is filled by bots
            if (!string.IsNullOrEmpty(m.Website))
            {
                _logger.LogInformation("Contact form: honeypot tripped from {Ip}",
                    HttpContext.Connection.RemoteIpAddress);
                m.DisplayConfirmationMessage = true;
                return View(m);
            }

            // 2) Time check: <3 seconds since GET = bot
            var nowUnix = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            if (m.FormLoadedAt > 0 && (nowUnix - m.FormLoadedAt) < 3)
            {
                _logger.LogInformation("Contact form: too-fast submission ({Elapsed}s) from {Ip}",
                    nowUnix - m.FormLoadedAt, HttpContext.Connection.RemoteIpAddress);
                m.DisplayConfirmationMessage = true;
                return View(m);
            }

            // 3) Heuristic spam filters (ported 1:1 from TownBackyard.Web.UI)
            bool isSpam = false;

            // Local-part with 3+ dots is a strong bot signal
            var atIdx = m.Email.IndexOf('@', StringComparison.Ordinal);
            if (atIdx > 0)
            {
                var local = m.Email.Substring(0, atIdx);
                if (local.Count(c => c == '.') >= 3) isSpam = true;
            }

            // Newline-injection in subject (CRLF header smuggling)
            if (m.Subject.Contains('\n') || m.Subject.Contains('\r')) isSpam = true;

            // Configured keyword filter (case-insensitive)
            var keywords = _appSettings.Contact.SpamKeywords ?? Array.Empty<string>();
            var upperMsg = m.Message.ToUpperInvariant();
            if (keywords.Any(kw => !string.IsNullOrEmpty(kw) && upperMsg.Contains(kw.ToUpperInvariant())))
            {
                isSpam = true;
            }

            if (isSpam)
            {
                _logger.LogInformation("Contact form: heuristic spam from {Ip}",
                    HttpContext.Connection.RemoteIpAddress);
                m.DisplayConfirmationMessage = true;
                return View(m);
            }

            // --- Legitimate message: send via ACS ---

            // Sanitize subject (strip CR/LF for the email header) and append timestamp like the reference
            var emailSubject = m.Subject.Replace("\n", " ").Replace("\r", " ") + " - " + DateTime.UtcNow.ToString("u");

            var bodyBuilder = new StringBuilder();
            if (!string.IsNullOrWhiteSpace(m.Name)) bodyBuilder.AppendLine($"Name: {m.Name}");
            if (!string.IsNullOrWhiteSpace(m.Email)) bodyBuilder.AppendLine($"Email: {m.Email}");
            if (!string.IsNullOrWhiteSpace(m.Phone)) bodyBuilder.AppendLine($"Phone: {m.Phone}");
            bodyBuilder.AppendLine();
            bodyBuilder.AppendLine(m.Message);

            // Tracing footer (matches reference)
            bodyBuilder.AppendLine();
            bodyBuilder.AppendLine();
            bodyBuilder.AppendLine($"{Request.Scheme}://{Request.Host}{Request.Path}{Request.QueryString}");
            var userAgent = Request.Headers.UserAgent.ToString();
            if (!string.IsNullOrEmpty(userAgent)) bodyBuilder.AppendLine(userAgent);

            try
            {
                await _email.SendAsync(
                    toAddress: _appSettings.Contact.ToEmail,
                    fromAddress: _appSettings.Contact.FromEmail,
                    subject: emailSubject,
                    plainTextBody: bodyBuilder.ToString(),
                    replyTo: string.IsNullOrWhiteSpace(m.Email) ? null : m.Email);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to send contact form email.");
                ModelState.AddModelError("", "Sorry, the message couldn't be sent. Please try again later.");
                m.FormLoadedAt = DateTimeOffset.UtcNow.ToUnixTimeSeconds();  // refresh timestamp for retry
                return View(m);
            }

            m.DisplayConfirmationMessage = true;
            return View(m);
        }
    }
}

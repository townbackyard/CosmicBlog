using System;
using System.IO;
using System.Linq;
using System.ServiceModel.Syndication;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using System.Xml;
using BlogWebApp;
using BlogWebApp.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace BlogWebApp.Controllers
{
    public class FeedController : Controller
    {
        private readonly IBlogCosmosDbService _blogDbService;
        private readonly IMarkdownRenderer _markdown;
        private readonly AppSettings _appSettings;

        public FeedController(IBlogCosmosDbService blogDbService, IMarkdownRenderer markdown, IOptions<AppSettings> appSettings)
        {
            _blogDbService = blogDbService;
            _markdown = markdown;
            _appSettings = appSettings.Value;
        }

        // Renders content to HTML for feed payloads (RSS/Atom/JSON Feed all want
        // pre-rendered HTML). Dispatches on Format like the view layer does.
        private string RenderToHtml(string content, string format)
        {
            if (string.IsNullOrEmpty(content)) return string.Empty;
            // IMarkdownRenderer returns IHtmlContent; convert to string via a writer.
            using var sw = new System.IO.StringWriter();
            _markdown.Render(content, format).WriteTo(sw, System.Text.Encodings.Web.HtmlEncoder.Default);
            return sw.ToString();
        }

        [Route("feed")]
        public async Task<IActionResult> Rss()
        {
            var feed = await BuildSyndicationFeedAsync();
            using var ms = new MemoryStream();
            using (var writer = XmlWriter.Create(ms, new XmlWriterSettings { Encoding = Encoding.UTF8, Indent = true, Async = true }))
            {
                var formatter = new Rss20FeedFormatter(feed);
                formatter.WriteTo(writer);
                writer.Flush();
            }
            return File(ms.ToArray(), "application/rss+xml; charset=utf-8");
        }

        [Route("feed.atom")]
        public async Task<IActionResult> Atom()
        {
            var feed = await BuildSyndicationFeedAsync();
            using var ms = new MemoryStream();
            using (var writer = XmlWriter.Create(ms, new XmlWriterSettings { Encoding = Encoding.UTF8, Indent = true, Async = true }))
            {
                var formatter = new Atom10FeedFormatter(feed);
                formatter.WriteTo(writer);
                writer.Flush();
            }
            return File(ms.ToArray(), "application/atom+xml; charset=utf-8");
        }

        [Route("feed.json")]
        public async Task<IActionResult> Json()
        {
            var posts = await _blogDbService.GetActivityFeedAsync(20);
            var site = _appSettings.SiteUrl.TrimEnd('/');

            var jsonFeed = new
            {
                version = "https://jsonfeed.org/version/1.1",
                title = _appSettings.BlogName,
                home_page_url = site,
                feed_url = $"{site}/feed.json",
                description = _appSettings.SiteDescription,
                authors = new[] { new { name = _appSettings.OwnerName, url = site } },
                items = posts.Select(p => new
                {
                    id = $"{site}/{ItemUrlPath(p.Type, p.Slug, p.PostId)}",
                    url = $"{site}/{ItemUrlPath(p.Type, p.Slug, p.PostId)}",
                    title = string.IsNullOrEmpty(p.Title) ? null : p.Title,
                    content_html = RenderToHtml(p.Content, p.Format),
                    external_url = p.LinkUrl,
                    date_published = p.DateCreated.ToString("o"),
                    tags = new[] { p.Type },  // "post" or "note" -- surfaces stream-type in clients that show tags
                }).ToArray(),
            };

            var json = JsonSerializer.Serialize(jsonFeed, new JsonSerializerOptions
            {
                WriteIndented = true,
                DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull,
            });
            return Content(json, "application/feed+json; charset=utf-8");
        }

        private async Task<SyndicationFeed> BuildSyndicationFeedAsync()
        {
            var posts = await _blogDbService.GetActivityFeedAsync(20);
            var site = _appSettings.SiteUrl.TrimEnd('/');

            var feed = new SyndicationFeed(
                _appSettings.BlogName,
                _appSettings.SiteDescription,
                new Uri(site));
            feed.Authors.Add(new SyndicationPerson(_appSettings.OwnerEmail, _appSettings.OwnerName, site));
            feed.Language = "en-us";
            feed.LastUpdatedTime = posts.Any() ? posts.Max(p => p.DateCreated) : DateTime.UtcNow;

            feed.Items = posts.Select(p =>
            {
                var url = $"{site}/{ItemUrlPath(p.Type, p.Slug, p.PostId)}";
                var item = new SyndicationItem(
                    title: string.IsNullOrEmpty(p.Title) ? (p.Type == "note" ? "Note" : "Untitled") : p.Title,
                    content: SyndicationContent.CreateHtmlContent(RenderToHtml(p.Content, p.Format)),
                    itemAlternateLink: new Uri(url),
                    id: url,
                    lastUpdatedTime: p.DateUpdated ?? p.DateCreated);
                item.PublishDate = p.DateCreated;
                item.Authors.Add(new SyndicationPerson(_appSettings.OwnerEmail, p.AuthorUsername, site));
                return item;
            }).ToList();

            return feed;
        }

        private static string ItemUrlPath(string type, string slug, string postId)
        {
            return type switch
            {
                "post" => $"posts/{slug}",
                "note" => $"notes/{(string.IsNullOrEmpty(slug) ? postId : slug)}",
                _ => "now",  // shouldn't happen in feed (GetActivityFeedAsync excludes "now"), but defensive
            };
        }
    }
}

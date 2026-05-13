using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml;
using BlogWebApp;
using BlogWebApp.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace BlogWebApp.Controllers
{
    public class SitemapController : Controller
    {
        private readonly IBlogCosmosDbService _blogDbService;
        private readonly AppSettings _appSettings;

        public SitemapController(IBlogCosmosDbService blogDbService, IOptions<AppSettings> appSettings)
        {
            _blogDbService = blogDbService;
            _appSettings = appSettings.Value;
        }

        [Route("sitemap.xml")]
        public async Task<IActionResult> Sitemap()
        {
            var site = _appSettings.SiteUrl.TrimEnd('/');
            var posts = await _blogDbService.GetMostRecentByTypeAsync("post", 500);
            var notes = await _blogDbService.GetMostRecentByTypeAsync("note", 500);

            using var ms = new MemoryStream();
            var settings = new XmlWriterSettings { Encoding = Encoding.UTF8, Indent = true, Async = true };
            using (var w = XmlWriter.Create(ms, settings))
            {
                w.WriteStartDocument();
                w.WriteStartElement("urlset", "http://www.sitemaps.org/schemas/sitemap/0.9");

                WriteUrl(w, $"{site}/", DateTime.UtcNow);
                WriteUrl(w, $"{site}/posts", DateTime.UtcNow);
                WriteUrl(w, $"{site}/notes", DateTime.UtcNow);
                WriteUrl(w, $"{site}/now", DateTime.UtcNow);
                WriteUrl(w, $"{site}/about", DateTime.UtcNow);
                WriteUrl(w, $"{site}/contact", DateTime.UtcNow);
                WriteUrl(w, $"{site}/newsletter", DateTime.UtcNow);

                foreach (var p in posts)
                    WriteUrl(w, $"{site}/posts/{p.Slug}", p.DateUpdated ?? p.DateCreated);

                foreach (var n in notes)
                {
                    var path = string.IsNullOrEmpty(n.Slug) ? n.PostId : n.Slug;
                    WriteUrl(w, $"{site}/notes/{path}", n.DateUpdated ?? n.DateCreated);
                }

                w.WriteEndElement();
                w.WriteEndDocument();
                w.Flush();
            }
            return File(ms.ToArray(), "application/xml; charset=utf-8");
        }

        private static void WriteUrl(XmlWriter w, string loc, DateTime lastmod)
        {
            w.WriteStartElement("url");
            w.WriteElementString("loc", loc);
            w.WriteElementString("lastmod", lastmod.ToString("yyyy-MM-dd"));
            w.WriteEndElement();
        }
    }
}


using System;
using System.Collections.Generic;

namespace BlogWebApp.ViewModels
{
    public class BlogPostViewViewModel : IOgContent
    {
        public string PostId { get; set; } = string.Empty;
        public string Slug { get; set; } = string.Empty;

        public string Title { get; set; } = string.Empty;

        public string Content { get; set; } = string.Empty;
        public string Format { get; set; } = "markdown";

        public string AuthorId { get; set; } = string.Empty;
        public string AuthorUsername { get; set; } = string.Empty;

        public DateTime DateCreated { get; set; }

        public List<string> Tags { get; set; } = new();

        public string? Excerpt { get; set; }

        // Prefer the admin-authored Excerpt for OG / JSON-LD descriptions when present.
        // Otherwise render Content through Markdig (if Format="markdown") so the strip
        // doesn't leak literal markdown syntax (**bold**, #headings, [text](url)) into
        // social previews.
        public string Description
        {
            get
            {
                if (!string.IsNullOrEmpty(Excerpt)) return Excerpt;
                var html = Format == "markdown" ? Markdig.Markdown.ToHtml(Content ?? string.Empty) : (Content ?? string.Empty);
                return html.StripHtml().Truncate(160);
            }
        }

        public string UrlPath => $"/posts/{Slug}";
    }
}

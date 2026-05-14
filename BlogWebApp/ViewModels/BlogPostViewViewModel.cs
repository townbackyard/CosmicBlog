
using System;

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

        public string Description => Content.StripHtml().Truncate(160);
        public string UrlPath => $"/posts/{Slug}";
    }
}

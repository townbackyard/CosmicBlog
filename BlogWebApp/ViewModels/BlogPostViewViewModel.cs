
using System;

namespace BlogWebApp.ViewModels
{
    public class BlogPostViewViewModel
    {
        public string PostId { get; set; } = string.Empty;
        public string Slug { get; set; } = string.Empty;

        public string Title { get; set; } = string.Empty;

        public string Content { get; set; } = string.Empty;

        public string AuthorId { get; set; } = string.Empty;
        public string AuthorUsername { get; set; } = string.Empty;

        public DateTime DateCreated { get; set; }
    }
}

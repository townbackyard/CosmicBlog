using System;
using System.Collections.Generic;

namespace BlogWebApp.ViewModels
{
    public class NoteViewViewModel : IOgContent
    {
        public string PostId { get; set; } = string.Empty;
        public string Slug { get; set; } = string.Empty;
        public string? Title { get; set; }
        public string Content { get; set; } = string.Empty;
        public string Format { get; set; } = "markdown";
        public string? LinkUrl { get; set; }
        public string AuthorId { get; set; } = string.Empty;
        public string AuthorUsername { get; set; } = string.Empty;
        public DateTime DateCreated { get; set; }

        public List<string> Tags { get; set; } = new();

        string IOgContent.Title => Title ?? string.Empty;  // explicit impl since the existing Title is nullable
        public string Description => Content.StripHtml().Truncate(160);
        public string UrlPath => $"/notes/{(string.IsNullOrEmpty(Slug) ? PostId : Slug)}";
    }
}

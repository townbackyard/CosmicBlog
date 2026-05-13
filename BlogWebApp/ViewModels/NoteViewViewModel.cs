using System;

namespace BlogWebApp.ViewModels
{
    public class NoteViewViewModel
    {
        public string PostId { get; set; } = string.Empty;
        public string Slug { get; set; } = string.Empty;
        public string? Title { get; set; }
        public string Content { get; set; } = string.Empty;
        public string? LinkUrl { get; set; }
        public string AuthorId { get; set; } = string.Empty;
        public string AuthorUsername { get; set; } = string.Empty;
        public DateTime DateCreated { get; set; }
    }
}

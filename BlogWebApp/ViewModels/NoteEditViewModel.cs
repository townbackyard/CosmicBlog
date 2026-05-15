using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace BlogWebApp.ViewModels
{
    public class NoteEditViewModel
    {
        public string? PostId { get; set; }

        [Display(Name = "Title (optional)")]
        public string? Title { get; set; }

        [Required]
        public string Content { get; set; } = string.Empty;

        [Url, Display(Name = "Link URL (optional)")]
        public string? LinkUrl { get; set; }

        public string Status { get; set; } = "draft";
        public DateTime? PublishedAtUtc { get; set; }
        public List<string> Tags { get; set; } = new();
    }
}

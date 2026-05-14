using BlogWebApp.Models;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Threading.Tasks;

namespace BlogWebApp.ViewModels
{
    public class BlogPostEditViewModel
    {
        public string? PostId { get; set; }
        public string Slug { get; set; } = string.Empty;


        [Required(AllowEmptyStrings = false)]
        public string Title { get; set; } = string.Empty;


        [Required(AllowEmptyStrings = false)]
        public string Content { get; set; } = string.Empty;

        public string Status { get; set; } = "draft";  // form default; "published" only on explicit action
        public DateTime? PublishedAtUtc { get; set; }  // null = publish-now; future = schedule
        public List<string> Tags { get; set; } = new();
        public string? Excerpt { get; set; }
        public string? CoverImageUrl { get; set; }
    }
}

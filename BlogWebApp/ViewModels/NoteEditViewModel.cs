using System.ComponentModel.DataAnnotations;

namespace BlogWebApp.ViewModels
{
    public class NoteEditViewModel
    {
        [Display(Name = "Title (optional)")]
        public string? Title { get; set; }

        [Required]
        public string Content { get; set; } = string.Empty;

        [Url, Display(Name = "Link URL (optional)")]
        public string? LinkUrl { get; set; }
    }
}

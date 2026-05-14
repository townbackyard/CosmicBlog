using System.ComponentModel.DataAnnotations;

namespace BlogWebApp.ViewModels
{
    public class NowEditViewModel
    {
        [Required]
        public string Content { get; set; } = string.Empty;
    }
}

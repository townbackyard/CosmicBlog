using System.ComponentModel.DataAnnotations;

namespace BlogWebApp.ViewModels
{
    public class NewsletterSignupViewModel
    {
        [Required, EmailAddress, StringLength(200)]
        public string Email { get; set; } = string.Empty;

        // Honeypot -- same pattern as Contact form
        public string? Website { get; set; }
    }
}

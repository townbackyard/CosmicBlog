using System;
using System.ComponentModel.DataAnnotations;

namespace BlogWebApp.ViewModels
{
    public class ContactFormViewModel
    {
        [StringLength(100)]
        public string Name { get; set; } = string.Empty;

        [EmailAddress, StringLength(200)]
        public string Email { get; set; } = string.Empty;

        [StringLength(40)]
        public string Phone { get; set; } = string.Empty;

        [StringLength(200)]
        public string Subject { get; set; } = "Contact";

        // Only Message is required (matches TownBackyard reference --
        // "We only require the message field, everything else is optional").
        [Required, StringLength(5000)]
        public string Message { get; set; } = string.Empty;

        // Render-control: when true, view shows the thank-you message instead of the form.
        public bool DisplayConfirmationMessage { get; set; }

        // Honeypot -- hidden from real users, bots fill it. Reference field name: "website".
        public string Website { get; set; } = string.Empty;

        // Time-check -- page load Unix seconds, injected on GET. POSTs <3 seconds later
        // are dropped. Reference uses DateTimeOffset.UtcNow.ToUnixTimeSeconds() -- match.
        public long FormLoadedAt { get; set; }
    }
}

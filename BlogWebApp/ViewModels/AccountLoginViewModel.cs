using System.ComponentModel.DataAnnotations;

namespace BlogWebApp.ViewModels
{
    public class AccountLoginViewModel
    {
        [Required, EmailAddress, Display(Name = "Email")]
        public string Email { get; set; } = string.Empty;

        [Required, DataType(DataType.Password), Display(Name = "Password")]
        public string Password { get; set; } = string.Empty;
    }
}

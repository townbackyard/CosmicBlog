using System.ComponentModel.DataAnnotations;

namespace BlogWebApp.ViewModels
{
    public class ChangePasswordViewModel
    {
        [Required, DataType(DataType.Password), Display(Name = "Current password")]
        public string CurrentPassword { get; set; } = string.Empty;

        [Required, DataType(DataType.Password), MinLength(12), Display(Name = "New password")]
        public string NewPassword { get; set; } = string.Empty;

        [Required, DataType(DataType.Password), Compare(nameof(NewPassword)), Display(Name = "Confirm new password")]
        public string ConfirmNewPassword { get; set; } = string.Empty;
    }
}

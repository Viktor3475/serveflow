using System.ComponentModel.DataAnnotations;

namespace ServeFlow.Web.Models;

public sealed class LoginModel
{
    [Required, EmailAddress, StringLength(256)]
    public string Email { get; set; } = string.Empty;
    [Required, DataType(DataType.Password), StringLength(128)]
    public string Password { get; set; } = string.Empty;
    [Display(Name = "Remember me")]
    public bool RememberMe { get; set; }
    public string? ReturnUrl { get; set; }
}

public sealed class ChangePasswordModel
{
    [Required, DataType(DataType.Password), StringLength(128), Display(Name = "Current password")]
    public string CurrentPassword { get; set; } = string.Empty;
    [Required, DataType(DataType.Password), StringLength(128, MinimumLength = 12), Display(Name = "New password")]
    public string NewPassword { get; set; } = string.Empty;
    [Required, DataType(DataType.Password), Compare(nameof(NewPassword)), Display(Name = "Confirm new password")]
    public string ConfirmPassword { get; set; } = string.Empty;
}

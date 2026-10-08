using System.ComponentModel.DataAnnotations;
using ServeFlow.Application.Security;

namespace ServeFlow.Web.Models;

public sealed class CreateEmployeeModel
{
    [Required, EmailAddress, StringLength(256)] public string Email { get; set; } = string.Empty;
    [Required, StringLength(160), Display(Name = "Display name")] public string DisplayName { get; set; } = string.Empty;
    [Required, DataType(DataType.Password), StringLength(128, MinimumLength = 12)] public string Password { get; set; } = string.Empty;
    [Required] public string Role { get; set; } = StaffRoles.Waiter;
}

public sealed class EditEmployeeModel
{
    public Guid Id { get; set; }
    [Required, StringLength(160), Display(Name = "Display name")] public string DisplayName { get; set; } = string.Empty;
    [Required] public string Role { get; set; } = StaffRoles.Waiter;
    [Display(Name = "Archive account (disable access)")] public bool IsArchived { get; set; }
    [Required, StringLength(64)] public string ConcurrencyStamp { get; set; } = string.Empty;
}

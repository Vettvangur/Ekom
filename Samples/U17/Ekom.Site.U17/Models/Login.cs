using System.ComponentModel.DataAnnotations;

namespace Ekom.Site.U17.Models;

public sealed class Login
{
    [Required]
    public string Username { get; set; } = "";

    [Required]
    public string Password { get; set; } = "";
}

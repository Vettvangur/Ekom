using System.ComponentModel.DataAnnotations;

namespace Ekom.Site.Models
{
    public class Register
    {
        [Required]
        public string Username { get; set; }
        [Required]
        public string Password { get; set; }
    }
}

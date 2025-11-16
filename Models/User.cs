using System.ComponentModel.DataAnnotations;

namespace Inventory_System.Models
{
    public class User
    {
        [Key]
        public int UserID { get; set; }

        [Required]
        public string FullName { get; set; }

        [Required]
        public string Username { get; set; }

        [Required]
        public string PasswordHash { get; set; }

        public string Role { get; set; } = "User";

        public string? UserType { get; set; }

        [Required]
        [EmailAddress]
        public string Email { get; set; }

        public string Status { get; set; } = "Pending";

        [Phone]
        [Display(Name = "Contact Number")]
        public string? ContactNumber { get; set; }

        [StringLength(255)]
        public string? Address { get; set; }

        public string? ProfilePicture { get; set; }
    }
}

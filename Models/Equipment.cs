using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Inventory_System.Models
{
    public class Equipment
    {
        [Key]
        public int EquipmentID { get; set; }

        [Required(ErrorMessage = "Item name is required.")]
        public string Name { get; set; }

        public string Description { get; set; }

        [Required(ErrorMessage = "Please select a type.")]
        public string Type { get; set; } // Office Supplies, ICT Equipment, etc.

        [Required(ErrorMessage = "Please select a label.")]
        public string Label { get; set; } // Borrowable / Non-Borrowable

        [Range(0, int.MaxValue, ErrorMessage = "Quantity cannot be negative.")]
        public int Quantity { get; set; }

        public string Status { get; set; } // Available / Borrowed / Out of Stock

        [Display(Name = "Item Image")]
        public string? ImagePath { get; set; } // stored file name or URL

        [NotMapped]
        [Display(Name = "Upload Image")]
        public IFormFile? ImageFile { get; set; } // for image upload (not stored in DB)
    }
}

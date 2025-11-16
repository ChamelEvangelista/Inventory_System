using Inventory_System.Models;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Inventory_System.Models
{
    public class BorrowRecord
    {
        [Key]
        public int BorrowID { get; set; }

        [ForeignKey("User")]
        public int UserID { get; set; }

        [ForeignKey("Equipment")]
        public int EquipmentID { get; set; }

        public int Quantity { get; set; }

        public string Purpose { get; set; }

        public DateTime BorrowDate { get; set; }

        public DateTime ExpectedReturnDate { get; set; }

        public DateTime? ReturnDate { get; set; }

        public string Status { get; set; } // Borrowed / Returned / Overdue

        // Navigation properties (optional but useful)
        public User User { get; set; }
        public Equipment Equipment { get; set; }
    }
}

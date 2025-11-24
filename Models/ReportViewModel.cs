using System.Collections.Generic;

namespace Inventory_System.Models
{
    public class ReportViewModel
    {
        // Summary Statistics
        public int TotalEquipment { get; set; }
        public int AvailableEquipment { get; set; }
        public int BorrowedEquipment { get; set; }
        public int OverdueEquipment { get; set; }
        public int ActiveUsers { get; set; }
        public int PendingUsers { get; set; }

        // Charts Data
        public Dictionary<string, int> BorrowedByUserType { get; set; } = new Dictionary<string, int>();
        public Dictionary<string, int> TopBorrowedEquipment { get; set; } = new Dictionary<string, int>();
        public Dictionary<string, int> EquipmentByType { get; set; } = new Dictionary<string, int>(); // NEW

        // Tables Data
        public List<OverdueRecordVM> OverdueRecords { get; set; } = new List<OverdueRecordVM>();
        public List<BorrowingRecordVM> BorrowingRecords { get; set; } = new List<BorrowingRecordVM>(); // NEW
    }

    // ViewModel for overdue records
    public class OverdueRecordVM
    {
        public int BorrowID { get; set; }
        public string Borrower { get; set; } = string.Empty;
        public string Equipment { get; set; } = string.Empty;
        public DateTime BorrowDate { get; set; }
        public DateTime ExpectedReturn { get; set; }
        public int DaysOverdue { get; set; }
    }

    // NEW ViewModel for borrowing records
    public class BorrowingRecordVM
    {
        public int BorrowID { get; set; }
        public string Borrower { get; set; } = string.Empty;
        public string Equipment { get; set; } = string.Empty;
        public int Quantity { get; set; }
        public DateTime BorrowDate { get; set; }
        public DateTime? ReturnDate { get; set; }
        public string Status { get; set; } = string.Empty;
    }
}
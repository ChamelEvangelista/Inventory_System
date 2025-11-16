using System;
using System.Collections.Generic;

namespace Inventory_System.Models
{
    public class ReportViewModel
    {
        // Summary
        public int TotalEquipment { get; set; }
        public int AvailableEquipment { get; set; }
        public int BorrowedEquipment { get; set; }
        public int OverdueEquipment { get; set; }
        public int ActiveUsers { get; set; }
        public int PendingUsers { get; set; }

        // Grouped / Chart Data
        public Dictionary<string, int> BorrowedByUserType { get; set; } = new();
        public Dictionary<string, int> TopBorrowedEquipment { get; set; } = new();
        public Dictionary<string, int> BorrowActivity { get; set; } = new();

        // Lists
        public List<OverdueRecordVM> OverdueRecords { get; set; } = new();
    }

    public class OverdueRecordVM
    {
        public int BorrowID { get; set; }
        public string Borrower { get; set; }
        public string Equipment { get; set; }
        public DateTime BorrowDate { get; set; }
        public DateTime ExpectedReturn { get; set; }
        public int DaysOverdue { get; set; }
    }
}

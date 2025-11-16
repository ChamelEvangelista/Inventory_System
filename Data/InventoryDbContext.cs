using Inventory_System.Models;
using Microsoft.EntityFrameworkCore;

namespace Inventory_System.Data
{
    public class InventoryDbContext : DbContext
    {
        public InventoryDbContext(DbContextOptions<InventoryDbContext> options) : base(options) { }

        public DbSet<User> Users { get; set; }
        public DbSet<Equipment> Equipment { get; set; }
        public DbSet<BorrowRecord> BorrowRecords { get; set; }
    }
}

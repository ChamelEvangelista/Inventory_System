using Inventory_System.Data;
using Inventory_System.Models;
using iTextSharp.text;
using iTextSharp.text.pdf;
using Microsoft.AspNetCore.Hosting; // ADD THIS
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using OfficeOpenXml;
using OfficeOpenXml.Style;
using System;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace Inventory_System.Controllers
{
    public class AdminController : Controller
    {
        private readonly InventoryDbContext _context;
        private readonly IWebHostEnvironment _webHostEnvironment; // ADD THIS

        // UPDATED CONSTRUCTOR
        public AdminController(InventoryDbContext context, IWebHostEnvironment webHostEnvironment)
        {
            _context = context;
            _webHostEnvironment = webHostEnvironment; // ADD THIS
        }

        // =========================
        // DASHBOARD
        // =========================
        public IActionResult AdminDashboard()
        {
            if (HttpContext.Session.GetString("Role") != "Admin")
                return RedirectToAction("AccessDenied", "Account");

            // Existing counts
            ViewBag.PendingUsers = _context.Users.Count(u => u.Status == "Pending");
            ViewBag.TotalEquipment = _context.Equipment.Count();
            ViewBag.BorrowedItems = _context.BorrowRecords.Count(b => b.Status == "Borrowed");
            ViewBag.ReturnedItems = _context.BorrowRecords.Count(b => b.Status == "Returned");
            ViewBag.BorrowableCount = _context.Equipment.Count(e => e.Label == "Borrowable");
            ViewBag.NonBorrowableCount = _context.Equipment.Count(e => e.Label == "Non-Borrowable");

            // NEW: Recent Activities - Mixed from all sources
            var recentActivities = new List<dynamic>();

            // 1. Get recent borrow/return activities
            var recentBorrowRecords = _context.BorrowRecords
                .Include(b => b.User)
                .Include(b => b.Equipment)
                .OrderByDescending(b => b.BorrowDate)
                .Take(3)
                .ToList();

            foreach (var record in recentBorrowRecords)
            {
                if (record.Status == "Borrowed")
                {
                    recentActivities.Add(new
                    {
                        Type = "Borrowed",
                        Title = "Equipment Borrowed",
                        Description = $"{record.User?.FullName ?? "Unknown User"} borrowed {record.Equipment?.Name ?? "Unknown Equipment"}",
                        Details = $"Quantity: {record.Quantity} | Purpose: {record.Purpose}",
                        Timestamp = record.BorrowDate
                    });
                }
                else if (record.Status == "Returned" && record.ReturnDate.HasValue)
                {
                    var isOnTime = record.ReturnDate <= record.ExpectedReturnDate;
                    recentActivities.Add(new
                    {
                        Type = "Returned",
                        Title = "Equipment Returned",
                        Description = $"{record.User?.FullName ?? "Unknown User"} returned {record.Equipment?.Name ?? "Unknown Equipment"}",
                        Details = $"{(isOnTime ? "✓ On time" : "⚠ Late return")} | Quantity: {record.Quantity}",
                        Timestamp = record.ReturnDate.Value
                    });
                }
            }

            // 2. Get recent user registrations
            var recentUsers = _context.Users
                .Where(u => u.Role != "Admin")
                .OrderByDescending(u => u.UserID)
                .Take(2)
                .ToList();

            foreach (var user in recentUsers)
            {
                recentActivities.Add(new
                {
                    Type = "UserRegistration",
                    Title = "New User Registration",
                    Description = $"{user.FullName} registered as {user.UserType}",
                    Details = $"Status: {user.Status}",
                    Timestamp = DateTime.Now // Use current time for recent registrations
                });
            }

            // 3. Get recent equipment additions
            var recentEquipment = _context.Equipment
                .OrderByDescending(e => e.EquipmentID)
                .Take(2)
                .ToList();

            foreach (var equipment in recentEquipment)
            {
                recentActivities.Add(new
                {
                    Type = "EquipmentAdded",
                    Title = "Equipment Added",
                    Description = $"New equipment '{equipment.Name}' added to inventory",
                    Details = $"Type: {equipment.Type} | Quantity: {equipment.Quantity}",
                    Timestamp = DateTime.Now // Use current time for recent additions
                });
            }

            // 4. Get recent user approvals
            var recentApprovals = _context.Users
                .Where(u => u.Status == "Active" && u.Role != "Admin")
                .OrderByDescending(u => u.UserID)
                .Take(2)
                .ToList();

            foreach (var user in recentApprovals)
            {
                recentActivities.Add(new
                {
                    Type = "UserApproval",
                    Title = "User Approved",
                    Description = $"{user.FullName} was approved",
                    Details = $"User Type: {user.UserType}",
                    Timestamp = DateTime.Now // Use current time for recent approvals
                });
            }

            // 5. Get recent equipment updates
            var recentUpdates = _context.Equipment
                .OrderByDescending(e => e.EquipmentID)
                .Take(1)
                .ToList();

            foreach (var equipment in recentUpdates)
            {
                recentActivities.Add(new
                {
                    Type = "EquipmentUpdated",
                    Title = "Equipment Updated",
                    Description = $"'{equipment.Name}' was updated",
                    Details = $"Status: {equipment.Status} | Quantity: {equipment.Quantity}",
                    Timestamp = DateTime.Now // Use current time for recent updates
                });
            }

            // Sort ALL activities by ID (as proxy for recentness) and take the 5 most recent
            ViewBag.RecentActivities = recentActivities
                .OrderByDescending(a => a.Timestamp)
                .Take(5)
                .ToList();

            // Equipment Distribution Chart Data (existing)
            var equipmentTypes = _context.Equipment
                .GroupBy(e => e.Type)
                .Select(g => new { Type = g.Key, Count = g.Count() })
                .ToList();

            ViewBag.EquipmentTypeLabels = equipmentTypes.Select(et => et.Type).ToList();
            ViewBag.EquipmentTypeCounts = equipmentTypes.Select(et => et.Count).ToList();

            return View();
        }

        // =========================
        // MANAGE USERS
        // =========================
        public IActionResult ManageUsers()
        {
            if (HttpContext.Session.GetString("Role") != "Admin")
                return RedirectToAction("AccessDenied", "Account");

            var users = _context.Users
                .Where(u => u.Role != "Admin")
                .OrderBy(u => u.Status)
                .ToList();

            return View(users);
        }

        public IActionResult ApproveUser(int id)
        {
            var user = _context.Users.Find(id);
            if (user != null)
            {
                user.Status = "Active";
                _context.SaveChanges();
            }
            return RedirectToAction("ManageUsers");
        }

        public IActionResult RejectUser(int id)
        {
            var user = _context.Users.Find(id);
            if (user != null)
            {
                user.Status = "Rejected";
                _context.SaveChanges();
            }
            return RedirectToAction("ManageUsers");
        }

        [HttpGet]
        public IActionResult EditUser(int id)
        {
            if (HttpContext.Session.GetString("Role") != "Admin")
                return RedirectToAction("AccessDenied", "Account");

            var user = _context.Users.FirstOrDefault(u => u.UserID == id);
            if (user == null) return NotFound();

            return View(user);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult EditUser(User updatedUser)
        {
            if (HttpContext.Session.GetString("Role") != "Admin")
                return RedirectToAction("AccessDenied", "Account");

            if (ModelState.IsValid)
            {
                var existingUser = _context.Users.Find(updatedUser.UserID);
                if (existingUser != null)
                {
                    existingUser.FullName = updatedUser.FullName;
                    existingUser.Username = updatedUser.Username;
                    existingUser.Role = updatedUser.Role;
                    existingUser.UserType = updatedUser.UserType;
                    existingUser.Status = updatedUser.Status;
                    _context.SaveChanges();

                    TempData["SuccessMessage"] = "User updated successfully!";
                }
                return RedirectToAction("ManageUsers");
            }

            return View(updatedUser);
        }

        public IActionResult DeleteUser(int id)
        {
            if (HttpContext.Session.GetString("Role") != "Admin")
                return RedirectToAction("AccessDenied", "Account");

            var user = _context.Users.Find(id);
            if (user == null) return NotFound();

            if (_context.BorrowRecords.Any(r => r.UserID == id))
            {
                TempData["ErrorMessage"] = "User cannot be deleted because they have existing borrow records.";
                return RedirectToAction("ManageUsers");
            }

            _context.Users.Remove(user);
            _context.SaveChanges();

            TempData["SuccessMessage"] = "User deleted successfully!";
            return RedirectToAction("ManageUsers");
        }

        // =========================
        // MANAGE EQUIPMENT / ITEMS
        // =========================
        public IActionResult ManageEquipment(string searchQuery)
        {
            if (HttpContext.Session.GetString("Role") != "Admin")
                return RedirectToAction("AccessDenied", "Account");

            var equipmentList = _context.Equipment.AsQueryable();

            if (!string.IsNullOrEmpty(searchQuery))
            {
                searchQuery = searchQuery.ToLower();
                equipmentList = equipmentList.Where(e =>
                    e.Name.ToLower().Contains(searchQuery) ||
                    e.Label.ToLower().Contains(searchQuery)
                );
            }

            ViewBag.SearchQuery = searchQuery;
            return View(equipmentList.ToList());
        }

        public IActionResult EquipmentList(string searchTerm, string typeFilter, string labelFilter, string statusFilter)
        {
            if (HttpContext.Session.GetString("Role") != "Admin")
                return RedirectToAction("AccessDenied", "Account");

            var query = _context.Equipment.AsQueryable();

            if (!string.IsNullOrEmpty(searchTerm))
                query = query.Where(e => e.Name.Contains(searchTerm) || e.Description.Contains(searchTerm));
            if (!string.IsNullOrEmpty(typeFilter))
                query = query.Where(e => e.Type == typeFilter);
            if (!string.IsNullOrEmpty(labelFilter))
                query = query.Where(e => e.Label == labelFilter);
            if (!string.IsNullOrEmpty(statusFilter))
                query = query.Where(e => e.Status == statusFilter);

            ViewBag.Types = _context.Equipment.Select(e => e.Type).Distinct().ToList();
            ViewBag.Labels = _context.Equipment.Select(e => e.Label).Distinct().ToList();

            ViewBag.ActiveFilter = new { Search = searchTerm, Type = typeFilter, Label = labelFilter, Status = statusFilter };

            // Get active borrow records to determine borrowed equipment
            var activeBorrowRecords = _context.BorrowRecords
                .Where(b => b.Status == "Borrowed")
                .Select(b => b.EquipmentID)
                .Distinct()
                .ToList();

            ViewBag.ActiveBorrowRecords = activeBorrowRecords;

            return View(query.ToList());
        }

        [HttpGet]
        public IActionResult AddEquipment()//MAO NI ANG CRUD(CREATE NI)
        {
            if (HttpContext.Session.GetString("Role") != "Admin")
                return RedirectToAction("AccessDenied", "Account");

            LoadDropdowns();
            return View();
        }

        // UPDATED ADD EQUIPMENT WITH IMAGE UPLOAD
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AddEquipment(Equipment equipment, IFormFile ImageFile)
        {
            try
            {
                // Handle image upload
                if (ImageFile != null && ImageFile.Length > 0)
                {
                    // Validate file size (5MB max)
                    if (ImageFile.Length > 5 * 1024 * 1024)
                    {
                        TempData["ErrorMessage"] = "File size must be less than 5MB";
                        LoadDropdowns();
                        return View(equipment);
                    }

                    // Validate file type
                    var allowedExtensions = new[] { ".jpg", ".jpeg", ".png", ".gif" };
                    var fileExtension = Path.GetExtension(ImageFile.FileName).ToLower();

                    if (!allowedExtensions.Contains(fileExtension))
                    {
                        TempData["ErrorMessage"] = "Only JPG, PNG, and GIF files are allowed";
                        LoadDropdowns();
                        return View(equipment);
                    }

                    // Generate unique filename
                    var uniqueFileName = $"{Guid.NewGuid()}_{Path.GetFileName(ImageFile.FileName)}";

                    // Define upload path
                    var uploadsFolder = Path.Combine(_webHostEnvironment.WebRootPath, "images", "equipment");

                    // Create directory if it doesn't exist
                    if (!Directory.Exists(uploadsFolder))
                    {
                        Directory.CreateDirectory(uploadsFolder);
                    }

                    var filePath = Path.Combine(uploadsFolder, uniqueFileName);

                    // Save file to server
                    using (var fileStream = new FileStream(filePath, FileMode.Create))
                    {
                        await ImageFile.CopyToAsync(fileStream);
                    }

                    // Store filename in database
                    equipment.ImagePath = uniqueFileName;
                }
                else
                {
                    // No image uploaded, use default
                    equipment.ImagePath = "no-image.png";
                }

                // Set status based on quantity
                equipment.Status = equipment.Quantity > 0 ? "Available" : "Out of Stock";

                // Add to database
                _context.Equipment.Add(equipment);
                await _context.SaveChangesAsync();

                TempData["SuccessMessage"] = $"Equipment '{equipment.Name}' added successfully!";
                return RedirectToAction("ManageEquipment");
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = $"Error adding equipment: {ex.Message}";
                LoadDropdowns();
                return View(equipment);
            }
        }

        [HttpGet]
        public IActionResult EditEquipment(int id)
        {
            // Check if ID is valid
            if (id <= 0)
            {
                TempData["ErrorMessage"] = "Invalid equipment ID.";
                return RedirectToAction("EquipmentList");
            }

            // Get equipment from database
            var equipment = _context.Equipment
                .FirstOrDefault(e => e.EquipmentID == id);

            // Check if equipment exists
            if (equipment == null)
            {
                TempData["ErrorMessage"] = "Equipment not found.";
                return RedirectToAction("EquipmentList");
            }

            // Populate ViewBag with required data
            ViewBag.Types = new string[]
            {
        "Office Supplies",
        "Cleaning Supplies",
        "ICT Equipment",
        "Office Equipment",
        "Audio-Visual Equipment",
        "Sports Equipment",
        "Maintenance Tools"
            };

            ViewBag.Labels = new string[] { "Borrowable", "Non-Borrowable" };

            return View(equipment);
        }

        // UPDATED EDIT EQUIPMENT WITH IMAGE UPLOAD
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> EditEquipment(Equipment equipment, IFormFile ImageFile)
        {
            try
            {
                // Get existing equipment from database
                var existingEquipment = await _context.Equipment.FindAsync(equipment.EquipmentID);

                if (existingEquipment == null)
                {
                    TempData["ErrorMessage"] = "Equipment not found";
                    return RedirectToAction("ManageEquipment");
                }

                // Handle new image upload
                if (ImageFile != null && ImageFile.Length > 0)
                {
                    // Validate file size (5MB max)
                    if (ImageFile.Length > 5 * 1024 * 1024)
                    {
                        TempData["ErrorMessage"] = "File size must be less than 5MB";
                        LoadDropdowns();
                        return View(equipment);
                    }

                    // Validate file type
                    var allowedExtensions = new[] { ".jpg", ".jpeg", ".png", ".gif" };
                    var fileExtension = Path.GetExtension(ImageFile.FileName).ToLower();

                    if (!allowedExtensions.Contains(fileExtension))
                    {
                        TempData["ErrorMessage"] = "Only JPG, PNG, and GIF files are allowed";
                        LoadDropdowns();
                        return View(equipment);
                    }

                    // Delete old image if it exists and is not the default
                    if (!string.IsNullOrEmpty(existingEquipment.ImagePath) &&
                        existingEquipment.ImagePath != "no-image.png")
                    {
                        var oldImagePath = Path.Combine(_webHostEnvironment.WebRootPath, "images", "equipment", existingEquipment.ImagePath);
                        if (System.IO.File.Exists(oldImagePath))
                        {
                            System.IO.File.Delete(oldImagePath);
                        }
                    }

                    // Generate unique filename for new image
                    var uniqueFileName = $"{Guid.NewGuid()}_{Path.GetFileName(ImageFile.FileName)}";

                    // Define upload path
                    var uploadsFolder = Path.Combine(_webHostEnvironment.WebRootPath, "images", "equipment");

                    // Create directory if it doesn't exist
                    if (!Directory.Exists(uploadsFolder))
                    {
                        Directory.CreateDirectory(uploadsFolder);
                    }

                    var filePath = Path.Combine(uploadsFolder, uniqueFileName);

                    // Save new file to server
                    using (var fileStream = new FileStream(filePath, FileMode.Create))
                    {
                        await ImageFile.CopyToAsync(fileStream);
                    }

                    // Update image path
                    existingEquipment.ImagePath = uniqueFileName;
                }
                // If no new image uploaded, keep the existing image path

                // Update equipment details
                existingEquipment.Name = equipment.Name;
                existingEquipment.Description = equipment.Description;
                existingEquipment.Type = equipment.Type;
                existingEquipment.Label = equipment.Label;
                existingEquipment.Quantity = equipment.Quantity;
                existingEquipment.Status = equipment.Status;

                // Save changes
                _context.Equipment.Update(existingEquipment);
                await _context.SaveChangesAsync();

                TempData["SuccessMessage"] = $"Equipment '{equipment.Name}' updated successfully!";
                return RedirectToAction("ManageEquipment");
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = $"Error updating equipment: {ex.Message}";
                LoadDropdowns();
                return View(equipment);
            }
        }

        [HttpGet]
        public IActionResult GetEquipmentUpdates()
        {
            try
            {
                var equipment = _context.Equipment.ToList();

                // Get all active borrow records to determine which equipment is currently borrowed
                var activeBorrowRecords = _context.BorrowRecords
                    .Where(b => b.Status == "Borrowed")
                    .GroupBy(b => b.EquipmentID)
                    .Select(g => new { EquipmentID = g.Key, BorrowedCount = g.Count() })
                    .ToList();

                // Calculate counters based on the new logic
                int availableCount = equipment.Count(e => e.Quantity > 0 && e.Label == "Borrowable");
                int borrowedCount = activeBorrowRecords.Count; // Count of equipment with active borrows
                int outOfStockCount = equipment.Count(e => e.Quantity == 0);

                var equipmentData = equipment.Select(e => new
                {
                    equipmentID = e.EquipmentID,
                    quantity = e.Quantity,
                    status = e.Status,
                    label = e.Label,
                    isBorrowed = activeBorrowRecords.Any(b => b.EquipmentID == e.EquipmentID) // Add this flag
                }).ToList();

                return Json(new
                {
                    success = true,
                    equipment = equipmentData,
                    counters = new
                    {
                        availableCount,
                        borrowedCount,
                        outOfStockCount
                    }
                });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = ex.Message });
            }
        }

        // UPDATED DELETE EQUIPMENT WITH IMAGE DELETION
        public async Task<IActionResult> DeleteEquipment(int id)
        {
            try
            {
                var item = await _context.Equipment.FindAsync(id);

                if (item == null)
                {
                    TempData["ErrorMessage"] = "Equipment not found";
                    return RedirectToAction("ManageEquipment");
                }

                // Delete image file if it exists and is not the default
                if (!string.IsNullOrEmpty(item.ImagePath) && item.ImagePath != "no-image.png")
                {
                    var imagePath = Path.Combine(_webHostEnvironment.WebRootPath, "images", "equipment", item.ImagePath);
                    if (System.IO.File.Exists(imagePath))
                    {
                        System.IO.File.Delete(imagePath);
                    }
                }

                // Delete equipment from database
                _context.Equipment.Remove(item);
                await _context.SaveChangesAsync();

                TempData["SuccessMessage"] = $"Equipment '{item.Name}' deleted successfully!";
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = $"Error deleting equipment: {ex.Message}";
            }

            return RedirectToAction("ManageEquipment");
        }

        public IActionResult Reports()
        {
            if (HttpContext.Session.GetString("Role") != "Admin")
                return RedirectToAction("AccessDenied", "Account");

            // Summary statistics - FIXED LOGIC
            var totalEquipment = _context.Equipment.Count();

            // Available equipment: items with quantity > 0
            var availableEquipment = _context.Equipment.Count(e => e.Quantity > 0);

            // Borrowed equipment: count of active borrow records
            var borrowedEquipment = _context.BorrowRecords.Count(b => b.Status == "Borrowed");

            // Overdue equipment: borrowed items past expected return date
            var overdueEquipment = _context.BorrowRecords
                .Count(b => b.Status == "Borrowed" && b.ExpectedReturnDate < DateTime.Now);

            var activeUsers = _context.Users.Count(u => u.Status == "Active");
            var pendingUsers = _context.Users.Count(u => u.Status == "Pending");

            // NEW: Equipment Quantity by Type (for bar chart)
            var equipmentByType = _context.Equipment
                .GroupBy(e => e.Type)
                .Select(g => new { Type = g.Key, TotalQuantity = g.Sum(e => e.Quantity) })
                .ToDictionary(x => x.Type ?? "Unknown", x => x.TotalQuantity);

            // FIXED: Top 5 most borrowed equipment - count by borrow records, not quantity
            var topBorrowedEquipment = _context.BorrowRecords
                .Include(b => b.Equipment)
                .GroupBy(b => b.Equipment.Name)
                .Select(g => new {
                    EquipmentName = g.Key,
                    TimesBorrowed = g.Count()  // Count how many times it was borrowed
                })
                .OrderByDescending(x => x.TimesBorrowed)
                .Take(5)
                .ToDictionary(x => x.EquipmentName ?? "Unknown Equipment", x => x.TimesBorrowed);

            // NEW: Recent borrowing records (instead of overdue)
            var borrowingRecords = _context.BorrowRecords
                .Include(b => b.User)
                .Include(b => b.Equipment)
                .OrderByDescending(b => b.BorrowDate)
                .Take(20) // Show last 20 records
                .Select(b => new BorrowingRecordVM
                {
                    BorrowID = b.BorrowID,
                    Borrower = b.User != null ? b.User.FullName : "Unknown User",
                    Equipment = b.Equipment != null ? b.Equipment.Name : "Unknown Equipment",
                    Quantity = b.Quantity,
                    BorrowDate = b.BorrowDate,
                    ReturnDate = b.ReturnDate,
                    Status = b.Status
                })
                .ToList();

            // Keep overdue records for the card counter, but don't display the table
            var overdueRecords = _context.BorrowRecords
                .Where(b => b.Status == "Borrowed" && b.ExpectedReturnDate < DateTime.Now)
                .AsEnumerable()
                .Select(b => new OverdueRecordVM
                {
                    BorrowID = b.BorrowID,
                    Borrower = b.User != null ? b.User.FullName : "Unknown User",
                    Equipment = b.Equipment != null ? b.Equipment.Name : "Unknown Equipment",
                    BorrowDate = b.BorrowDate,
                    ExpectedReturn = b.ExpectedReturnDate,
                    DaysOverdue = (DateTime.Now - b.ExpectedReturnDate).Days
                })
                .ToList();

            // Building the View Model
            var viewModel = new ReportViewModel
            {
                TotalEquipment = totalEquipment,
                AvailableEquipment = availableEquipment,
                BorrowedEquipment = borrowedEquipment,
                OverdueEquipment = overdueEquipment,
                ActiveUsers = activeUsers,
                PendingUsers = pendingUsers,

                EquipmentByType = equipmentByType,

                TopBorrowedEquipment = topBorrowedEquipment,

                BorrowingRecords = borrowingRecords,
                OverdueRecords = overdueRecords
            };

            return View(viewModel);
        }

        // =========================
        // EXPORT TO PDF (iTextSharp)
        // =========================
        [HttpGet]
        public IActionResult ExportToPDF()
        {
            var borrowRecords = _context.BorrowRecords
                .Select(b => new
                {
                    b.BorrowID,
                    Borrower = b.User.FullName,
                    UserType = b.User.UserType,
                    Equipment = b.Equipment.Name,
                    b.Quantity,
                    b.BorrowDate,
                    b.ExpectedReturnDate,
                    b.Status
                })
                .ToList();

            using (var stream = new MemoryStream())
            {
                var doc = new Document(PageSize.A4, 40, 40, 40, 40);
                PdfWriter.GetInstance(doc, stream);
                doc.Open();

                var titleFont = FontFactory.GetFont(FontFactory.HELVETICA_BOLD, 14);
                var headerFont = FontFactory.GetFont(FontFactory.HELVETICA_BOLD, 10, BaseColor.WHITE);
                var normalFont = FontFactory.GetFont(FontFactory.HELVETICA, 10);

                // Title
                var title = new Paragraph("Technology Equipment Inventory System - Borrowing Report", titleFont)
                {
                    Alignment = Element.ALIGN_CENTER,
                    SpacingAfter = 20f
                };
                doc.Add(title);

                // Table
                PdfPTable table = new PdfPTable(8);
                table.WidthPercentage = 100;
                table.SetWidths(new float[] { 1.2f, 2.5f, 2.0f, 2.5f, 1.2f, 2.0f, 2.0f, 1.5f });

                string[] headers = { "Borrow ID", "Borrower", "User Type", "Equipment", "Qty", "Borrow Date", "Expected Return", "Status" };
                foreach (var header in headers)
                {
                    var cell = new PdfPCell(new Phrase(header, headerFont))
                    {
                        BackgroundColor = new BaseColor(0, 102, 204),
                        HorizontalAlignment = Element.ALIGN_CENTER,
                        Padding = 5
                    };
                    table.AddCell(cell);
                }

                foreach (var record in borrowRecords)
                {
                    table.AddCell(new Phrase(record.BorrowID.ToString(), normalFont));
                    table.AddCell(new Phrase(record.Borrower, normalFont));
                    table.AddCell(new Phrase(record.UserType, normalFont));
                    table.AddCell(new Phrase(record.Equipment, normalFont));
                    table.AddCell(new Phrase(record.Quantity.ToString(), normalFont));
                    table.AddCell(new Phrase(record.BorrowDate.ToString("yyyy-MM-dd"), normalFont));
                    table.AddCell(new Phrase(record.ExpectedReturnDate.ToString("yyyy-MM-dd"), normalFont));
                    table.AddCell(new Phrase(record.Status, normalFont));
                }

                doc.Add(table);
                doc.Close();

                byte[] bytes = stream.ToArray();
                string fileName = $"EquipmentReport_{DateTime.Now:yyyyMMddHHmmss}.pdf";
                return File(bytes, "application/pdf", fileName);
            }
        }

        // =========================
        // ADMIN PROFILE - UPDATED
        // =========================
        public IActionResult AdminProfile()
        {
            if (HttpContext.Session.GetString("Role") != "Admin")
                return RedirectToAction("AccessDenied", "Account");

            var username = HttpContext.Session.GetString("Username");
            // Check for messages in TempData and move to ViewData
            if (TempData["SuccessMessage"] != null)
            {
                ViewData["SuccessMessage"] = TempData["SuccessMessage"];
            }
            if (TempData["ErrorMessage"] != null)
            {
                ViewData["ErrorMessage"] = TempData["ErrorMessage"];
            }

            // For hardcoded admin account
            if (username == "admin")
            {
                var defaultAdmin = new User
                {
                    FullName = HttpContext.Session.GetString("Admin_FullName") ?? "Administrator",
                    Username = "admin",
                    Email = HttpContext.Session.GetString("Admin_Email") ?? "admin@email.com",
                    Role = "Admin",
                    Status = "Active",
                    ProfilePicture = HttpContext.Session.GetString("ProfilePicture") ?? "default-profile.png"
                };

                ViewData["ActivePage"] = "AdminProfile";
                return View(defaultAdmin);
            }

            // DB-based admin accounts
            var admin = _context.Users.FirstOrDefault(u => u.Username == username && u.Role == "Admin");
            if (admin == null)
                return RedirectToAction("Login", "Account");

            ViewData["ActivePage"] = "AdminProfile";
            return View(admin);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult UpdateProfile(User updatedData)
        {
            if (HttpContext.Session.GetString("Role") != "Admin")
                return RedirectToAction("AccessDenied", "Account");

            string username = HttpContext.Session.GetString("Username");

            // HARD-CODED ADMIN
            if (username == "admin")
            {
                // Store all data in session for hardcoded admin
                HttpContext.Session.SetString("Admin_FullName", updatedData.FullName ?? "Administrator");
                HttpContext.Session.SetString("Admin_Email", updatedData.Email ?? "admin@email.com");

                TempData["SuccessMessage"] = "Profile updated successfully!";
                return RedirectToAction("AdminProfile");
            }

            // DATABASE ADMIN
            var admin = _context.Users.FirstOrDefault(u => u.UserID == updatedData.UserID);

            if (admin != null)
            {
                admin.FullName = updatedData.FullName;
                admin.Email = updatedData.Email;
                _context.SaveChanges();
            }

            TempData["SuccessMessage"] = "Profile updated successfully!";
            return RedirectToAction("AdminProfile");
        }

        [HttpPost]
        public async Task<IActionResult> UpdateProfilePicture(IFormFile ProfileImage)
        {
            if (HttpContext.Session.GetString("Role") != "Admin")
                return RedirectToAction("AccessDenied", "Account");

            if (ProfileImage == null || ProfileImage.Length == 0)
            {
                TempData["ErrorMessage"] = "Please select an image.";
                return RedirectToAction("AdminProfile");
            }

            // Validate file size (max 5MB)
            if (ProfileImage.Length > 5 * 1024 * 1024)
            {
                TempData["ErrorMessage"] = "Image size must not exceed 5MB.";
                return RedirectToAction("AdminProfile");
            }

            // Allowed image types
            var allowedExtensions = new[] { ".jpg", ".jpeg", ".png" };
            var fileExtension = Path.GetExtension(ProfileImage.FileName).ToLower();

            if (!allowedExtensions.Contains(fileExtension))
            {
                TempData["ErrorMessage"] = "Only JPG and PNG images are allowed.";
                return RedirectToAction("AdminProfile");
            }

            string username = HttpContext.Session.GetString("Username");

            // Create upload folder
            var uploadsFolder = Path.Combine(_webHostEnvironment.WebRootPath, "uploads", "profile");
            if (!Directory.Exists(uploadsFolder))
                Directory.CreateDirectory(uploadsFolder);

            // Create unique filename
            var newFileName = $"{Guid.NewGuid()}{fileExtension}";
            var newFilePath = Path.Combine(uploadsFolder, newFileName);

            // Save new image
            using (var stream = new FileStream(newFilePath, FileMode.Create))
                await ProfileImage.CopyToAsync(stream);

            // Handle hardcoded admin
            if (username == "admin")
            {
                // Delete old picture if it exists and is not the default
                var currentPicture = HttpContext.Session.GetString("ProfilePicture") ?? "default-profile.png";
                if (!string.IsNullOrEmpty(currentPicture) && currentPicture != "default-profile.png")
                {
                    var oldImagePath = Path.Combine(uploadsFolder, currentPicture);
                    if (System.IO.File.Exists(oldImagePath))
                        System.IO.File.Delete(oldImagePath);
                }

                // Update session
                HttpContext.Session.SetString("ProfilePicture", newFileName);
            }
            else
            {
                // Handle database admin
                var admin = _context.Users.FirstOrDefault(u => u.Username == username && u.Role == "Admin");
                if (admin != null)
                {
                    // Delete old picture if it exists and is not the default
                    if (!string.IsNullOrEmpty(admin.ProfilePicture) && admin.ProfilePicture != "default-profile.png")
                    {
                        var oldImagePath = Path.Combine(uploadsFolder, admin.ProfilePicture);
                        if (System.IO.File.Exists(oldImagePath))
                            System.IO.File.Delete(oldImagePath);
                    }

                    // Update database
                    admin.ProfilePicture = newFileName;
                    _context.SaveChanges();

                    // Update session
                    HttpContext.Session.SetString("ProfilePicture", newFileName);
                }
            }

            TempData["SuccessMessage"] = "Profile picture updated successfully!";
            return RedirectToAction("AdminProfile");
        }

        // =========================
        // HELPER: LOAD DROPDOWNS
        // =========================
        private void LoadDropdowns()
        {
            ViewBag.Types = new string[]
            {
                "Office Supplies", "Cleaning Supplies",
                "ICT Equipment", "Office Equipment",
                "Audio-Visual Equipment", "Sports Equipment", "Maintenance Tools"
            };
            ViewBag.Labels = new string[] { "Borrowable", "Non-Borrowable" };
        }
    }
}
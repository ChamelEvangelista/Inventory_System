using Inventory_System.Data;
using Inventory_System.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

using OfficeOpenXml;
using OfficeOpenXml.Style;
using iTextSharp.text;
using iTextSharp.text.pdf;
using System.Drawing;

using System.Linq;
using System.IO;
using System;
using System.Threading.Tasks;

namespace Inventory_System.Controllers
{
    public class AdminController : Controller
    {
        private readonly InventoryDbContext _context;

        public AdminController(InventoryDbContext context)
        {
            _context = context;
        }

        // =========================
        // DASHBOARD
        // =========================
        public IActionResult AdminDashboard()
        {
            if (HttpContext.Session.GetString("Role") != "Admin")
                return RedirectToAction("AccessDenied", "Account");

            ViewBag.PendingUsers = _context.Users.Count(u => u.Status == "Pending");
            ViewBag.TotalEquipment = _context.Equipment.Count();
            ViewBag.BorrowedItems = _context.BorrowRecords.Count(b => b.Status == "Borrowed");
            ViewBag.ReturnedItems = _context.BorrowRecords.Count(b => b.Status == "Returned");
            ViewBag.BorrowableCount = _context.Equipment.Count(e => e.Label == "Borrowable");
            ViewBag.NonBorrowableCount = _context.Equipment.Count(e => e.Label == "Non-Borrowable");

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
                    e.Category.ToLower().Contains(searchQuery) ||
                    e.Label.ToLower().Contains(searchQuery)
                );
            }

            ViewBag.SearchQuery = searchQuery;
            return View(equipmentList.ToList());
        }

        public IActionResult EquipmentList(string searchTerm, string categoryFilter, string typeFilter, string labelFilter, string statusFilter)
        {
            if (HttpContext.Session.GetString("Role") != "Admin")
                return RedirectToAction("AccessDenied", "Account");

            var query = _context.Equipment.AsQueryable();

            if (!string.IsNullOrEmpty(searchTerm))
                query = query.Where(e => e.Name.Contains(searchTerm) || e.Description.Contains(searchTerm));
            if (!string.IsNullOrEmpty(categoryFilter))
                query = query.Where(e => e.Category == categoryFilter);
            if (!string.IsNullOrEmpty(typeFilter))
                query = query.Where(e => e.Type == typeFilter);
            if (!string.IsNullOrEmpty(labelFilter))
                query = query.Where(e => e.Label == labelFilter);
            if (!string.IsNullOrEmpty(statusFilter))
                query = query.Where(e => e.Status == statusFilter);

            ViewBag.Categories = _context.Equipment.Select(e => e.Category).Distinct().ToList();
            ViewBag.Types = _context.Equipment.Select(e => e.Type).Distinct().ToList();
            ViewBag.Labels = _context.Equipment.Select(e => e.Label).Distinct().ToList();

            ViewBag.ActiveFilter = new { Search = searchTerm, Category = categoryFilter, Type = typeFilter, Label = labelFilter, Status = statusFilter };

            return View(query.ToList());
        }

        [HttpGet]
        public IActionResult AddEquipment()
        {
            if (HttpContext.Session.GetString("Role") != "Admin")
                return RedirectToAction("AccessDenied", "Account");

            LoadDropdowns();
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult AddEquipment(Equipment equipment)
        {
            try
            {
                equipment.Status = "Available";
                _context.Equipment.Add(equipment);
                _context.SaveChanges();
                TempData["SuccessMessage"] = "Item added successfully!";
            }
            catch
            {
                TempData["ErrorMessage"] = "Failed to add item.";
            }

            return RedirectToAction("ManageEquipment");
        }

        [HttpGet]
        public IActionResult EditEquipment(int id)
        {
            var item = _context.Equipment.Find(id);
            if (item == null) return NotFound();

            LoadDropdowns();
            return View(item);
        }

        [HttpPost]
        public IActionResult EditEquipment(Equipment equipment)
        {
            if (ModelState.IsValid)
            {
                _context.Equipment.Update(equipment);
                _context.SaveChanges();
                return RedirectToAction("ManageEquipment");
            }

            LoadDropdowns();
            return View(equipment);
        }

        public IActionResult DeleteEquipment(int id)
        {
            var item = _context.Equipment.Find(id);
            if (item != null)
            {
                _context.Equipment.Remove(item);
                _context.SaveChanges();
            }
            return RedirectToAction("ManageEquipment");
        }

        // =========================
        // REPORTS
        // =========================
        public IActionResult Reports()
        {
            if (HttpContext.Session.GetString("Role") != "Admin")
                return RedirectToAction("AccessDenied", "Account");

            // Summary statistics
            var totalEquipment = _context.Equipment.Count();
            var availableEquipment = _context.Equipment.Count(e => e.Status == "Available");
            var borrowedEquipment = _context.BorrowRecords.Count(b => b.Status == "Borrowed");
            var overdueEquipment = _context.BorrowRecords.Count(b =>
                b.Status == "Borrowed" && b.ExpectedReturnDate < DateTime.Now);

            var activeUsers = _context.Users.Count(u => u.Status == "Active");
            var pendingUsers = _context.Users.Count(u => u.Status == "Pending");

            // Borrowed items grouped by user type
            var borrowedByUserType = _context.BorrowRecords
                .Where(b => b.Status == "Borrowed")
                .GroupBy(b => b.User.UserType)
                .Select(g => new { UserType = g.Key, Count = g.Count() })
                .ToList();

            // Top 5 most borrowed equipment
            var topBorrowedEquipment = _context.BorrowRecords
                .GroupBy(b => b.Equipment.Name)
                .Select(g => new { EquipmentName = g.Key, TotalBorrowed = g.Sum(b => b.Quantity) })
                .OrderByDescending(x => x.TotalBorrowed)
                .Take(5)
                .ToList();

            // Overdue records
            var overdueRecords = _context.BorrowRecords
                .Where(b => b.Status == "Borrowed" && b.ExpectedReturnDate < DateTime.Now)
                .AsEnumerable() // Force client-side evaluation (prevents SQL coercion error)
                .Select(b => new
                {
                    b.BorrowID,
                    Borrower = b.User.FullName,
                    EquipmentName = b.Equipment.Name,
                    b.BorrowDate,
                    b.ExpectedReturnDate,
                    DaysOverdue = (DateTime.Now - b.ExpectedReturnDate).Days
                })
                .ToList();


            // Monthly borrowing activity
            var borrowActivity = _context.BorrowRecords
                .GroupBy(b => new { b.BorrowDate.Year, b.BorrowDate.Month })
                .Select(g => new
                {
                    Month = new DateTime(g.Key.Year, g.Key.Month, 1).ToString("MMM yyyy"),
                    Count = g.Count()
                })
                .OrderBy(x => x.Month)
                .ToList();

            // Assemble view model
            var viewModel = new ReportViewModel
            {
                TotalEquipment = totalEquipment,
                AvailableEquipment = availableEquipment,
                BorrowedEquipment = borrowedEquipment,
                OverdueEquipment = overdueEquipment,
                ActiveUsers = activeUsers,
                PendingUsers = pendingUsers,
                BorrowedByUserType = borrowedByUserType
                    .ToDictionary(x => x.UserType ?? "Unknown", x => x.Count),
                TopBorrowedEquipment = topBorrowedEquipment
                    .ToDictionary(x => x.EquipmentName, x => x.TotalBorrowed),
                OverdueRecords = overdueRecords.Select(o => new OverdueRecordVM
                {
                    BorrowID = o.BorrowID,
                    BorrowDate = o.BorrowDate,
                    ExpectedReturn = o.ExpectedReturnDate,
                    DaysOverdue = o.DaysOverdue
                }).ToList(),
                BorrowActivity = borrowActivity.ToDictionary(x => x.Month, x => x.Count)
            };

            return View(viewModel);
        }

        // =========================
        // EXPORT TO EXCEL (EPPlus)
        // =========================
        [HttpGet]
        public IActionResult ExportToExcel()
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

            using (var package = new ExcelPackage())
            {
                var worksheet = package.Workbook.Worksheets.Add("Borrow Report");

                // Title
                worksheet.Cells["A1"].Value = "Technology Equipment Inventory System - Borrowing Report";
                worksheet.Cells["A1:H1"].Merge = true;
                worksheet.Cells["A1"].Style.Font.Bold = true;
                worksheet.Cells["A1"].Style.Font.Size = 14;
                worksheet.Cells["A1"].Style.HorizontalAlignment = ExcelHorizontalAlignment.Center;

                // Headers
                worksheet.Cells["A3"].Value = "Borrow ID";
                worksheet.Cells["B3"].Value = "Borrower";
                worksheet.Cells["C3"].Value = "User Type";
                worksheet.Cells["D3"].Value = "Equipment";
                worksheet.Cells["E3"].Value = "Quantity";
                worksheet.Cells["F3"].Value = "Borrow Date";
                worksheet.Cells["G3"].Value = "Expected Return";
                worksheet.Cells["H3"].Value = "Status";

                using (var headerRange = worksheet.Cells["A3:H3"])
                {
                    headerRange.Style.Font.Bold = true;
                    headerRange.Style.Fill.PatternType = ExcelFillStyle.Solid;
                    headerRange.Style.Fill.BackgroundColor.SetColor(Color.LightGray);
                    headerRange.Style.HorizontalAlignment = ExcelHorizontalAlignment.Center;
                }

                int row = 4;
                foreach (var record in borrowRecords)
                {
                    worksheet.Cells[row, 1].Value = record.BorrowID;
                    worksheet.Cells[row, 2].Value = record.Borrower;
                    worksheet.Cells[row, 3].Value = record.UserType;
                    worksheet.Cells[row, 4].Value = record.Equipment;
                    worksheet.Cells[row, 5].Value = record.Quantity;
                    worksheet.Cells[row, 6].Value = record.BorrowDate.ToString("yyyy-MM-dd");
                    worksheet.Cells[row, 7].Value = record.ExpectedReturnDate.ToString("yyyy-MM-dd");
                    worksheet.Cells[row, 8].Value = record.Status;
                    row++;
                }

                worksheet.Cells.AutoFitColumns();

                var stream = new MemoryStream();
                package.SaveAs(stream);
                stream.Position = 0;

                string fileName = $"EquipmentReport_{DateTime.Now:yyyyMMddHHmmss}.xlsx";
                return File(stream, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", fileName);
            }
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
        // ADMIN PROFILE
        // =========================
        public IActionResult AdminProfile()
        {
            if (HttpContext.Session.GetString("Role") != "Admin")
                return RedirectToAction("AccessDenied", "Account");

            var username = HttpContext.Session.GetString("Username");

            // Hardcoded admin account
            if (username == "admin")
            {
                var defaultAdmin = new User
                {
                    FullName = "Administrator",
                    Username = "admin",
                    Email = "admin@email.com",
                    Role = "Admin",
                    Status = "Active",
                    ContactNumber = "N/A",
                    Address = "N/A",
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

            var admin = _context.Users.FirstOrDefault(u => u.UserID == updatedData.UserID);
            if (admin != null)
            {
                admin.FullName = updatedData.FullName;
                admin.Email = updatedData.Email;
                admin.ContactNumber = updatedData.ContactNumber;
                admin.Address = updatedData.Address;
                _context.SaveChanges();

                TempData["SuccessMessage"] = "Profile updated successfully!";
            }
            return RedirectToAction("AdminProfile");
        }

        [HttpPost]
        public async Task<IActionResult> UpdateProfilePicture(IFormFile ProfileImage)
        {
            if (ProfileImage != null)
            {
                var username = HttpContext.Session.GetString("Username");
                var admin = _context.Users.FirstOrDefault(u => u.Username == username && u.Role == "Admin");

                if (admin != null)
                {
                    var uploadsFolder = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot/uploads/admin-profile");
                    if (!Directory.Exists(uploadsFolder))
                        Directory.CreateDirectory(uploadsFolder);

                    var fileName = $"{Guid.NewGuid()}_{Path.GetFileName(ProfileImage.FileName)}";
                    var filePath = Path.Combine(uploadsFolder, fileName);

                    using (var stream = new FileStream(filePath, FileMode.Create))
                        await ProfileImage.CopyToAsync(stream);

                    admin.ProfilePicture = fileName;
                    _context.SaveChanges();
                }
            }

            return RedirectToAction("AdminProfile");
        }

        // =========================
        // HELPER: LOAD DROPDOWNS
        // =========================
        private void LoadDropdowns()
        {
            ViewBag.Categories = new string[] { "Consumable", "Non-Consumable" };
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

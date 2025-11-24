using Inventory_System.Data;
using Inventory_System.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Hosting;
using System;
using System.Linq;

namespace Inventory_System.Controllers
{
    public class UserController : Controller
    {
        private readonly InventoryDbContext _context;
        private readonly IWebHostEnvironment _environment;
        public UserController(InventoryDbContext context, IWebHostEnvironment environment)
        {
            _context = context;
            _environment = environment;
        }
        // 🧭 USER DASHBOARD
        public IActionResult UserDashboard()
        {
            // 🔒 Access control
            if (HttpContext.Session.GetString("Role") != "User")
                return RedirectToAction("AccessDenied", "Account");

            var username = HttpContext.Session.GetString("Username");
            var userType = HttpContext.Session.GetString("UserType");

            var user = _context.Users.FirstOrDefault(u => u.Username == username);
            if (user == null)
                return RedirectToAction("Login", "Account");

            var userId = user.UserID;

            // 📊 Dashboard Summary
            // FIXED: Only count borrowable equipment for the dashboard card
            ViewBag.TotalItems = _context.Equipment.Count(e => e.Status == "Available" && e.Quantity > 0 && e.Label == "Borrowable");
            ViewBag.ReturnedCount = _context.BorrowRecords.Count(b => b.UserID == userId && b.Status == "Returned");
            ViewBag.BorrowedCount = _context.BorrowRecords.Count(b => b.UserID == userId && b.Status == "Borrowed");

            // FIXED: Calculate overdue count based on date comparison, not just status
            var overdueCount = _context.BorrowRecords
                .Count(b => b.UserID == userId &&
                           b.Status == "Borrowed" &&
                           b.ExpectedReturnDate < DateTime.Now);
            ViewBag.OverdueCount = overdueCount;

            // 🔹 Currently Borrowed Items
            ViewBag.BorrowedItems = _context.BorrowRecords
                .Where(b => b.UserID == userId && b.Status == "Borrowed")
                .Join(_context.Equipment,
                      b => b.EquipmentID,
                      e => e.EquipmentID,
                      (b, e) => new
                      {
                          Name = e.Name,
                          Quantity = b.Quantity,
                          BorrowDate = b.BorrowDate,
                          ExpectedReturnDate = b.ExpectedReturnDate
                      })
                .ToList();

            // 🔹 Overdue Items - FIXED: Check for borrowed items with past expected return date
            var overdueRecords = _context.BorrowRecords
                .Where(b => b.UserID == userId &&
                           b.Status == "Borrowed" &&
                           b.ExpectedReturnDate < DateTime.Now)
                .Join(_context.Equipment,
                      b => b.EquipmentID,
                      e => e.EquipmentID,
                      (b, e) => new { b, e })
                .ToList();

            ViewBag.OverdueItems = overdueRecords
                .Select(x => new
                {
                    x.e.Name,
                    x.b.ExpectedReturnDate,
                    DaysOverdue = (DateTime.Now.Date - x.b.ExpectedReturnDate.Date).Days
                })
                .ToList();

            return View();
        }

        // 📦 BORROW EQUIPMENT (GET)
        [HttpGet]
        public IActionResult BorrowEquipment()
        {
            if (HttpContext.Session.GetString("Role") != "User")
                return RedirectToAction("AccessDenied", "Account");

            // FIXED: Only show borrowable equipment (not non-borrowable)
            var availableEquipment = _context.Equipment
                .Where(e => e.Status == "Available" && e.Quantity > 0 && e.Label == "Borrowable")
                .OrderBy(e => e.Type)
                .ToList();

            return View(availableEquipment);
        }

        // 📦 BORROW EQUIPMENT (POST)
        [HttpPost]
        public IActionResult SubmitBorrowRequest(int EquipmentID, int Quantity, string Purpose, DateTime ExpectedReturnDate)
        {
            var username = HttpContext.Session.GetString("Username");
            var user = _context.Users.FirstOrDefault(u => u.Username == username);

            if (user == null)
                return RedirectToAction("Login", "Account");

            // 🧠 Borrowing rules based on UserType
            int maxBorrow = user.UserType == "Instructor" ? 3 : 1;
            int currentBorrowed = _context.BorrowRecords.Count(b => b.UserID == user.UserID && b.Status == "Borrowed");

            if (currentBorrowed >= maxBorrow)
            {
                TempData["Error"] = $"You have reached your borrowing limit ({maxBorrow} item(s)).";
                return RedirectToAction("BorrowEquipment");
            }

            var equipment = _context.Equipment.Find(EquipmentID);
            if (equipment == null || equipment.Quantity < Quantity)
            {
                TempData["Error"] = "Insufficient equipment quantity.";
                return RedirectToAction("BorrowEquipment");
            }

            // 🧾 Create Borrow Record
            var borrowRecord = new BorrowRecord
            {
                UserID = user.UserID,
                EquipmentID = equipment.EquipmentID,
                Quantity = Quantity,
                Purpose = Purpose,
                BorrowDate = DateTime.Now,
                ExpectedReturnDate = ExpectedReturnDate,
                Status = "Borrowed"
            };

            // Update Equipment Quantity
            equipment.Quantity -= Quantity;
            if (equipment.Quantity == 0)
                equipment.Status = "Borrowed";

            _context.BorrowRecords.Add(borrowRecord);
            _context.SaveChanges();

            TempData["Success"] = "✅ Equipment borrowed successfully!";
            return RedirectToAction("UserDashboard");
        }

        // 🔁 RETURN EQUIPMENT (GET)
        [HttpGet]
        public IActionResult ReturnEquipment()
        {
            if (HttpContext.Session.GetString("Role") != "User")
                return RedirectToAction("AccessDenied", "Account");

            var username = HttpContext.Session.GetString("Username");
            var user = _context.Users.FirstOrDefault(u => u.Username == username);

            if (user == null)
                return RedirectToAction("Login", "Account");

            var borrowedItems = _context.BorrowRecords
                .Where(b => b.UserID == user.UserID && b.Status == "Borrowed")
                .Join(_context.Equipment,
                      b => b.EquipmentID,
                      e => e.EquipmentID,
                      (b, e) => new
                      {
                          b.BorrowID,
                          e.Label,
                          e.Name,
                          e.Type,
                          b.Quantity,
                          b.BorrowDate,
                          b.ExpectedReturnDate
                      })
                .ToList();

            return View(borrowedItems);
        }

        // 🔁 RETURN EQUIPMENT (POST)
        [HttpPost]
        public IActionResult ReturnEquipment(int BorrowID)
        {
            var record = _context.BorrowRecords.FirstOrDefault(b => b.BorrowID == BorrowID);
            if (record == null)
                return RedirectToAction("ReturnEquipment");

            var equipment = _context.Equipment.Find(record.EquipmentID);
            if (equipment != null)
            {
                equipment.Quantity += record.Quantity;
                equipment.Status = "Available";
            }

            record.Status = "Returned";
            record.ReturnDate = DateTime.Now;

            _context.SaveChanges();

            TempData["Success"] = "✅ Equipment returned successfully!";
            return RedirectToAction("ReturnEquipment");
        }

        // 🧾 BORROWING HISTORY
        public IActionResult BorrowHistory()
        {
            if (HttpContext.Session.GetString("Role") != "User")
                return RedirectToAction("AccessDenied", "Account");

            var username = HttpContext.Session.GetString("Username");
            var user = _context.Users.FirstOrDefault(u => u.Username == username);

            if (user == null)
                return RedirectToAction("Login", "Account");

            var history = _context.BorrowRecords
                .Where(b => b.UserID == user.UserID)
                .Join(_context.Equipment,
                      b => b.EquipmentID,
                      e => e.EquipmentID,
                      (b, e) => new
                      {
                          e.Label,
                          e.Name,
                          e.Type,
                          b.Quantity,
                          b.Purpose,
                          b.BorrowDate,
                          b.ExpectedReturnDate,
                          b.ReturnDate,
                          b.Status
                      })
                .OrderByDescending(b => b.BorrowDate)
                .ToList();

            return View(history);
        }

        // 🔄 AUTO UPDATE OVERDUE STATUS (Call this from dashboard or add as background task)
        private void UpdateOverdueStatus(int userId)
        {
            var overdueRecords = _context.BorrowRecords
                .Where(b => b.UserID == userId &&
                           b.Status == "Borrowed" &&
                           b.ExpectedReturnDate < DateTime.Now)
                .ToList();

            foreach (var record in overdueRecords)
            {
                record.Status = "Overdue";
            }

            if (overdueRecords.Any())
            {
                _context.SaveChanges();
            }
        }

        // ⚙️ USER SETTINGS (GET)
        [HttpGet]
        public IActionResult UserSettings()
        {
            if (HttpContext.Session.GetString("Role") != "User")
                return RedirectToAction("AccessDenied", "Account");

            var username = HttpContext.Session.GetString("Username");
            var user = _context.Users.FirstOrDefault(u => u.Username == username);

            if (user == null)
                return RedirectToAction("Login", "Account");

            return View(user);
        }

        // ⚙️ UPDATE USER INFORMATION (POST)
        [HttpPost]
        public IActionResult UpdateUserInfo(string Username, string Email, string NewPassword, string ConfirmPassword)
        {
            if (HttpContext.Session.GetString("Role") != "User")
                return RedirectToAction("AccessDenied", "Account");

            var currentUsername = HttpContext.Session.GetString("Username");
            var user = _context.Users.FirstOrDefault(u => u.Username == currentUsername);

            if (user == null)
                return RedirectToAction("Login", "Account");

            // Check if username is already taken by another user
            if (Username != user.Username && _context.Users.Any(u => u.Username == Username))
            {
                TempData["Error"] = "Username is already taken. Please choose a different one.";
                return RedirectToAction("UserSettings");
            }

            // Check if email is already taken by another user
            if (Email != user.Email && _context.Users.Any(u => u.Email == Email))
            {
                TempData["Error"] = "Email is already registered. Please use a different one.";
                return RedirectToAction("UserSettings");
            }

            // Validate password if provided
            if (!string.IsNullOrEmpty(NewPassword))
            {
                if (NewPassword.Length < 6)
                {
                    TempData["Error"] = "Password must be at least 6 characters long.";
                    return RedirectToAction("UserSettings");
                }

                if (NewPassword != ConfirmPassword)
                {
                    TempData["Error"] = "New password and confirmation password do not match.";
                    return RedirectToAction("UserSettings");
                }

                // Update password (in real application, hash the password)
                user.PasswordHash = NewPassword; // Note: You should hash this password in production
            }

            // Update user information
            user.Username = Username;
            user.Email = Email;

            _context.Users.Update(user);
            _context.SaveChanges();

            // Update session username if changed
            if (currentUsername != Username)
            {
                HttpContext.Session.SetString("Username", Username);
            }

            TempData["Success"] = "✅ User information updated successfully!";
            return RedirectToAction("UserSettings");
        }

        // 📸 UPLOAD PROFILE PICTURE (POST)
        [HttpPost]
        public IActionResult UploadProfilePicture(IFormFile profilePicture)
        {
            if (HttpContext.Session.GetString("Role") != "User")
                return RedirectToAction("AccessDenied", "Account");

            var username = HttpContext.Session.GetString("Username");
            var user = _context.Users.FirstOrDefault(u => u.Username == username);

            if (user == null)
                return RedirectToAction("Login", "Account");

            if (profilePicture != null && profilePicture.Length > 0)
            {
                // Validate file type
                var allowedExtensions = new[] { ".jpg", ".jpeg", ".png", ".gif" };
                var fileExtension = Path.GetExtension(profilePicture.FileName).ToLower();

                if (!allowedExtensions.Contains(fileExtension))
                {
                    TempData["Error"] = "Only image files (JPG, JPEG, PNG, GIF) are allowed.";
                    return RedirectToAction("UserSettings");
                }

                // Validate file size (max 5MB)
                if (profilePicture.Length > 5 * 1024 * 1024)
                {
                    TempData["Error"] = "Profile picture must be less than 5MB.";
                    return RedirectToAction("UserSettings");
                }

                // Create uploads directory if it doesn't exist
                var uploadsFolder = Path.Combine(_environment.WebRootPath, "uploads", "profiles");
                if (!Directory.Exists(uploadsFolder))
                {
                    Directory.CreateDirectory(uploadsFolder);
                }

                // Generate unique filename
                var fileName = $"user_{user.UserID}_{DateTime.Now:yyyyMMddHHmmss}{fileExtension}";
                var filePath = Path.Combine(uploadsFolder, fileName);

                // Save the file
                using (var stream = new FileStream(filePath, FileMode.Create))
                {
                    profilePicture.CopyTo(stream);
                }

                // Update user profile picture path
                user.ProfilePicture = $"/uploads/profiles/{fileName}";
                _context.Users.Update(user);
                _context.SaveChanges();

                TempData["Success"] = "✅ Profile picture uploaded successfully!";
            }
            else
            {
                TempData["Error"] = "Please select a valid image file.";
            }

            return RedirectToAction("UserSettings");
        }
    }
}

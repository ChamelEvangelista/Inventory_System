using Microsoft.AspNetCore.Mvc;
using Inventory_System.Data;
using Inventory_System.Models;
using System.Linq;
using BCrypt.Net;

namespace Inventory_System.Controllers
{
    public class AccountController : Controller
    {
        private readonly InventoryDbContext _context;

        public AccountController(InventoryDbContext context)
        {
            _context = context;
        }

        // ✅ Hardcoded admin credentials
        private readonly string adminUsername = "admin";
        private readonly string adminPassword = "admin123"; // Change anytime

        // ---------- Registration ----------
        [HttpGet]
        public IActionResult Register() => View();

        [HttpPost]
        public IActionResult Register(User model)
        {
            if (ModelState.IsValid)
            {
                // 🧩 Hash password and set defaults
                model.PasswordHash = BCrypt.Net.BCrypt.HashPassword(model.PasswordHash);
                model.Role = "User";
                model.Status = "Pending";

                _context.Users.Add(model);
                _context.SaveChanges();

                // 🧩 Store alert message temporarily
                TempData["SuccessMessage"] = "✅ Registration successful! Please wait for admin approval.";

                // 🧩 Redirect to Login
                return RedirectToAction("Login");
            }

            return View(model);
        }

        // ---------- Login ----------
        [HttpGet]
        public IActionResult Login()
        {
            // 🧩 Retrieve message passed from Register
            if (TempData.ContainsKey("SuccessMessage"))
            {
                ViewBag.Message = TempData["SuccessMessage"];
            }

            return View();
        }

        [HttpPost]
        public IActionResult Login(string username, string password)
        {
            // 🧩 Hardcoded Admin Login
            if (username == adminUsername && password == adminPassword)
            {
                // ✅ Set session for hardcoded admin safely (no DB lookup)
                HttpContext.Session.SetString("Username", adminUsername);
                HttpContext.Session.SetString("Role", "Admin");
                HttpContext.Session.SetString("FullName", "Administrator");
                HttpContext.Session.SetString("ProfilePicture", "default-profile.png");

                return RedirectToAction("AdminDashboard", "Admin");
            }

            // 🧩 Regular User Login (DB-based)
            var user = _context.Users.FirstOrDefault(u => u.Username == username);

            // Validate login credentials
            if (user == null || !BCrypt.Net.BCrypt.Verify(password, user.PasswordHash))
            {
                ViewBag.Error = "❌ Invalid username or password.";
                return View();
            }

            // Account status validation
            if (user.Status == "Pending")
            {
                ViewBag.Error = "⏳ Your account is still pending admin approval.";
                return View();
            }

            if (user.Status == "Disabled" || user.Status == "Rejected")
            {
                ViewBag.Error = "🚫 Your account has been disabled or rejected.";
                return View();
            }

            // ✅ Successful login — set all required session data
            HttpContext.Session.SetString("Username", user.Username);
            HttpContext.Session.SetString("Role", user.Role);
            HttpContext.Session.SetString("FullName", user.FullName);
            HttpContext.Session.SetString("ProfilePicture", user.ProfilePicture ?? "default-profile.png");

            if (!string.IsNullOrEmpty(user.UserType))
                HttpContext.Session.SetString("UserType", user.UserType);

            // Redirect based on role
            if (user.Role == "Admin")
                return RedirectToAction("AdminDashboard", "Admin");
            else
                return RedirectToAction("UserDashboard", "User");
        }

        // ---------- Forgot Password ----------
        [HttpGet]
        public IActionResult ForgotPassword() => View();

        [HttpPost]
        public IActionResult ForgotPassword(string username, string email)
        {
            var user = _context.Users.FirstOrDefault(u => u.Username == username && u.Email == email);
            if (user == null)
            {
                ViewBag.Error = "❌ No account found with that username and email.";
                return View();
            }

            string tempPassword = "Temp" + new Random().Next(1000, 9999).ToString();
            user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(tempPassword);
            _context.SaveChanges();

            ViewBag.Message = $"✅ Account verified! Your temporary password is: <strong>{tempPassword}</strong><br>Please log in and change your password immediately.";
            return View();
        }

        // ---------- Logout ----------
        public IActionResult Logout()
        {
            HttpContext.Session.Clear();
            return RedirectToAction("Login");
        }
    }
}

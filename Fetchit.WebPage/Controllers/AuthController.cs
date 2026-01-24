using Fetchit.WebPage.Data;
using Fetchit.WebPage.Models;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;

namespace Fetchit.WebPage.Controllers
{
    public class AuthController : Controller
    {
        private readonly MqttConfigContext _db;

        public AuthController(MqttConfigContext db)
        {
            _db = db;
        }

        [HttpGet]
        public IActionResult Login()
        {
            return View();
        }

        [HttpPost]
        public async Task<IActionResult> Login(string username, string password)
        {
            if (string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(password))
            {
                ViewBag.Error = "Username and password required.";
                return View();
            }

            var hashed = HashPassword(password);

            var user = await _db.Users
                .FirstOrDefaultAsync(x => x.Username == username && x.PasswordHash == hashed);

            if (user == null)
            {
                ViewBag.Error = "Invalid username or password";
                return View();
            }

            var claims = new List<Claim>
            {
                new Claim(ClaimTypes.Name, user.Username)
            };

            var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
            var principal = new ClaimsPrincipal(identity);

            await HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, principal);

            // Redirect to MQTT config index (or home)
            return RedirectToAction("Index", "MqttConfig");
        }

        public async Task<IActionResult> Logout()
        {
            await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            return RedirectToAction("Login");
        }

        private static string HashPassword(string password)
        {
            using var sha = SHA256.Create();
            return Convert.ToBase64String(sha.ComputeHash(Encoding.UTF8.GetBytes(password)));
        }


        [HttpPost]
        public async Task<IActionResult> ChangePassword(string currentPassword, string newPassword, string confirmPassword)
        {
            if (!User.Identity.IsAuthenticated)
                return RedirectToAction("Login");

            if (string.IsNullOrWhiteSpace(currentPassword) || string.IsNullOrWhiteSpace(newPassword))
            {
                ViewBag.Error = "All fields are required.";
                return View("Login");
            }

            if (currentPassword == newPassword)
            {
                ViewBag.Error = "New password cannot be the same as the current password.";
                return View("Login");
            }

            if (newPassword.Length < 6 ||
                !Regex.IsMatch(newPassword, @"^(?=.*[a-z])(?=.*[A-Z])(?=.*\d)(?=.*[@$!%*?&]).{6,}$"))
            {
                ViewBag.Error = "Password must be at least 6 characters and contain uppercase, lowercase, number and special character.";
                return View("Login");
            }


            if (newPassword != confirmPassword)
            {
                ViewBag.Error = "New password and confirmation do not match.";
                return View("Login");
            }

            var username = User.Identity.Name;
            var hashedCurrent = HashPassword(currentPassword);

            var user = await _db.Users.FirstOrDefaultAsync(u => u.Username == username && u.PasswordHash == hashedCurrent);
            if (user == null)
            {
                ViewBag.Error = "Current password is incorrect.";
                return View("Login");
            }

            // Update password
            user.PasswordHash = HashPassword(newPassword);
            _db.Users.Update(user);
            await _db.SaveChangesAsync();

            // Logout after password change
            await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);

            TempData["Message"] = "Password changed successfully. Please login again.";
            return RedirectToAction("Login");
        }

    }
}

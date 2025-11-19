using Microsoft.AspNetCore.Mvc;
using NewsDigestApp.Models;
using NewsDigestApp.Data;
using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json;

namespace NewsDigestApp.Controllers
{
    public class AccountController : Controller
    {
        private readonly AppDbContext _context;

        public AccountController(AppDbContext context)
        {
            _context = context;
        }

        public IActionResult Login()
        {
            if (HttpContext.Session.GetString("UserId") != null)
            {
                return RedirectToAction("Index", "Dashboard");
            }
            return View();
        }

        [HttpPost]
        public async Task<IActionResult> Login(LoginViewModel model)
        {
            if (ModelState.IsValid)
            {
                // Find user in database
                var user = await _context.Users
                    .FirstOrDefaultAsync(u => u.Email == model.Email);

                if (user == null)
                {
                    // User doesn't exist - redirect to registration
                    TempData["Error"] = "No account found with this email. Please register first.";
                    TempData["PrefilledEmail"] = model.Email;
                    return RedirectToAction("Register");
                }

                // User exists - verify password
                // In production, check password hash
                // For demo: accept any password or add basic check
                if (user.Password != model.Password)
                {
                    ModelState.AddModelError("", "Invalid password");
                    return View(model);
                }

                // Store user ID in session
                HttpContext.Session.SetString("UserId", user.Id.ToString());
                HttpContext.Session.SetString("UserEmail", user.Email ?? "");
                HttpContext.Session.SetString("UserName", user.FullName ?? "User");

                // Update days active
                user.DaysActive++;
                await _context.SaveChangesAsync();

                TempData["Success"] = $"Welcome back, {user.FullName}!";
                return RedirectToAction("Index", "Dashboard");
            }
            return View(model);
        }

        public IActionResult Register()
        {
            return View();
        }

        [HttpPost]
        public async Task<IActionResult> Register(RegisterViewModel model)
        {
            if (ModelState.IsValid)
            {
                // Check if email already exists
                var existingUser = await _context.Users
                    .FirstOrDefaultAsync(u => u.Email == model.Email);

                if (existingUser != null)
                {
                    ModelState.AddModelError("Email", "Email already registered");
                    return View(model);
                }

                // Create new user
                var user = new User
                {
                    FullName = model.FullName,
                    Email = model.Email,
                    Password = model.Password, // In production: hash this!
                    CreatedAt = DateTime.Now,
                    InterestsJson = JsonConvert.SerializeObject(new List<string> { "technology" }),
                    PreferredSentiment = "all",
                    MaxArticles = 12,
                    Language = "en"
                };

                _context.Users.Add(user);
                await _context.SaveChangesAsync();

                // Store user ID in session
                HttpContext.Session.SetString("UserId", user.Id.ToString());
                HttpContext.Session.SetString("UserEmail", user.Email ?? "");
                HttpContext.Session.SetString("UserName", user.FullName ?? "User");

                TempData["Success"] = $"Welcome to NewsDigest, {user.FullName}!";
                return RedirectToAction("Index", "Dashboard");
            }
            return View(model);
        }

        public IActionResult Logout()
        {
            HttpContext.Session.Clear();
            TempData["Success"] = "You have been logged out successfully.";
            return RedirectToAction("Index", "Home");
        }
    }
}
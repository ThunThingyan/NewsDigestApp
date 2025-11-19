using Microsoft.AspNetCore.Mvc;
using NewsDigestApp.Models;
using NewsDigestApp.Data;
using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json;

namespace NewsDigestApp.Controllers
{
    public class ProfileController : Controller
    {
        private readonly AppDbContext _context;

        public ProfileController(AppDbContext context)
        {
            _context = context;
        }

        public async Task<IActionResult> Index()
        {
            var userIdStr = HttpContext.Session.GetString("UserId");
            if (string.IsNullOrEmpty(userIdStr))
            {
                TempData["Error"] = "Please login to access your profile.";
                return RedirectToAction("Login", "Account");
            }

            int userId = int.Parse(userIdStr);
            var user = await _context.Users.FindAsync(userId);

            if (user == null)
            {
                return RedirectToAction("Login", "Account");
            }

            var userProfile = new UserProfile
            {
                Name = user.FullName,
                Email = user.Email,
                Phone = user.Phone,
                Country = user.Country,
                Bio = user.Bio
            };

            // Get statistics
            ViewBag.UserName = user.FullName;
            ViewBag.UserEmail = user.Email;
            ViewBag.ArticlesRead = user.ArticlesRead;
            ViewBag.DaysActive = user.DaysActive;

            // Get user interests
            var interests = string.IsNullOrEmpty(user.InterestsJson)
                ? new List<string>()
                : JsonConvert.DeserializeObject<List<string>>(user.InterestsJson);
            ViewBag.UserInterests = interests;

            // Get favorite category
            var history = await _context.ReadingHistory
                .Where(h => h.UserId == userId)
                .ToListAsync();

            var favoriteCategory = history
                .GroupBy(h => h.Category)
                .OrderByDescending(g => g.Count())
                .Select(g => g.Key)
                .FirstOrDefault() ?? "General";
            ViewBag.FavoriteCategory = favoriteCategory;

            // Get preferred sentiment
            var preferredSentiment = history
                .GroupBy(h => h.Sentiment)
                .OrderByDescending(g => g.Count())
                .Select(g => g.Key)
                .FirstOrDefault() ?? "All";
            ViewBag.PreferredSentiment = preferredSentiment;

            // Chart data - Last 7 days
            var chartLabels = new List<string>();
            var chartData = new List<int>();

            for (int i = 6; i >= 0; i--)
            {
                var date = DateTime.Now.AddDays(-i);
                chartLabels.Add(date.ToString("MMM dd"));

                var count = history.Count(h => h.ReadAt.Date == date.Date);
                chartData.Add(count);
            }

            ViewBag.ChartLabels = JsonConvert.SerializeObject(chartLabels);
            ViewBag.ChartData = JsonConvert.SerializeObject(chartData);

            return View(userProfile);
        }

        [HttpPost]
        public async Task<IActionResult> UpdateProfile([FromBody] UserProfile profile)
        {
            var userIdStr = HttpContext.Session.GetString("UserId");
            if (string.IsNullOrEmpty(userIdStr))
            {
                return Json(new { success = false, message = "Not authenticated" });
            }

            int userId = int.Parse(userIdStr);
            var user = await _context.Users.FindAsync(userId);

            if (user != null)
            {
                user.FullName = profile.Name;
                user.Email = profile.Email;
                user.Phone = profile.Phone;
                user.Country = profile.Country;
                user.Bio = profile.Bio;

                await _context.SaveChangesAsync();

                // Update session
                HttpContext.Session.SetString("UserName", user.FullName ?? "User");
                HttpContext.Session.SetString("UserEmail", user.Email ?? "");

                return Json(new { success = true, message = "Profile updated successfully!" });
            }

            return Json(new { success = false, message = "User not found" });
        }

        public async Task<IActionResult> ExportData()
        {
            var userIdStr = HttpContext.Session.GetString("UserId");
            if (string.IsNullOrEmpty(userIdStr))
            {
                return RedirectToAction("Login", "Account");
            }

            int userId = int.Parse(userIdStr);
            var user = await _context.Users.FindAsync(userId);
            var history = await _context.ReadingHistory
                .Where(h => h.UserId == userId)
                .OrderByDescending(h => h.ReadAt)
                .ToListAsync();

            var exportData = new
            {
                User = new
                {
                    user?.FullName,
                    user?.Email,
                    user?.Country,
                    user?.ArticlesRead,
                    user?.DaysActive,
                    user?.CreatedAt
                },
                Interests = JsonConvert.DeserializeObject<List<string>>(user?.InterestsJson ?? "[]"),
                PreferredSentiment = user?.PreferredSentiment,
                ReadingHistory = history.Select(h => new
                {
                    h.ArticleTitle,
                    h.ArticleUrl,
                    h.Category,
                    h.Sentiment,
                    h.ReadAt
                })
            };

            var json = JsonConvert.SerializeObject(exportData, Formatting.Indented);
            var bytes = System.Text.Encoding.UTF8.GetBytes(json);

            return File(bytes, "application/json", $"NewsDigest_Data_{DateTime.Now:yyyyMMdd}.json");
        }
    }
}
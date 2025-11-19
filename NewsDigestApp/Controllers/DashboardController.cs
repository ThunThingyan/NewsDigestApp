
using Microsoft.AspNetCore.Mvc;
using NewsDigestApp.Models;
using NewsDigestApp.Services;
using NewsDigestApp.Data;
using Microsoft.EntityFrameworkCore;

namespace NewsDigestApp.Controllers
{
    public class DashboardController : Controller
    {
        private readonly NewsService _newsService;
        private readonly SentimentAnalysisService _sentimentService;
        private readonly UserPreferenceService _preferenceService;
        private readonly AppDbContext _context;

        public DashboardController(
            NewsService newsService,
            SentimentAnalysisService sentimentService,
            UserPreferenceService preferenceService,
            AppDbContext context)
        {
            _newsService = newsService;
            _sentimentService = sentimentService;
            _preferenceService = preferenceService;
            _context = context;
        }

        public async Task<IActionResult> Index()
        {
            var userIdStr = HttpContext.Session.GetString("UserId");
            if (string.IsNullOrEmpty(userIdStr))
            {
                TempData["Error"] = "Please login to access the dashboard.";
                return RedirectToAction("Login", "Account");
            }

            int userId = int.Parse(userIdStr);

            // Get user preferences from database
            var preferences = await _preferenceService.GetUserPreferencesAsync(userId);

            ViewBag.UserName = HttpContext.Session.GetString("UserName") ?? "User";
            ViewBag.SuggestedInterests = await _preferenceService.GetSuggestedInterestsAsync(userId);

            return View(preferences);
        }

        [HttpPost]
        public async Task<IActionResult> GetNews([FromBody] UserPreferences preferences)
        {
            try
            {
                Console.WriteLine("=== Dashboard GetNews Called ===");

                var userIdStr = HttpContext.Session.GetString("UserId");
                int userId = 0;

                if (!string.IsNullOrEmpty(userIdStr))
                {
                    userId = int.Parse(userIdStr);

                    // Save preferences to database
                    await _preferenceService.SaveUserPreferencesAsync(userId, preferences);
                }

                var allArticles = new List<NewsArticle>();

                if (preferences.Interests != null && preferences.Interests.Any())
                {
                    Console.WriteLine($"Fetching news for {preferences.Interests.Count} interests");
                    foreach (var interest in preferences.Interests)
                    {
                        Console.WriteLine($"Fetching: {interest}");
                        // FIXED: Pass userId to avoid global cache issues
                        var articles = await _newsService.GetNewsAsync(interest, preferences.Language, 20, userId);
                        Console.WriteLine($"Got {articles.Count} articles for {interest}");
                        allArticles.AddRange(articles);
                    }
                }
                else
                {
                    Console.WriteLine("No interests selected, fetching general headlines");
                    // FIXED: Pass userId
                    allArticles = await _newsService.GetTopHeadlinesAsync("general", "us", userId);
                }

                Console.WriteLine($"Total articles before dedup: {allArticles.Count}");

                // Remove duplicates by URL (in case same article appears in multiple categories)
                allArticles = allArticles
                    .GroupBy(a => a.Url)
                    .Select(g => g.First())
                    .ToList();

                Console.WriteLine($"Total articles after dedup: {allArticles.Count}");

                // Apply ML.NET sentiment analysis
                Console.WriteLine($"Applying sentiment filter: {preferences.SentimentFilter}");
                var filteredArticles = _sentimentService.FilterBySentiment(allArticles, preferences.SentimentFilter);
                Console.WriteLine($"Articles after sentiment filter: {filteredArticles.Count}");

                filteredArticles = filteredArticles
                    .OrderByDescending(a => a.PublishedAt)
                    .Take(preferences.MaxArticles)
                    .ToList();

                Console.WriteLine($"✅ Final articles to return: {filteredArticles.Count}");
                return Json(filteredArticles);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ ERROR in GetNews: {ex.Message}");
                Console.WriteLine($"Stack: {ex.StackTrace}");
                return Json(new List<NewsArticle>());
            }
        }

        [HttpPost]
        public async Task<IActionResult> TrackArticleRead([FromBody] NewsArticle article)
        {
            try
            {
                var userIdStr = HttpContext.Session.GetString("UserId");
                if (!string.IsNullOrEmpty(userIdStr))
                {
                    int userId = int.Parse(userIdStr);
                    await _preferenceService.TrackArticleReadAsync(userId, article);
                    Console.WriteLine($"✅ Tracked article for user {userId}: {article.Title}");
                    return Ok();
                }
                return BadRequest("User not authenticated");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Error tracking article: {ex.Message}");
                return StatusCode(500);
            }
        }

        [HttpPost]
        public async Task<IActionResult> AutoAdjustPreferences()
        {
            try
            {
                var userIdStr = HttpContext.Session.GetString("UserId");
                if (!string.IsNullOrEmpty(userIdStr))
                {
                    int userId = int.Parse(userIdStr);
                    await _preferenceService.AutoAdjustPreferencesAsync(userId);
                    return Json(new { success = true, message = "Preferences auto-adjusted based on your reading history!" });
                }
                return Json(new { success = false, message = "User not authenticated" });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Error in AutoAdjustPreferences: {ex.Message}");
                return Json(new { success = false, message = "Error adjusting preferences" });
            }
        }

        // NEW: Clear news cache for fresh articles
        [HttpPost]
        public IActionResult ClearNewsCache()
        {
            try
            {
                var userIdStr = HttpContext.Session.GetString("UserId");
                if (!string.IsNullOrEmpty(userIdStr))
                {
                    int userId = int.Parse(userIdStr);
                    _newsService.ClearUserCache(userId);
                    Console.WriteLine($"🔄 Cleared cache for user {userId}");
                    return Json(new { success = true, message = "Cache cleared! You'll see fresh articles." });
                }
                return Json(new { success = false, message = "User not authenticated" });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Error clearing cache: {ex.Message}");
                return Json(new { success = false, message = "Error clearing cache" });
            }
        }

        [HttpGet]
        public async Task<IActionResult> GetReadingStats()
        {
            try
            {
                var userIdStr = HttpContext.Session.GetString("UserId");
                if (string.IsNullOrEmpty(userIdStr))
                {
                    return Json(new { today = 0, week = 0, total = 0 });
                }

                int userId = int.Parse(userIdStr);
                var now = DateTime.UtcNow;

                // Get all reading history for the user
                var allHistory = await _context.ReadingHistory
                    .Where(h => h.UserId == userId)
                    .ToListAsync();

                var stats = new
                {
                    today = allHistory.Count(h => h.ReadAt.Date == now.Date),
                    week = allHistory.Count(h => h.ReadAt >= now.AddDays(-7)),
                    total = allHistory.Count
                };

                Console.WriteLine($"📊 Stats for user {userId}: Today={stats.today}, Week={stats.week}, Total={stats.total}");
                return Json(stats);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Error getting reading stats: {ex.Message}");
                return Json(new { today = 0, week = 0, total = 0 });
            }
        }

        public async Task<IActionResult> ReadingHistory()
        {
            var userIdStr = HttpContext.Session.GetString("UserId");
            if (string.IsNullOrEmpty(userIdStr))
            {
                TempData["Error"] = "Please login to view reading history.";
                return RedirectToAction("Login", "Account");
            }

            int userId = int.Parse(userIdStr);

            // Get reading history from database
            var history = await _context.ReadingHistory
                .Where(h => h.UserId == userId)
                .OrderByDescending(h => h.ReadAt)
                .Take(100)
                .ToListAsync();

            ViewBag.UserName = HttpContext.Session.GetString("UserName") ?? "User";

            Console.WriteLine($"📚 Loading reading history for user {userId}: {history.Count} articles");
            return View(history);
        }

        [HttpPost]
        public async Task<IActionResult> ClearHistory()
        {
            try
            {
                var userIdStr = HttpContext.Session.GetString("UserId");
                if (!string.IsNullOrEmpty(userIdStr))
                {
                    int userId = int.Parse(userIdStr);

                    var historyToDelete = await _context.ReadingHistory
                        .Where(h => h.UserId == userId)
                        .ToListAsync();

                    _context.ReadingHistory.RemoveRange(historyToDelete);
                    await _context.SaveChangesAsync();

                    Console.WriteLine($"🗑️ Cleared {historyToDelete.Count} history records for user {userId}");
                    return Json(new { success = true, message = "Reading history cleared successfully!" });
                }
                return Json(new { success = false, message = "User not authenticated" });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Error clearing history: {ex.Message}");
                return Json(new { success = false, message = "Error clearing history" });
            }
        }
    }
}
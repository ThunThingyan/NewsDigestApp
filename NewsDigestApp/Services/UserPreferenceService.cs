using NewsDigestApp.Data;
using NewsDigestApp.Models;
using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json;

namespace NewsDigestApp.Services
{
    public class UserPreferenceService
    {
        private readonly AppDbContext _context;

        public UserPreferenceService(AppDbContext context)
        {
            _context = context;
        }

        // Get user preferences from database
        public async Task<UserPreferences> GetUserPreferencesAsync(int userId)
        {
            var user = await _context.Users.FindAsync(userId);
            if (user == null)
                return new UserPreferences();

            var interests = string.IsNullOrEmpty(user.InterestsJson)
                ? new List<string> { "technology" }
                : JsonConvert.DeserializeObject<List<string>>(user.InterestsJson) ?? new List<string>();

            return new UserPreferences
            {
                Interests = interests,
                SentimentFilter = user.PreferredSentiment ?? "all",
                MaxArticles = user.MaxArticles,
                Language = user.Language ?? "en"
            };
        }

        // Save user preferences to database
        public async Task SaveUserPreferencesAsync(int userId, UserPreferences preferences)
        {
            var user = await _context.Users.FindAsync(userId);
            if (user == null) return;

            user.InterestsJson = JsonConvert.SerializeObject(preferences.Interests);
            user.PreferredSentiment = preferences.SentimentFilter;
            user.MaxArticles = preferences.MaxArticles;
            user.Language = preferences.Language;

            await _context.SaveChangesAsync();
            Console.WriteLine($"✅ Saved preferences for user {userId}: {string.Join(", ", preferences.Interests)}");
        }

        // FIXED: Track article reading with duplicate prevention
        public async Task TrackArticleReadAsync(int userId, NewsArticle article)
        {
            try
            {
                // ⚠️ CRITICAL FIX: Check if article already tracked (prevent duplicates)
                var exists = await _context.ReadingHistory
                    .AnyAsync(h => h.UserId == userId && h.ArticleUrl == article.Url);

                if (exists)
                {
                    Console.WriteLine($"⏭️ Article already tracked: {article.Title}");
                    return;
                }

                // IMPROVED: Extract category from both title AND description
                var textToAnalyze = $"{article.Title} {article.Description}";
                var category = ExtractCategoryFromText(textToAnalyze);

                var history = new ReadingHistory
                {
                    UserId = userId,
                    ArticleTitle = article.Title ?? "Unknown",
                    ArticleUrl = article.Url ?? "",
                    Category = category,
                    Sentiment = article.Sentiment ?? "neutral",
                    ReadAt = DateTime.UtcNow // Use UTC for consistency
                };

                _context.ReadingHistory.Add(history);

                // Update user stats
                var user = await _context.Users.FindAsync(userId);
                if (user != null)
                {
                    user.ArticlesRead++;
                }

                await _context.SaveChangesAsync();
                Console.WriteLine($"✅ Tracked: {article.Title} | Category: {category} | Sentiment: {article.Sentiment}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Error tracking article: {ex.Message}");
            }
        }

        // Analyze user reading patterns and suggest interests
        public async Task<List<string>> GetSuggestedInterestsAsync(int userId)
        {
            var history = await _context.ReadingHistory
                .Where(h => h.UserId == userId)
                .OrderByDescending(h => h.ReadAt)
                .Take(50)
                .ToListAsync();

            if (!history.Any())
            {
                Console.WriteLine($"💡 No history for user {userId}, suggesting defaults");
                return new List<string> { "technology", "business" };
            }

            // Count category frequency
            var categoryFrequency = history
                .GroupBy(h => h.Category)
                .OrderByDescending(g => g.Count())
                .Select(g => new { Category = g.Key, Count = g.Count() })
                .Where(c => !string.IsNullOrEmpty(c.Category))
                .Take(5)
                .ToList();

            var suggested = categoryFrequency.Select(c => c.Category).ToList();

            Console.WriteLine($"💡 Suggested interests for user {userId}: " +
                            string.Join(", ", categoryFrequency.Select(c => $"{c.Category}({c.Count})")));

            return suggested;
        }

        // Get preferred sentiment based on reading history
        public async Task<string> GetPreferredSentimentAsync(int userId)
        {
            // Analyze last 30 days of reading
            var thirtyDaysAgo = DateTime.UtcNow.AddDays(-30);
            var history = await _context.ReadingHistory
                .Where(h => h.UserId == userId && h.ReadAt >= thirtyDaysAgo)
                .ToListAsync();

            if (history.Count < 5) // Need minimum 5 articles
            {
                Console.WriteLine($"⚠️ Not enough recent history ({history.Count} articles) for user {userId}");
                return "all";
            }

            var sentimentCounts = history
                .GroupBy(h => h.Sentiment)
                .Select(g => new { Sentiment = g.Key, Count = g.Count() })
                .OrderByDescending(x => x.Count)
                .ToList();

            var total = history.Count;
            var dominant = sentimentCounts.FirstOrDefault();

            // Only change if there's a clear preference (>40%)
            if (dominant != null)
            {
                var percentage = (dominant.Count * 100.0) / total;

                Console.WriteLine($"📊 Sentiment analysis for user {userId}:");
                foreach (var s in sentimentCounts)
                {
                    Console.WriteLine($"   {s.Sentiment}: {s.Count} ({s.Count * 100.0 / total:F1}%)");
                }

                if (percentage > 40)
                {
                    Console.WriteLine($"✅ Clear preference: {dominant.Sentiment} ({percentage:F1}%)");
                    return dominant.Sentiment ?? "all";
                }
                else
                {
                    Console.WriteLine($"⚠️ No clear preference (highest: {percentage:F1}%), keeping 'all'");
                }
            }

            return "all";
        }

        // IMPROVED: Auto-adjust preferences based on behavior with better logic
        public async Task AutoAdjustPreferencesAsync(int userId)
        {
            try
            {
                Console.WriteLine($"🤖 Starting auto-adjustment for user {userId}...");

                // Check if user has enough reading history (minimum 10 articles)
                var totalArticlesRead = await _context.ReadingHistory
                    .CountAsync(h => h.UserId == userId);

                if (totalArticlesRead < 10)
                {
                    Console.WriteLine($"⚠️ Not enough data: {totalArticlesRead} articles (need 10+)");
                    return;
                }

                Console.WriteLine($"📚 Analyzing {totalArticlesRead} total articles...");

                // Get AI suggestions based on recent behavior
                var suggestedInterests = await GetSuggestedInterestsAsync(userId);
                var preferredSentiment = await GetPreferredSentimentAsync(userId);

                var user = await _context.Users.FindAsync(userId);
                if (user == null)
                {
                    Console.WriteLine($"❌ User {userId} not found");
                    return;
                }

                // Get current interests
                var currentInterests = string.IsNullOrEmpty(user.InterestsJson)
                    ? new List<string>()
                    : JsonConvert.DeserializeObject<List<string>>(user.InterestsJson) ?? new List<string>();

                Console.WriteLine($"📌 Current interests: {string.Join(", ", currentInterests)}");

                // IMPROVED LOGIC: Replace with top reading patterns (not merge)
                // This makes the AI more responsive to changing behavior
                if (suggestedInterests.Any())
                {
                    // Take top 3-4 most read categories
                    var newInterests = suggestedInterests.Take(4).ToList();

                    user.InterestsJson = JsonConvert.SerializeObject(newInterests);
                    Console.WriteLine($"✅ Updated interests to: {string.Join(", ", newInterests)}");
                }

                // Update sentiment preference
                var oldSentiment = user.PreferredSentiment;
                user.PreferredSentiment = preferredSentiment;

                if (oldSentiment != preferredSentiment)
                {
                    Console.WriteLine($"✅ Updated sentiment: {oldSentiment} → {preferredSentiment}");
                }
                else
                {
                    Console.WriteLine($"📌 Sentiment unchanged: {preferredSentiment}");
                }

                await _context.SaveChangesAsync();
                Console.WriteLine($"🎯 Auto-adjustment complete for user {userId}!");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Error in AutoAdjustPreferencesAsync: {ex.Message}");
                throw;
            }
        }

        // IMPROVED: Extract category from full text (title + description)
        private string ExtractCategoryFromText(string? text)
        {
            if (string.IsNullOrEmpty(text)) return "general";

            var keywords = new Dictionary<string, string[]>
            {
                { "technology", new[] { "tech", "ai", "software", "app", "digital", "computer", "internet",
                    "startup", "coding", "programming", "data", "cloud", "cyber" } },
                { "business", new[] { "business", "market", "stock", "company", "ceo", "startup",
                    "finance", "economy", "trade", "investor", "revenue" } },
                { "sports", new[] { "sport", "game", "player", "team", "match", "football", "basketball",
                    "soccer", "championship", "tournament", "athlete" } },
                { "health", new[] { "health", "medical", "doctor", "hospital", "disease", "vaccine",
                    "medicine", "treatment", "patient", "wellness" } },
                { "science", new[] { "science", "research", "study", "discovery", "scientist",
                    "experiment", "laboratory", "physics", "chemistry", "biology" } },
                { "entertainment", new[] { "movie", "music", "celebrity", "entertainment", "film",
                    "actor", "singer", "album", "concert", "show" } }
            };

            var lowerText = text.ToLower();
            var categoryScores = new Dictionary<string, int>();

            // Score each category based on keyword matches
            foreach (var category in keywords)
            {
                var score = category.Value.Count(keyword => lowerText.Contains(keyword));
                if (score > 0)
                {
                    categoryScores[category.Key] = score;
                }
            }

            // Return category with highest score
            if (categoryScores.Any())
            {
                var bestMatch = categoryScores.OrderByDescending(kv => kv.Value).First();
                Console.WriteLine($"🏷️ Category: {bestMatch.Key} (score: {bestMatch.Value})");
                return bestMatch.Key;
            }

            return "general";
        }
    }
}
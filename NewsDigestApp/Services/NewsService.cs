using NewsDigestApp.Models;
using Newtonsoft.Json;

namespace NewsDigestApp.Services
{
    public class NewsService
    {
        private readonly HttpClient _httpClient;
        private readonly IConfiguration _configuration;
        private readonly string _apiKey;
        private readonly string _baseUrl;
        private static readonly Dictionary<int, HashSet<string>> _userShownArticles = new Dictionary<int, HashSet<string>>();
        private static readonly Dictionary<int, DateTime> _userLastClearTime = new Dictionary<int, DateTime>();

        public NewsService(HttpClient httpClient, IConfiguration configuration)
        {
            _httpClient = httpClient;
            _configuration = configuration;
            _apiKey = _configuration["NewsApi:ApiKey"] ?? "";
            _baseUrl = _configuration["NewsApi:BaseUrl"] ?? "https://newsapi.org/v2/";

            _httpClient.DefaultRequestHeaders.Add("User-Agent", "NewsDigestApp/1.0 (ASP.NET Core)");

            Console.WriteLine($"=== NewsService Initialized ===");
            Console.WriteLine($"API Key: {(_apiKey.Length > 0 ? _apiKey.Substring(0, 8) + "..." : "MISSING")}");
        }

        private void ManageUserCache(int userId)
        {
            // Initialize user cache if not exists
            if (!_userShownArticles.ContainsKey(userId))
            {
                _userShownArticles[userId] = new HashSet<string>();
                _userLastClearTime[userId] = DateTime.UtcNow;
            }

            // Clear user cache every 2 hours for fresh content
            if ((DateTime.UtcNow - _userLastClearTime[userId]).TotalHours > 2)
            {
                _userShownArticles[userId].Clear();
                _userLastClearTime[userId] = DateTime.UtcNow;
                Console.WriteLine($"🔄 Cleared article cache for user {userId}");
            }

            // Limit cache size per user (keep only last 200 articles)
            if (_userShownArticles[userId].Count > 200)
            {
                var oldest = _userShownArticles[userId].Take(100).ToList();
                foreach (var url in oldest)
                {
                    _userShownArticles[userId].Remove(url);
                }
                Console.WriteLine($"🧹 Cleaned old cache for user {userId}");
            }
        }

        public async Task<List<NewsArticle>> GetNewsAsync(string query, string language = "en", int pageSize = 20, int userId = 0)
        {
            try
            {
                ManageUserCache(userId);

                // Fetch articles from last 3 days to get fresher content
                var fromDate = DateTime.UtcNow.AddDays(-3).ToString("yyyy-MM-dd");
                var sortBy = "publishedAt"; // Get newest first

                var url = $"{_baseUrl}everything?q={query}&language={language}&pageSize={pageSize * 2}&from={fromDate}&sortBy={sortBy}&apiKey={_apiKey}";
                Console.WriteLine($"Fetching URL: {url.Replace(_apiKey, "***")}");

                var response = await _httpClient.GetAsync(url);
                var content = await response.Content.ReadAsStringAsync();

                Console.WriteLine($"Response Status: {response.StatusCode}");

                if (!response.IsSuccessStatusCode)
                {
                    Console.WriteLine($"ERROR: API returned status {response.StatusCode}");
                    return new List<NewsArticle>();
                }

                var newsResponse = JsonConvert.DeserializeObject<NewsApiResponse>(content);

                if (newsResponse?.Articles == null || newsResponse.Articles.Count == 0)
                {
                    Console.WriteLine("WARNING: No articles found in response");
                    return new List<NewsArticle>();
                }

                // Filter out already shown articles for this user and removed articles
                var userCache = _userShownArticles[userId];
                var newArticles = newsResponse.Articles
                    .Where(a => !string.IsNullOrEmpty(a.Url) &&
                                !userCache.Contains(a.Url) &&
                                a.Title != "[Removed]" &&
                                !string.IsNullOrEmpty(a.Title) &&
                                !string.IsNullOrEmpty(a.Description) &&
                                a.Description != "[Removed]" &&
                                a.Title.Length > 10 &&  // Filter very short titles
                                a.Description.Length > 20)  // Filter very short descriptions
                    .Select(a => new NewsArticle
                    {
                        Title = a.Title,
                        Description = a.Description,
                        Content = a.Content,
                        Author = a.Author,
                        Url = a.Url,
                        UrlToImage = a.UrlToImage,
                        PublishedAt = a.PublishedAt,
                        Source = a.Source?.Name
                    })
                    .Take(pageSize)
                    .ToList();

                // Mark these articles as shown for this user
                foreach (var article in newArticles)
                {
                    userCache.Add(article.Url);
                }

                Console.WriteLine($"SUCCESS: Found {newArticles.Count} NEW articles for user {userId} (filtered {newsResponse.Articles.Count - newArticles.Count} duplicates/removed)");

                return newArticles;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"EXCEPTION in GetNewsAsync: {ex.Message}");
                return new List<NewsArticle>();
            }
        }

        public async Task<List<NewsArticle>> GetTopHeadlinesAsync(string category = "general", string country = "us", int userId = 0)
        {
            try
            {
                ManageUserCache(userId);

                var url = $"{_baseUrl}top-headlines?category={category}&country={country}&pageSize=50&apiKey={_apiKey}";
                Console.WriteLine($"Fetching Headlines URL: {url.Replace(_apiKey, "***")}");

                var response = await _httpClient.GetAsync(url);
                var content = await response.Content.ReadAsStringAsync();

                Console.WriteLine($"Headlines Status: {response.StatusCode}");

                if (!response.IsSuccessStatusCode)
                {
                    Console.WriteLine($"ERROR: API returned status {response.StatusCode}");
                    return new List<NewsArticle>();
                }

                var newsResponse = JsonConvert.DeserializeObject<NewsApiResponse>(content);

                if (newsResponse?.Articles == null || newsResponse.Articles.Count == 0)
                {
                    Console.WriteLine("WARNING: No articles in headlines response");
                    return new List<NewsArticle>();
                }

                // Filter out already shown and removed articles
                var userCache = _userShownArticles[userId];
                var newArticles = newsResponse.Articles
                    .Where(a => !string.IsNullOrEmpty(a.Url) &&
                                !userCache.Contains(a.Url) &&
                                a.Title != "[Removed]" &&
                                !string.IsNullOrEmpty(a.Title) &&
                                !string.IsNullOrEmpty(a.Description))

                    .Select(a => new NewsArticle
                    {
                        Title = a.Title,
                        Description = a.Description,
                        Content = a.Content,
                        Author = a.Author,
                        Url = a.Url,
                        UrlToImage = a.UrlToImage,
                        PublishedAt = a.PublishedAt,
                        Source = a.Source?.Name
                    })
                    .Take(20)
                    .ToList();

                // Mark as shown
                foreach (var article in newArticles)
                {
                    userCache.Add(article.Url);
                }

                Console.WriteLine($"SUCCESS: Found {newArticles.Count} NEW headlines for user {userId}");

                return newArticles;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"EXCEPTION in GetTopHeadlinesAsync: {ex.Message}");
                return new List<NewsArticle>();
            }
        }

        // NEW: Method to clear cache for a specific user
        public void ClearUserCache(int userId)
        {
            if (_userShownArticles.ContainsKey(userId))
            {
                _userShownArticles[userId].Clear();
                _userLastClearTime[userId] = DateTime.UtcNow;
                Console.WriteLine($"🔄 Manually cleared article cache for user {userId}");
            }
        }
    }
}

//using NewsDigestApp.Models;
//using Newtonsoft.Json;

//namespace NewsDigestApp.Services
//{
//    public class NewsService
//    {
//        private readonly HttpClient _httpClient;
//        private readonly IConfiguration _configuration;
//        private readonly string _apiKey;
//        private readonly string _baseUrl;
//        private static readonly Dictionary<int, HashSet<string>> _userShownArticles = new Dictionary<int, HashSet<string>>();
//        private static readonly Dictionary<int, DateTime> _userLastClearTime = new Dictionary<int, DateTime>();

//        public NewsService(HttpClient httpClient, IConfiguration configuration)
//        {
//            _httpClient = httpClient;
//            _configuration = configuration;
//            _apiKey = _configuration["NewsApi:ApiKey"] ?? "";
//            _baseUrl = _configuration["NewsApi:BaseUrl"] ?? "https://newsapi.org/v2/";

//            _httpClient.DefaultRequestHeaders.Add("User-Agent", "NewsDigestApp/1.0 (ASP.NET Core)");
//        }

//        private void ManageUserCache(int userId)
//        {
//            if (!_userShownArticles.ContainsKey(userId))
//            {
//                _userShownArticles[userId] = new HashSet<string>();
//                _userLastClearTime[userId] = DateTime.UtcNow;
//            }

//            if ((DateTime.UtcNow - _userLastClearTime[userId]).TotalHours > 2)
//            {
//                _userShownArticles[userId].Clear();
//                _userLastClearTime[userId] = DateTime.UtcNow;
//            }

//            if (_userShownArticles[userId].Count > 200)
//            {
//                var toRemove = _userShownArticles[userId].Take(100).ToList();
//                foreach (var url in toRemove)
//                    _userShownArticles[userId].Remove(url);
//            }
//        }

//        public async Task<List<NewsArticle>> GetNewsAsync(string query, string language = "en", int pageSize = 20, int userId = 0)
//        {
//            try
//            {
//                ManageUserCache(userId);

//                var fromDate = DateTime.UtcNow.AddDays(-3).ToString("yyyy-MM-dd");

//                var url = $"{_baseUrl}everything?q={query}&language={language}&pageSize={pageSize * 2}&from={fromDate}&sortBy=publishedAt&apiKey={_apiKey}";
//                var response = await _httpClient.GetAsync(url);
//                var content = await response.Content.ReadAsStringAsync();

//                if (!response.IsSuccessStatusCode)
//                    return new List<NewsArticle>();

//                var newsResponse = JsonConvert.DeserializeObject<NewsApiResponse>(content);

//                if (newsResponse?.Articles == null)
//                    return new List<NewsArticle>();

//                var userCache = _userShownArticles[userId];

//                var newArticles = newsResponse.Articles
//                    .Where(a =>
//                        !string.IsNullOrEmpty(a.Url) &&
//                        !userCache.Contains(a.Url) &&
//                        !string.IsNullOrEmpty(a.Title) &&
//                        !string.IsNullOrEmpty(a.Description) &&
//                        a.Title != "[Removed]" &&
//                        a.Description != "[Removed]" &&
//                        !string.IsNullOrEmpty(a.UrlToImage) // ✅ prevent blank image cards
//                    )
//                    .Take(pageSize)
//                    .Select(a => new NewsArticle
//                    {
//                        Title = a.Title,
//                        Description = a.Description,
//                        Content = a.Content,
//                        Author = a.Author,
//                        Url = a.Url,
//                        UrlToImage = a.UrlToImage,
//                        PublishedAt = a.PublishedAt,
//                        Source = a.Source?.Name
//                    })
//                    .ToList();

//                foreach (var article in newArticles)
//                    userCache.Add(article.Url);

//                return newArticles;
//            }
//            catch
//            {
//                return new List<NewsArticle>();
//            }
//        }

//        public async Task<List<NewsArticle>> GetTopHeadlinesAsync(string category = "general", string country = "us", int userId = 0)
//        {
//            try
//            {
//                ManageUserCache(userId);

//                var url = $"{_baseUrl}top-headlines?category={category}&country={country}&pageSize=40&apiKey={_apiKey}";
//                var response = await _httpClient.GetAsync(url);
//                var content = await response.Content.ReadAsStringAsync();

//                if (!response.IsSuccessStatusCode)
//                    return new List<NewsArticle>();

//                var newsResponse = JsonConvert.DeserializeObject<NewsApiResponse>(content);

//                if (newsResponse?.Articles == null)
//                    return new List<NewsArticle>();

//                var userCache = _userShownArticles[userId];

//                var newArticles = newsResponse.Articles
//                    .Where(a =>
//                        !string.IsNullOrEmpty(a.Url) &&
//                        !userCache.Contains(a.Url) &&
//                        !string.IsNullOrEmpty(a.Title) &&
//                        !string.IsNullOrEmpty(a.Description) &&
//                        a.Title != "[Removed]" &&
//                        a.Description != "[Removed]" &&
//                        !string.IsNullOrEmpty(a.UrlToImage) // ✅ filter articles without images
//                    )
//                    .Take(20)
//                    .Select(a => new NewsArticle
//                    {
//                        Title = a.Title,
//                        Description = a.Description,
//                        Content = a.Content,
//                        Author = a.Author,
//                        Url = a.Url,
//                        UrlToImage = a.UrlToImage,
//                        PublishedAt = a.PublishedAt,
//                        Source = a.Source?.Name
//                    })
//                    .ToList();

//                foreach (var article in newArticles)
//                    userCache.Add(article.Url);

//                return newArticles;
//            }
//            catch
//            {
//                return new List<NewsArticle>();
//            }
//        }

//        public void ClearUserCache(int userId)
//        {
//            if (_userShownArticles.ContainsKey(userId))
//            {
//                _userShownArticles[userId].Clear();
//                _userLastClearTime[userId] = DateTime.UtcNow;
//            }
//        }
//    }
//}

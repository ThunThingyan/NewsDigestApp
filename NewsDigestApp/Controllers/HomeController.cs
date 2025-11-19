    using Microsoft.AspNetCore.Mvc;
    using NewsDigestApp.Models;
    using NewsDigestApp.Services;

    namespace NewsDigestApp.Controllers
    {
        public class HomeController : Controller
        {
            private readonly NewsService _newsService;
            private readonly SentimentAnalysisService _sentimentService;

            public HomeController(NewsService newsService, SentimentAnalysisService sentimentService)
            {
                _newsService = newsService;
                _sentimentService = sentimentService;
            }

            public async Task<IActionResult> Index()
            {
                var articles = await _newsService.GetTopHeadlinesAsync("technology", "us");

                foreach (var article in articles.Take(6))
                {
                    var textToAnalyze = $"{article.Title} {article.Description}";
                    article.Sentiment = _sentimentService.AnalyzeSentiment(textToAnalyze);
                    article.SentimentScore = _sentimentService.GetSentimentScore(textToAnalyze);
                }

                return View(articles.Take(6).ToList());
            }

            public IActionResult ContactUs()
            {
                return View();
            }

            [HttpPost]
            public IActionResult ContactUs(ContactFormModel model)
            {
                if (ModelState.IsValid)
                {
                    TempData["Success"] = "Thank you! Your message has been sent successfully.";
                    return RedirectToAction("ContactUs");
                }
                return View(model);
            }

            public IActionResult Privacy()
            {
                return View();
            }
        }
    }
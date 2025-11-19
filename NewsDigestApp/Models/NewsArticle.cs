namespace NewsDigestApp.Models
{
    public class NewsArticle
    {
        public string? Title { get; set; }
        public string? Description { get; set; }
        public string? Content { get; set; }
        public string? Author { get; set; }
        public string? Url { get; set; }
        public string? UrlToImage { get; set; }
        public DateTime PublishedAt { get; set; }
        public string? Source { get; set; }
        public string? Sentiment { get; set; }
        public float SentimentScore { get; set; }
    }
}

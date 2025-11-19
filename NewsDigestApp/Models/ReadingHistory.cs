namespace NewsDigestApp.Models
{
    public class ReadingHistory
    {
        public int Id { get; set; }
        public int UserId { get; set; }
        public string? ArticleTitle { get; set; }
        public string? ArticleUrl { get; set; }
        public string? Category { get; set; }
        public string? Sentiment { get; set; }
        public DateTime ReadAt { get; set; } = DateTime.Now;
    }
}

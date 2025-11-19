namespace NewsDigestApp.Models
{
    public class UserPreferences
    {
        public List<string> Interests { get; set; } = new List<string> { "technology" };
        public string SentimentFilter { get; set; } = "all";
        public string Language { get; set; } = "en";
        public int MaxArticles { get; set; } = 12;
    }
}

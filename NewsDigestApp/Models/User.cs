namespace NewsDigestApp.Models
{
    public class User
    {
        public int Id { get; set; }
        public string? FullName { get; set; }
        public string? Email { get; set; }
        public string? Password { get; set; }
        public string? Country { get; set; }
        public string? Phone { get; set; }
        public string? Bio { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.Now;

        // User Preferences (JSON stored as string)
        public string? InterestsJson { get; set; }
        public string? PreferredSentiment { get; set; } = "all";
        public int MaxArticles { get; set; } = 12;
        public string? Language { get; set; } = "en";

        // Reading History
        public int ArticlesRead { get; set; } = 0;
        public int DaysActive { get; set; } = 0;
    }
}
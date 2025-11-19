namespace NewsDigestApp.Models
{
    public class SentimentData
    {
        public string? Text { get; set; }
    }

    public class SentimentPrediction
    {
        public bool Prediction { get; set; }
        public float Probability { get; set; }
        public float Score { get; set; }
    }
}

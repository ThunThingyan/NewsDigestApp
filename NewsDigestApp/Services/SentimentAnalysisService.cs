using Microsoft.ML;
using Microsoft.ML.Data;
using NewsDigestApp.Models;

namespace NewsDigestApp.Services
{
    public class SentimentAnalysisService
    {
        private readonly MLContext _mlContext;
        private ITransformer? _model;
        private PredictionEngine<SentimentInput, SentimentOutput>? _predictionEngine;

        public SentimentAnalysisService()
        {
            _mlContext = new MLContext(seed: 1);
            Console.WriteLine("=== Initializing ML.NET Sentiment Analysis ===");
            TrainModel();
        }

        private void TrainModel()
        {
            try
            {
                // Training data with sentiment labels (1 = positive, 0 = negative)
                var trainingData = new List<SentimentInput>
                {
                    new SentimentInput { Text = "This is absolutely amazing and wonderful news", Label = true },
                    new SentimentInput { Text = "Great breakthrough in technology innovation", Label = true },
                    new SentimentInput { Text = "Excellent progress and fantastic achievement", Label = true },
                    new SentimentInput { Text = "Positive outlook for future developments", Label = true },
                    new SentimentInput { Text = "Successful implementation of new system", Label = true },
                    new SentimentInput { Text = "Revolutionary advancement in the field", Label = true },
                    new SentimentInput { Text = "Outstanding performance and results", Label = true },
                    new SentimentInput { Text = "Terrible disaster strikes the region", Label = false },
                    new SentimentInput { Text = "Disappointing results and major setback", Label = false },
                    new SentimentInput { Text = "Crisis deepens as situation worsens dramatically", Label = false },
                    new SentimentInput { Text = "Tragic accident causes widespread devastation", Label = false },
                    new SentimentInput { Text = "Severe problems and critical failures", Label = false },
                    new SentimentInput { Text = "Negative impact on economy and markets", Label = false },
                    new SentimentInput { Text = "Declining performance and poor outcomes", Label = false }
                };

                var data = _mlContext.Data.LoadFromEnumerable(trainingData);

                // Data processing pipeline
                var pipeline = _mlContext.Transforms.Text.FeaturizeText(
                    outputColumnName: "Features",
                    inputColumnName: nameof(SentimentInput.Text))
                    .Append(_mlContext.BinaryClassification.Trainers.SdcaLogisticRegression(
                        labelColumnName: nameof(SentimentInput.Label),
                        featureColumnName: "Features"));

                // Train the model
                Console.WriteLine("Training ML.NET sentiment model...");
                _model = pipeline.Fit(data);
                _predictionEngine = _mlContext.Model.CreatePredictionEngine<SentimentInput, SentimentOutput>(_model);
                Console.WriteLine("ML.NET model trained successfully!");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error training ML model: {ex.Message}");
            }
        }

        public string AnalyzeSentiment(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
                return "neutral";

            try
            {
                // Use ML.NET prediction if available
                if (_predictionEngine != null)
                {
                    var input = new SentimentInput { Text = text };
                    var prediction = _predictionEngine.Predict(input);

                    Console.WriteLine($"ML Prediction - Text: '{text.Substring(0, Math.Min(50, text.Length))}...' | Positive: {prediction.Prediction} | Score: {prediction.Score}");

                    // Use score to determine sentiment
                    if (prediction.Score > 0.6f)
                        return "positive";
                    else if (prediction.Score < 0.4f)
                        return "negative";
                    else
                        return "neutral";
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"ML Prediction error: {ex.Message}");
            }

            // Fallback to keyword-based analysis
            return AnalyzeSentimentKeywords(text);
        }

        private string AnalyzeSentimentKeywords(string text)
        {
            var lowerText = text.ToLower();

            var positiveWords = new[] { "great", "amazing", "wonderful", "excellent", "fantastic",
                "breakthrough", "success", "positive", "exciting", "innovative", "growth", "wins",
                "best", "outstanding", "revolutionary", "advancement" };

            var negativeWords = new[] { "terrible", "disaster", "crisis", "tragic", "disappointing",
                "setback", "concern", "failure", "decline", "death", "killed", "worst",
                "devastating", "critical", "severe" };

            int positiveCount = positiveWords.Count(word => lowerText.Contains(word));
            int negativeCount = negativeWords.Count(word => lowerText.Contains(word));

            if (positiveCount > negativeCount)
                return "positive";
            else if (negativeCount > positiveCount)
                return "negative";
            else
                return "neutral";
        }

        public float GetSentimentScore(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
                return 0.5f;

            try
            {
                if (_predictionEngine != null)
                {
                    var input = new SentimentInput { Text = text };
                    var prediction = _predictionEngine.Predict(input);
                    return prediction.Score;
                }
            }
            catch { }

            // Fallback
            var sentiment = AnalyzeSentiment(text);
            return sentiment switch
            {
                "positive" => 0.8f,
                "negative" => 0.2f,
                _ => 0.5f
            };
        }

        public List<NewsArticle> FilterBySentiment(List<NewsArticle> articles, string sentimentFilter)
        {
            Console.WriteLine($"=== Analyzing sentiment for {articles.Count} articles ===");

            foreach (var article in articles)
            {
                var textToAnalyze = $"{article.Title} {article.Description}";
                article.Sentiment = AnalyzeSentiment(textToAnalyze);
                article.SentimentScore = GetSentimentScore(textToAnalyze);
            }

            Console.WriteLine($"Sentiment counts - Positive: {articles.Count(a => a.Sentiment == "positive")}, " +
                            $"Negative: {articles.Count(a => a.Sentiment == "negative")}, " +
                            $"Neutral: {articles.Count(a => a.Sentiment == "neutral")}");

            if (sentimentFilter.ToLower() == "all")
                return articles;

            var filtered = articles.Where(a => a.Sentiment?.ToLower() == sentimentFilter.ToLower()).ToList();
            Console.WriteLine($"After filtering for '{sentimentFilter}': {filtered.Count} articles");

            return filtered;
        }
    }

    // ML.NET Model Classes
    public class SentimentInput
    {
        public string? Text { get; set; }
        public bool Label { get; set; }
    }

    public class SentimentOutput
    {
        [ColumnName("PredictedLabel")]
        public bool Prediction { get; set; }

        public float Probability { get; set; }
        public float Score { get; set; }
    }
}
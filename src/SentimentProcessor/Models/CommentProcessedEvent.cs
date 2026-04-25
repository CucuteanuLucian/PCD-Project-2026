namespace SentimentProcessor.Models;

public class CommentProcessedEvent
{
    public int CommentId { get; set; }
    public string Status { get; set; } = "processed";
    public double SentimentScore { get; set; }
    public string UserId { get; set; } = string.Empty;
    public long ProcessedAtMs { get; set; }
}

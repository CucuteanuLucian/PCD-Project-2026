namespace SentimentProcessor.Models;

public class CommentCreatedEvent
{
    public int CommentId { get; set; }
    public string Content { get; set; } = string.Empty;
    public int ArticleId { get; set; }
}

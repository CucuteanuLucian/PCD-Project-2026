using System.Text.Json;
using Azure.Messaging.ServiceBus;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using Npgsql;
using SentimentProcessor.Models;
using SentimentProcessor.Services;

namespace SentimentProcessor.Functions;

public class CommentProcessorFunction(ILogger<CommentProcessorFunction> logger, NpgsqlDataSource db)
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    [Function("CommentProcessor")]
    [ServiceBusOutput("comments-processed", Connection = "ServiceBusConnection")]
    public async Task<string> Run(
        [ServiceBusTrigger("comments-queue", Connection = "ServiceBusConnection")]
            ServiceBusReceivedMessage message,
        FunctionContext context
    )
    {
        var body = message.Body.ToString();
        logger.LogInformation("Processing message: {MessageId}", message.MessageId);

        var evt = JsonSerializer.Deserialize<CommentCreatedEvent>(body, JsonOptions);
        if (evt == null)
        {
            logger.LogError("Failed to deserialize message body");
            throw new InvalidOperationException("Cannot deserialize CommentCreatedEvent");
        }

        // Single query: get status + author in one round-trip, idempotency check included
        await using var cmd = db.CreateCommand("""
            SELECT c."Status", p."Username"
            FROM "Comments" c
            JOIN "Persons" p ON p."PersonId" = c."AuthorId"
            WHERE c."CommentId" = @id
            """);
        cmd.Parameters.AddWithValue("id", evt.CommentId);
        await using var reader = await cmd.ExecuteReaderAsync();

        if (!await reader.ReadAsync())
        {
            logger.LogWarning("Comment {CommentId} not found in DB", evt.CommentId);
            return null!;
        }

        var status = reader.GetString(0);
        var username = reader.GetString(1);
        await reader.CloseAsync();

        if (status != "pending")
        {
            logger.LogWarning("Comment {CommentId} already {Status}, skipping", evt.CommentId, status);
            return null!;
        }

        var score = SentimentAnalyzer.Analyze(evt.Content);
        logger.LogInformation("Comment {CommentId} score={Score} user={User}", evt.CommentId, score, username);

        await using var updateCmd = db.CreateCommand("""
            UPDATE "Comments"
            SET "Status" = 'processed', "SentimentScore" = @score
            WHERE "CommentId" = @id
            """);
        updateCmd.Parameters.AddWithValue("score", score);
        updateCmd.Parameters.AddWithValue("id", evt.CommentId);
        await updateCmd.ExecuteNonQueryAsync();

        var processedEvent = new CommentProcessedEvent
        {
            CommentId = evt.CommentId,
            Status = "processed",
            SentimentScore = score,
            UserId = username,
            ProcessedAtMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
        };

        return JsonSerializer.Serialize(processedEvent);
    }
}

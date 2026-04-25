using System.Text.Json;
using Azure.Messaging.ServiceBus;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using Npgsql;
using SentimentProcessor.Models;
using SentimentProcessor.Services;

namespace SentimentProcessor.Functions;

public class CommentProcessorFunction(ILogger<CommentProcessorFunction> logger)
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

        var pgConn = Environment.GetEnvironmentVariable("PostgresConnection")!;

        // Idempotenta — verificam daca comentariul a fost deja procesat
        var currentStatus = await GetCommentStatusAsync(pgConn, evt.CommentId);
        if (currentStatus != "pending")
        {
            logger.LogWarning(
                "Comment {CommentId} already processed (status={Status}), skipping",
                evt.CommentId,
                currentStatus
            );
            return null!;
        }

        var score = SentimentAnalyzer.Analyze(evt.Content);
        logger.LogInformation("Comment {CommentId} sentiment score: {Score}", evt.CommentId, score);

        var (_, username) = await GetCommentAuthorAsync(pgConn, evt.CommentId);

        await UpdateCommentAsync(pgConn, evt.CommentId, score);

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

    private static async Task<string> GetCommentStatusAsync(string connectionString, int commentId)
    {
        await using var conn = new NpgsqlConnection(connectionString);
        await conn.OpenAsync();
        await using var cmd = new NpgsqlCommand(
            "SELECT \"Status\" FROM \"Comments\" WHERE \"CommentId\" = @id",
            conn
        );
        cmd.Parameters.AddWithValue("id", commentId);
        var result = await cmd.ExecuteScalarAsync();
        return result?.ToString() ?? "unknown";
    }

    private static async Task<(int AuthorId, string Username)> GetCommentAuthorAsync(
        string connectionString,
        int commentId
    )
    {
        await using var conn = new NpgsqlConnection(connectionString);
        await conn.OpenAsync();
        await using var cmd = new NpgsqlCommand(
            """
            SELECT c."AuthorId", p."Username"
            FROM "Comments" c
            JOIN "Persons" p ON p."PersonId" = c."AuthorId"
            WHERE c."CommentId" = @id
            """,
            conn
        );
        cmd.Parameters.AddWithValue("id", commentId);
        await using var reader = await cmd.ExecuteReaderAsync();
        if (await reader.ReadAsync())
        {
            return (reader.GetInt32(0), reader.GetString(1));
        }

        return (0, string.Empty);
    }

    private static async Task UpdateCommentAsync(
        string connectionString,
        int commentId,
        double score
    )
    {
        await using var conn = new NpgsqlConnection(connectionString);
        await conn.OpenAsync();
        await using var cmd = new NpgsqlCommand(
            """
            UPDATE "Comments"
            SET "Status" = 'processed', "SentimentScore" = @score
            WHERE "CommentId" = @id
            """,
            conn
        );
        cmd.Parameters.AddWithValue("score", score);
        cmd.Parameters.AddWithValue("id", commentId);
        await cmd.ExecuteNonQueryAsync();
    }
}

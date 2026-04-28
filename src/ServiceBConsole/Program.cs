using System.Text.Json;
using Azure.Messaging.ServiceBus;
using Npgsql;

var builder = WebApplication.CreateBuilder(args);

var sbConn = Environment.GetEnvironmentVariable("ConnectionStrings__ServiceBus")
    ?? "Endpoint=sb://pcd-servicebus-ns.servicebus.windows.net/;SharedAccessKeyName=RootManageSharedAccessKey;SharedAccessKey=REDACTED";
var pgConn = Environment.GetEnvironmentVariable("ConnectionStrings__DefaultConnection")
    ?? "Host=pcd-postgres-server.postgres.database.azure.com;Database=conduit;Username=pcdadmin;Password=REDACTED;SslMode=Require";

builder.Services.AddSingleton(_ => NpgsqlDataSource.Create(pgConn));
builder.Services.AddSingleton(_ => new ServiceBusClient(sbConn));
builder.Services.AddHostedService<SentimentWorker>();

var app = builder.Build();
app.MapGet("/health", () => Results.Ok(new { status = "healthy", service = "SentimentWorker" }));
app.Run();

// ── Background Worker ────────────────────────────────────────────────────────
public class SentimentWorker(NpgsqlDataSource db, ServiceBusClient sbClient, ILogger<SentimentWorker> logger)
    : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await using var sender = sbClient.CreateSender("comments-processed");
        var processor = sbClient.CreateProcessor("comments-queue", new ServiceBusProcessorOptions
        {
            MaxConcurrentCalls = 1,
            AutoCompleteMessages = false
        });

        processor.ProcessMessageAsync += async args =>
        {
            try
            {
                var body = args.Message.Body.ToString();
                var evt = JsonSerializer.Deserialize<CommentCreatedEvent>(body, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

                if (evt == null)
                {
                    await args.DeadLetterMessageAsync(args.Message, "DeserializationFailed", "Could not deserialize CommentCreatedEvent");
                    logger.LogWarning("Dead-lettered: invalid message body");
                    return;
                }

                logger.LogInformation("Processing comment #{CommentId}: {Content}", evt.CommentId, evt.Content);

                string status;
                string username;
                await using (var cmd = db.CreateCommand("""
                    SELECT c."Status", p."Username"
                    FROM "Comments" c
                    JOIN "Persons" p ON p."PersonId" = c."AuthorId"
                    WHERE c."CommentId" = @id
                    """))
                {
                    cmd.Parameters.AddWithValue("id", evt.CommentId);
                    await using var reader = await cmd.ExecuteReaderAsync();
                    if (!await reader.ReadAsync())
                    {
                        await args.DeadLetterMessageAsync(args.Message, "CommentNotFound", $"CommentId {evt.CommentId} not found");
                        logger.LogWarning("Dead-lettered: comment #{CommentId} not found", evt.CommentId);
                        return;
                    }
                    status = reader.GetString(0);
                    username = reader.GetString(1);
                }

                if (status != "pending")
                {
                    logger.LogInformation("Comment #{CommentId} already {Status}, skipping", evt.CommentId, status);
                    await args.CompleteMessageAsync(args.Message);
                    return;
                }

                var score = Analyze(evt.Content);
                logger.LogInformation("Comment #{CommentId} score={Score} user={User}", evt.CommentId, score, username);

                await using (var cmd = db.CreateCommand("""
                    UPDATE "Comments"
                    SET "Status" = 'processed', "SentimentScore" = @score
                    WHERE "CommentId" = @id
                    """))
                {
                    cmd.Parameters.AddWithValue("score", score);
                    cmd.Parameters.AddWithValue("id", evt.CommentId);
                    await cmd.ExecuteNonQueryAsync();
                }

                var processedEvt = JsonSerializer.Serialize(new
                {
                    CommentId = evt.CommentId,
                    Status = "processed",
                    SentimentScore = score,
                    UserId = username,
                    ProcessedAtMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()
                });
                await sender.SendMessageAsync(new ServiceBusMessage(processedEvt) { ContentType = "application/json" });
                await args.CompleteMessageAsync(args.Message);
                logger.LogInformation("Done with #{CommentId}", evt.CommentId);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error processing message — abandoning");
                await args.AbandonMessageAsync(args.Message);
            }
        };

        processor.ProcessErrorAsync += args =>
        {
            logger.LogError(args.Exception, "Service Bus error: {Source}", args.ErrorSource);
            return Task.CompletedTask;
        };

        await processor.StartProcessingAsync(stoppingToken);
        logger.LogInformation("SentimentWorker listening on 'comments-queue'");

        await Task.Delay(Timeout.Infinite, stoppingToken).ContinueWith(_ => { });
        await processor.StopProcessingAsync();
    }

    private static double Analyze(string text)
    {
        var positive = new[] { "good", "great", "excellent", "amazing", "love", "wonderful", "fantastic", "best", "happy", "perfect" };
        var negative = new[] { "bad", "terrible", "awful", "hate", "worst", "horrible", "poor", "sad", "fail", "ugly" };
        var lower = text.ToLowerInvariant();
        int pos = Array.FindAll(positive, w => lower.Contains(w, StringComparison.Ordinal)).Length;
        int neg = Array.FindAll(negative, w => lower.Contains(w, StringComparison.Ordinal)).Length;
        if (pos == 0 && neg == 0) return 0;
        return Math.Clamp((double)(pos - neg) / (pos + neg), -1, 1);
    }
}

record CommentCreatedEvent(int CommentId, string Content, int ArticleId);

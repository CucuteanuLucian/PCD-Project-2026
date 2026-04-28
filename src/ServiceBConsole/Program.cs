using System.Text.Json;
using Azure.Messaging.ServiceBus;
using Npgsql;

var sbConn = Environment.GetEnvironmentVariable("ConnectionStrings__ServiceBus")
    ?? "Endpoint=sb://pcd-servicebus-ns.servicebus.windows.net/;SharedAccessKeyName=RootManageSharedAccessKey;SharedAccessKey=REDACTED";
var pgConn = Environment.GetEnvironmentVariable("ConnectionStrings__DefaultConnection")
    ?? "Host=pcd-postgres-server.postgres.database.azure.com;Database=conduit;Username=pcdadmin;Password=REDACTED;SslMode=Require";

Console.WriteLine("[ServiceB] Starting...");

await using var sbClient = new ServiceBusClient(sbConn);
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
        if (evt == null) { await args.DeadLetterMessageAsync(args.Message, "DeserializationFailed", "null"); return; }

        Console.WriteLine($"[ServiceB] Processing comment #{evt.CommentId}: {evt.Content}");

        // Get current status (idempotency)
        string status;
        string username;
        await using (var conn = new NpgsqlConnection(pgConn))
        {
            await conn.OpenAsync();
            await using var cmd = new NpgsqlCommand("SELECT c.\"Status\", p.\"Username\" FROM \"Comments\" c JOIN \"Persons\" p ON p.\"PersonId\" = c.\"AuthorId\" WHERE c.\"CommentId\" = @id", conn);
            cmd.Parameters.AddWithValue("id", evt.CommentId);
            await using var reader = await cmd.ExecuteReaderAsync();
            if (!await reader.ReadAsync()) { await args.CompleteMessageAsync(args.Message); return; }
            status = reader.GetString(0);
            username = reader.GetString(1);
        }

        if (status != "pending")
        {
            Console.WriteLine($"[ServiceB] Comment #{evt.CommentId} already {status}, skipping");
            await args.CompleteMessageAsync(args.Message);
            return;
        }

        // Sentiment analysis
        var score = Analyze(evt.Content);
        Console.WriteLine($"[ServiceB] Comment #{evt.CommentId} score={score} user={username}");

        // Update DB
        await using (var conn = new NpgsqlConnection(pgConn))
        {
            await conn.OpenAsync();
            await using var cmd = new NpgsqlCommand("UPDATE \"Comments\" SET \"Status\" = 'processed', \"SentimentScore\" = @score WHERE \"CommentId\" = @id", conn);
            cmd.Parameters.AddWithValue("score", score);
            cmd.Parameters.AddWithValue("id", evt.CommentId);
            var rows = await cmd.ExecuteNonQueryAsync();
            Console.WriteLine($"[ServiceB] DB updated {rows} rows");
        }

        // Publish to comments-processed
        var processedEvt = JsonSerializer.Serialize(new
        {
            CommentId = evt.CommentId,
            Status = "processed",
            SentimentScore = score,
            UserId = username,
            ProcessedAtMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()
        });
        await sender.SendMessageAsync(new ServiceBusMessage(processedEvt) { ContentType = "application/json" });
        Console.WriteLine($"[ServiceB] Published to comments-processed");

        await args.CompleteMessageAsync(args.Message);
        Console.WriteLine($"[ServiceB] Done with #{evt.CommentId}");
    }
    catch (Exception ex)
    {
        Console.WriteLine($"[ServiceB] ERROR: {ex.Message}");
        await args.AbandonMessageAsync(args.Message);
    }
};

processor.ProcessErrorAsync += args =>
{
    Console.WriteLine($"[ServiceB] Bus error: {args.Exception.Message}");
    return Task.CompletedTask;
};

await processor.StartProcessingAsync();
Console.WriteLine("[ServiceB] Listening on comments-queue... Ctrl+C to stop");
await Task.Delay(Timeout.Infinite);

static double Analyze(string text)
{
    var positive = new[] { "good", "great", "excellent", "amazing", "love", "wonderful", "fantastic", "best", "happy", "perfect" };
    var negative = new[] { "bad", "terrible", "awful", "hate", "worst", "horrible", "poor", "sad", "fail", "ugly" };
    var lower = text.ToLower();
    int pos = positive.Count(w => lower.Contains(w));
    int neg = negative.Count(w => lower.Contains(w));
    if (pos == 0 && neg == 0) return 0;
    return Math.Clamp((double)(pos - neg) / (pos + neg), -1, 1);
}

record CommentCreatedEvent(int CommentId, string Content, int ArticleId);

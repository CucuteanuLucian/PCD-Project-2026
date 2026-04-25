using System.Text.Json;
using Azure.Messaging.ServiceBus;
using Microsoft.AspNetCore.SignalR;
using NotificationService.Hubs;
using NotificationService.Models;

namespace NotificationService.Services;

// Background service — rulează continuu și ascultă mesaje din Service Bus
// Când primește un CommentProcessedEvent, îl trimite prin SignalR către clientul corect
public class ServiceBusListener(
    IHubContext<CommentHub> hubContext,
    ILogger<ServiceBusListener> logger,
    ServiceBusClient client
) : BackgroundService
{
    private ServiceBusProcessor? _processor;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _processor = client.CreateProcessor(
            "comments-processed",
            new ServiceBusProcessorOptions { MaxConcurrentCalls = 5, AutoCompleteMessages = false }
        );

        _processor.ProcessMessageAsync += HandleMessageAsync;
        _processor.ProcessErrorAsync += HandleErrorAsync;

        await _processor.StartProcessingAsync(stoppingToken);

        logger.LogInformation("ServiceBusListener started — listening on 'comments-processed'");

        await Task.Delay(Timeout.Infinite, stoppingToken);
    }

    private async Task HandleMessageAsync(ProcessMessageEventArgs args)
    {
        try
        {
            var body = args.Message.Body.ToString();
            var evt = JsonSerializer.Deserialize<CommentProcessedEvent>(body);

            if (evt == null)
            {
                await args.DeadLetterMessageAsync(
                    args.Message,
                    "DeserializationFailed",
                    "Could not deserialize message"
                );
                return;
            }

            if (logger.IsEnabled(LogLevel.Information))
            {
                logger.LogInformation(
                    "Received CommentProcessedEvent: CommentId={CommentId}, Score={Score}",
                    evt.CommentId,
                    evt.SentimentScore
                );
            }

            // Trimite notificarea prin SignalR grupului de user
            await hubContext
                .Clients.Group($"user-{evt.UserId}")
                .SendAsync(
                    "CommentProcessed",
                    new
                    {
                        commentId = evt.CommentId,
                        status = evt.Status,
                        sentimentScore = evt.SentimentScore,
                        receivedAtMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
                    }
                );

            await args.CompleteMessageAsync(args.Message);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error processing message");
            // Abandon — Service Bus va retrimite mesajul (retry automat)
            await args.AbandonMessageAsync(args.Message);
        }
    }

    private Task HandleErrorAsync(ProcessErrorEventArgs args)
    {
        logger.LogError(args.Exception, "Service Bus processor error: {Source}", args.ErrorSource);
        return Task.CompletedTask;
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        if (_processor != null)
        {
            await _processor.StopProcessingAsync(cancellationToken);
        }

        await base.StopAsync(cancellationToken);
    }
}

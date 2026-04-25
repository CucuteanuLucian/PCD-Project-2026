using Azure.Messaging.ServiceBus;
using System.Text.Json;
using System.Threading.Tasks;
using Conduit.Features.Comments;

namespace Conduit.Infrastructure.Messaging;

public class AzureServiceBusMessageBus(ServiceBusClient client) : IMessageBus
{
    private readonly ServiceBusSender _sender = client.CreateSender("comments-queue");

    public async Task PublishAsync(CommentCreatedEvent message)
    {
        var json = JsonSerializer.Serialize(message);

        await _sender.SendMessageAsync(new ServiceBusMessage(json)
        {
            ContentType = "application/json"
        });
    }
}
using System;
using System.Threading.Tasks;
using Conduit.Features.Comments;

namespace Conduit.Infrastructure;

public class FakeMessageBus : IMessageBus
{
    public Task PublishAsync(CommentCreatedEvent message)
    {
        Console.WriteLine(
            $"[MessageBus] Comment sent -> Id: {message.CommentId}, Article: {message.ArticleId}"
        );

        return Task.CompletedTask;
    }
}

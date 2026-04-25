using System.Threading.Tasks;
using Conduit.Features.Comments;

namespace Conduit.Infrastructure;

public interface IMessageBus
{
    Task PublishAsync(CommentCreatedEvent message);
}

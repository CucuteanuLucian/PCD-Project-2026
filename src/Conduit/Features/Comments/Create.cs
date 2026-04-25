using System;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using Conduit.Domain;
using Conduit.Features.Comments;
using Conduit.Infrastructure;
using Conduit.Infrastructure.Errors;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Conduit.Features.Comments;

public class Create
{
    public record CommentData(string? Body);

    public record Command(Model Model, string Slug) : IRequest<CommentEnvelope>;

    public record Model(CommentData Comment) : IRequest<CommentEnvelope>;

    public class CommandValidator : AbstractValidator<Command>
    {
        public CommandValidator()
        {
            RuleFor(x => x.Model.Comment.Body).NotEmpty();
        }
    }

    public class Handler(
        ConduitContext context,
        ICurrentUserAccessor currentUserAccessor,
        IMessageBus messageBus
    ) : IRequestHandler<Command, CommentEnvelope>
    {
        public async Task<CommentEnvelope> Handle(
            Command message,
            CancellationToken cancellationToken
        )
        {
            var article = await context
                .Articles.Include(x => x.Comments)
                .FirstOrDefaultAsync(x => x.Slug == message.Slug, cancellationToken);

            if (article == null)
            {
                throw new RestException(
                    HttpStatusCode.NotFound,
                    new { Article = Constants.NOT_FOUND }
                );
            }

            var author = await context.Persons.FirstAsync(
                x => x.Username == currentUserAccessor.GetCurrentUsername(),
                cancellationToken
            );

            var comment = new Comment
            {
                Author = author,
                Body = message.Model.Comment.Body ?? string.Empty,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow,
                Status = "pending",
            };

            await context.Comments.AddAsync(comment, cancellationToken);
            article.Comments.Add(comment);

            await context.SaveChangesAsync(cancellationToken);

            // 🔥 EVENT-DRIVEN PART (5.1)
            await messageBus.PublishAsync(
                new CommentCreatedEvent
                {
                    CommentId = comment.CommentId,
                    Content = comment.Body,
                    ArticleId = article.ArticleId,
                }
            );

            return new CommentEnvelope(comment);
        }
    }
}

using System.Threading;
using System.Threading.Tasks;
using Turbo.Pipeline.Abstractions.Registry;
using Turbo.Primitives;

namespace Turbo.Messaging.Abstractions.Registry;

public interface IMessageHandler<TMessage>
    where TMessage : IMessageEvent
{
    Task HandleAsync(TMessage interaction, MessageContext ctx, CancellationToken ct);
}

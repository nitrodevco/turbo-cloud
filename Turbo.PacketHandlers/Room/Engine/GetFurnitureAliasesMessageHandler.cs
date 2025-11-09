using System.Threading;
using System.Threading.Tasks;
using Turbo.Messages.Registry;
using Turbo.Primitives.Messages.Incoming.Room.Engine;
using Turbo.Primitives.Messages.Outgoing.Room.Engine;

namespace Turbo.PacketHandlers.Room.Engine;

public class GetFurnitureAliasesMessageHandler : IMessageHandler<GetFurnitureAliasesMessage>
{
    public async ValueTask HandleAsync(
        GetFurnitureAliasesMessage message,
        MessageContext ctx,
        CancellationToken ct
    )
    {
        await ctx
            .Session.SendComposerAsync(new FurnitureAliasesMessageComposer { Aliases = [] }, ct)
            .ConfigureAwait(false);
    }
}

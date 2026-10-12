using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Options;
using Turbo.Messages.Registry;
using Turbo.Primitives.Messages.Incoming.Room.Engine;
using Turbo.Primitives.Messages.Outgoing.Room.Engine;
using Turbo.Rooms.Configuration;

namespace Turbo.PacketHandlers.Room.Engine;

/// <summary>The hotel's furniture aliases (<c>Turbo:Rooms:FurnitureAliases</c>), as Habbo sends its own.</summary>
public class GetFurnitureAliasesMessageHandler(IOptions<RoomConfig> roomConfig)
    : IMessageHandler<GetFurnitureAliasesMessage>
{
    private readonly RoomConfig _roomConfig = roomConfig.Value;

    public async ValueTask HandleAsync(
        GetFurnitureAliasesMessage message,
        MessageContext ctx,
        CancellationToken ct
    )
    {
        await ctx.SendComposerAsync(
                new FurnitureAliasesMessageComposer
                {
                    Aliases = [.. _roomConfig.FurnitureAliases.Select(x => (x.Key, x.Value))],
                },
                ct
            )
            .ConfigureAwait(false);
    }
}

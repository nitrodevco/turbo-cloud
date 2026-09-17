using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Turbo.Messages.Registry;
using Turbo.Primitives.Messages.Incoming.Navigator;
using Turbo.Primitives.Messages.Outgoing.Navigator;

namespace Turbo.PacketHandlers.Navigator;

public class ConvertGlobalRoomIdMessageHandler : IMessageHandler<ConvertGlobalRoomIdMessage>
{
    private const int MAX_GLOBAL_ID_LENGTH = 64;
    private const int MAX_ROOM_ID_DIGITS = 9;

    public async ValueTask HandleAsync(
        ConvertGlobalRoomIdMessage message,
        MessageContext ctx,
        CancellationToken ct
    )
    {
        if (ctx.PlayerId <= 0)
            return;

        // Global ids carry a prefix in front of the flat id (for example "r123").
        var globalId =
            message.FlatId.Length <= MAX_GLOBAL_ID_LENGTH ? message.FlatId : string.Empty;
        var digits = new string(
            globalId.Where(char.IsAsciiDigit).Take(MAX_ROOM_ID_DIGITS).ToArray()
        );
        var convertedId = int.TryParse(
            digits,
            NumberStyles.None,
            CultureInfo.InvariantCulture,
            out var id
        )
            ? id
            : -1;

        await ctx.SendComposerAsync(
                new ConvertedRoomIdMessageComposer
                {
                    GlobalId = globalId,
                    ConvertedId = convertedId,
                },
                ct
            )
            .ConfigureAwait(false);
    }
}

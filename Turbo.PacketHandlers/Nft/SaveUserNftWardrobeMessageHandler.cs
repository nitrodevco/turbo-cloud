using System.Threading;
using System.Threading.Tasks;
using Turbo.Messages.Registry;
using Turbo.Primitives.Messages.Incoming.Nft;

namespace Turbo.PacketHandlers.Nft;

/// <summary>
/// Wear an NFT outfit. The hotel has no NFT outfits, so no id can name one the player owns and
/// nothing changes; the editor asks for the selection again by itself after saving.
/// </summary>
public class SaveUserNftWardrobeMessageHandler : IMessageHandler<SaveUserNftWardrobeMessage>
{
    public ValueTask HandleAsync(
        SaveUserNftWardrobeMessage message,
        MessageContext ctx,
        CancellationToken ct
    ) => ValueTask.CompletedTask;
}

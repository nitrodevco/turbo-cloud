using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Turbo.Primitives.Messages.Outgoing.FriendList;
using Turbo.Primitives.Players.Messenger;
using Turbo.Primitives.Players.Snapshots.Messenger;

namespace Turbo.Players.Grains;

internal sealed partial class PlayerPresenceGrain
{
    private Task FlushMessengerUpdatesAsync(
        List<MessengerCategoryDto> categories,
        List<MessengerUpdateSnapshot> updates,
        CancellationToken ct
    ) =>
        SendComposerAsync(
            new FriendListUpdateMessageComposer { Categories = categories, Updates = updates },
            ct
        );
}

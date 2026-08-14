using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Turbo.Primitives.Players.Enums.Messenger;
using Turbo.Primitives.Players.Messenger;
using Turbo.Primitives.Players.Snapshots.Messenger;

namespace Turbo.Primitives.Players.Grains;

public partial interface IPlayerPresenceGrain
{
    public Task OnInitMessengerAsync(CancellationToken ct);
    public Task FlushMessengerUpdatesAsync(
        List<MessengerCategoryDto> categories,
        List<MessengerUpdateSnapshot> updates,
        CancellationToken ct
    );
    public Task OnReceiveFriendRequestAsync(MessengerRequestDto requestDto, CancellationToken ct);

    public Task OnBlockPlayerUpdatedAsync(PlayerId playerId, int result, CancellationToken ct);

    public Task OnIgnorePlayerUpdatedAsync(
        PlayerId playerId,
        MessengerIgnoreResultType result,
        CancellationToken ct
    );
    public Task OnIgnoredUpdatedAsync(List<PlayerId> ignoredPlayerIds, CancellationToken ct);
}

using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Orleans.Concurrency;
using Turbo.Primitives.Players.Enums.Messenger;
using Turbo.Primitives.Players.Messenger;
using Turbo.Primitives.Players.Snapshots.Messenger;

namespace Turbo.Primitives.Players.Grains;

public partial interface IPlayerPresenceGrain
{
    public Task OnInitMessengerAsync(CancellationToken ct);

    [AlwaysInterleave]
    public Task FlushMessengerUpdatesAsync(
        List<MessengerCategoryDto> categories,
        List<MessengerUpdateSnapshot> updates,
        CancellationToken ct
    );

    [AlwaysInterleave]
    public Task OnReceiveFriendRequestAsync(MessengerRequestDto requestDto, CancellationToken ct);

    [AlwaysInterleave]
    public Task OnBlockPlayerUpdatedAsync(PlayerId playerId, int result, CancellationToken ct);

    [AlwaysInterleave]
    public Task OnIgnorePlayerUpdatedAsync(
        PlayerId playerId,
        MessengerIgnoreResultType result,
        CancellationToken ct
    );

    [AlwaysInterleave]
    public Task OnIgnoredUpdatedAsync(List<PlayerId> ignoredPlayerIds, CancellationToken ct);
}

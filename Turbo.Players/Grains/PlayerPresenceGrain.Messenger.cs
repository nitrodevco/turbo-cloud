using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Turbo.Primitives.Messages.Outgoing.FriendList;
using Turbo.Primitives.Messages.Outgoing.Users;
using Turbo.Primitives.Orleans;
using Turbo.Primitives.Players;
using Turbo.Primitives.Players.Enums.Messenger;
using Turbo.Primitives.Players.Messenger;
using Turbo.Primitives.Players.Snapshots.Messenger;

namespace Turbo.Players.Grains;

internal sealed partial class PlayerPresenceGrain
{
    public async Task OnInitMessengerAsync(CancellationToken ct)
    {
        var messengerGrain = _grainFactory.GetPlayerMessengerGrain(_state.PlayerId);
        var categories = await messengerGrain.GetCategoriesAsync(ct);

        await SendComposerAsync(
            new MessengerInitMessageComposer
            {
                UserFriendLimit = _playerConfig.MessengerUserFriendLimit,
                NormalFriendLimit = _playerConfig.MessengerNormalFriendLimit,
                ExtendedFriendLimit = _playerConfig.MessengerExtendedFriendLimit,
                FriendCategories = categories,
            }
        );

        var friends = await messengerGrain.GetFriendsAsync(ct);
        var fragmentSize = 100;
        var totalFragments =
            friends.Count == 0 ? 1 : (friends.Count + fragmentSize - 1) / fragmentSize;

        for (var i = 0; i < totalFragments; i++)
        {
            var fragment = friends.GetRange(
                i * fragmentSize,
                Math.Min(fragmentSize, friends.Count - i * fragmentSize)
            );

            await SendComposerAsync(
                new FriendListFragmentMessageComposer
                {
                    TotalFragments = totalFragments,
                    FragmentIndex = i,
                    Fragment = fragment,
                }
            );
        }

        // deliver offline messages?
        // await messengerGrain.DeliverOfflineMessagesAsync(ct).ConfigureAwait(false);
    }

    public Task FlushMessengerUpdatesAsync(
        List<MessengerCategoryDto> categories,
        List<MessengerUpdateSnapshot> updates,
        CancellationToken ct
    ) =>
        SendComposerAsync(
            new FriendListUpdateMessageComposer { Categories = categories, Updates = updates }
        );

    public Task OnReceiveFriendRequestAsync(MessengerRequestDto requestDto, CancellationToken ct) =>
        SendComposerAsync(new NewFriendRequestMessageComposer { Request = requestDto });

    public Task OnBlockPlayerUpdatedAsync(PlayerId playerId, int result, CancellationToken ct) =>
        SendComposerAsync(
            new BlockUserUpdateMessageComposer { Result = result, UserId = playerId }
        );

    public Task OnIgnorePlayerUpdatedAsync(
        PlayerId playerId,
        MessengerIgnoreResultType result,
        CancellationToken ct
    ) =>
        SendComposerAsync(
            new IgnoreResultMessageComposer { Result = result, IgnoredUserId = playerId }
        );

    public Task OnIgnoredUpdatedAsync(List<PlayerId> ignoredPlayerIds, CancellationToken ct) =>
        SendComposerAsync(new IgnoredUsersMessageComposer { IgnoredUserIds = ignoredPlayerIds });
}

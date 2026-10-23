using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Orleans;
using Turbo.Primitives.Messages.Outgoing.FriendList;
using Turbo.Primitives.Networking;
using Turbo.Primitives.Orleans;
using Turbo.Primitives.Players;
using Turbo.Primitives.Players.Enums.Messenger;
using Turbo.Primitives.Players.Messenger;
using Turbo.Primitives.Quests;
using Turbo.Primitives.Rooms;

namespace Turbo.Players.Messenger;

/// <summary>
/// The friend list and messenger packets' one entry point (see <see cref="IMessengerService"/>).
/// It holds no state: friends, requests, conversations and group chats are the player's
/// messenger grain's, and this class resolves names, picks the grain calls and turns their
/// answers into the replies the Flash client reads.
/// </summary>
public sealed class MessengerService(
    IGrainFactory grainFactory,
    IRoomService roomService,
    ILogger<IMessengerService> logger
) : IMessengerService
{
    private readonly IGrainFactory _grainFactory = grainFactory;
    private readonly IRoomService _roomService = roomService;
    private readonly ILogger<IMessengerService> _logger = logger;

    public Task SendInitAsync(PlayerId playerId, CancellationToken ct) =>
        _grainFactory.GetPlayerMessengerGrain(playerId).SendInitAsync(ct);

    public async Task<IComposer> GetFriendRequestsAsync(PlayerId playerId, CancellationToken ct)
    {
        var requests = await _grainFactory
            .GetPlayerMessengerGrain(playerId)
            .GetRequestsAsync(ct)
            .ConfigureAwait(false);

        return new FriendRequestsMessageComposer { Requests = requests };
    }

    public async Task<IComposer?> GetFriendListUpdateAsync(PlayerId playerId, CancellationToken ct)
    {
        var messenger = _grainFactory.GetPlayerMessengerGrain(playerId);
        var updates = await messenger.GetPendingUpdatesAsync(ct).ConfigureAwait(false);

        // The messenger pushes its changes as they happen, so the poll is usually answered
        // already; an empty update would only make the client redraw for nothing.
        if (updates.Count == 0)
            return null;

        var categories = await messenger.GetCategoriesAsync(ct).ConfigureAwait(false);

        return new FriendListUpdateMessageComposer { Categories = categories, Updates = updates };
    }

    public async Task<IComposer?> RequestFriendAsync(
        PlayerId playerId,
        string targetName,
        CancellationToken ct
    )
    {
        var targetId = await _grainFactory
            .GetPlayerDirectoryGrain()
            .GetPlayerIdAsync(targetName, ct)
            .ConfigureAwait(false);

        if (targetId is not PlayerId target)
            return null;

        var result = await _grainFactory
            .GetPlayerMessengerGrain(playerId)
            .SendFriendRequestAsync(target, ct)
            .ConfigureAwait(false);

        if (result.Success)
            _grainFactory.CountRewardTrackAction(
                playerId,
                RewardTrackActionTypes.REQUEST_FRIEND,
                _logger
            );

        // A request that already stands, either way, or one to yourself fails with no error:
        // the client has no text for code 0 (HabboFriendList.showAlertView), so it is dropped.
        if (
            result.Success
            || result.ErrorType is not { } errorType
            || errorType == FriendListErrorCodeType.None
        )
            return null;

        return new MessengerErrorMessageComposer { ClientMessageId = 0, ErrorCode = errorType };
    }

    public async Task<IComposer?> AcceptFriendRequestsAsync(
        PlayerId playerId,
        List<int> requesterIds,
        CancellationToken ct
    )
    {
        var failures = await _grainFactory
            .GetPlayerMessengerGrain(playerId)
            .AcceptFriendRequestsAsync(requesterIds, ct)
            .ConfigureAwait(false);

        return failures.Count == 0
            ? null
            : new AcceptFriendResultMessageComposer { Failures = failures };
    }

    public Task DeclineFriendRequestsAsync(
        PlayerId playerId,
        List<PlayerId> requesterIds,
        bool declineAll,
        CancellationToken ct
    ) =>
        _grainFactory
            .GetPlayerMessengerGrain(playerId)
            .DeclineFriendRequestsAsync(requesterIds, declineAll, ct);

    public Task RemoveFriendsAsync(
        PlayerId playerId,
        List<PlayerId> friendIds,
        CancellationToken ct
    ) => _grainFactory.GetPlayerMessengerGrain(playerId).RemoveFriendsAsync(friendIds, ct);

    public async Task SetRelationshipStatusAsync(
        PlayerId playerId,
        PlayerId friendId,
        int relationType,
        CancellationToken ct
    )
    {
        var changed = await _grainFactory
            .GetPlayerMessengerGrain(playerId)
            .SetRelationshipStatusAsync(friendId, (MessengerFriendRelationType)relationType, ct)
            .ConfigureAwait(false);

        // Setting one, not clearing it (relation 0, "none").
        if (changed && relationType != (int)MessengerFriendRelationType.Zero)
            _grainFactory.CountRewardTrackAction(
                playerId,
                RewardTrackActionTypes.SET_RELATIONSHIP_STATUS,
                _logger
            );
    }

    public async Task<IComposer> SearchAsync(PlayerId playerId, string query, CancellationToken ct)
    {
        var (friends, others) = await _grainFactory
            .GetPlayerMessengerGrain(playerId)
            .SearchPlayersAsync(query, ct)
            .ConfigureAwait(false);

        return new HabboSearchResultMessageComposer { Friends = friends, Others = others };
    }

    public async Task<IComposer?> SendMessageAsync(
        PlayerId playerId,
        int chatId,
        string message,
        int confirmationId,
        CancellationToken ct
    )
    {
        // Each copy of the message carries the sender's name and figure as they are now.
        var sender = await _grainFactory
            .GetPlayerGrain(playerId)
            .GetSummaryAsync(ct)
            .ConfigureAwait(false);

        var error = await _grainFactory
            .GetPlayerMessengerGrain(playerId)
            .SendMessageAsync(chatId, message, confirmationId, sender.Name, sender.Figure, ct)
            .ConfigureAwait(false);

        if (error is null && message.Length > 0)
            _grainFactory.CountRewardTrackAction(
                playerId,
                RewardTrackActionTypes.SEND_MESSENGER_MESSAGE,
                _logger
            );

        // MainView.onInstantMessageError shows the text after the error's own words.
        return error is { } errorCode
            ? new InstantMessageErrorMessageComposer
            {
                ErrorCode = errorCode,
                PlayerId = chatId,
                Message = message,
            }
            : null;
    }

    public async Task<IComposer?> SendHabbiconAsync(
        PlayerId playerId,
        int chatId,
        int habbiconId,
        int confirmationId,
        CancellationToken ct
    )
    {
        var habbicons = _grainFactory.GetPlayerHabbiconGrain(playerId);

        if (!await habbicons.CanUseAsync(habbiconId, ct).ConfigureAwait(false))
            return null;

        var sender = await _grainFactory
            .GetPlayerGrain(playerId)
            .GetSummaryAsync(ct)
            .ConfigureAwait(false);

        var error = await _grainFactory
            .GetPlayerMessengerGrain(playerId)
            .SendHabbiconAsync(chatId, habbiconId, confirmationId, sender.Name, sender.Figure, ct)
            .ConfigureAwait(false);

        if (error is { } errorCode)
            return new InstantMessageErrorMessageComposer
            {
                ErrorCode = errorCode,
                PlayerId = chatId,
                Message = string.Empty,
            };

        // MainView.onHabbiconSelected: a sent Habbicon goes to the front of the recent ones.
        await habbicons.NoteUsedAsync(habbiconId, ct).ConfigureAwait(false);

        return null;
    }

    public async Task<IComposer?> GetMessageHistoryAsync(
        PlayerId playerId,
        int chatId,
        string beforeMessageId,
        CancellationToken ct
    )
    {
        var history = await _grainFactory
            .GetPlayerMessengerGrain(playerId)
            .GetMessageHistoryAsync(chatId, beforeMessageId, ct)
            .ConfigureAwait(false);

        // Habbo answers every page, an empty one too (official client capture hc-rights-test,
        // the page before the oldest message: no messages); MainView.loadMessageHistory adds
        // nothing from it.
        return new ConsoleMessageHistoryMessageComposer { ChatId = chatId, Messages = history };
    }

    public async Task<IComposer?> SendRoomInviteAsync(
        PlayerId playerId,
        List<int> friendIds,
        string message,
        CancellationToken ct
    )
    {
        if (friendIds.Count == 0)
            return null;

        var failed = await _grainFactory
            .GetPlayerMessengerGrain(playerId)
            .SendRoomInviteAsync([.. friendIds.Select(PlayerId.Parse)], message, ct)
            .ConfigureAwait(false);

        // One invite, however many friends it reached; none reached is no invite.
        if (failed.Count < friendIds.Count)
            _grainFactory.CountRewardTrackAction(
                playerId,
                RewardTrackActionTypes.SEND_MESSENGER_INVITE,
                _logger
            );

        // HabboFriendList.onRoomInviteError lists who was not reached.
        return failed.Count == 0
            ? null
            : new RoomInviteErrorMessageComposer
            {
                ErrorCode = RoomInviteErrorCodeType.RecipientsFailed,
                FailedRecipients = [.. failed.Select(x => x.Value)],
            };
    }

    public Task<List<MessengerCategoryDto>> GetCategoriesAsync(
        PlayerId playerId,
        CancellationToken ct
    ) => _grainFactory.GetPlayerMessengerGrain(playerId).GetCategoriesAsync(ct);

    public Task<(
        MessengerCategoryResultType Result,
        MessengerCategoryDto? Category
    )> CreateCategoryAsync(PlayerId playerId, string name, CancellationToken ct) =>
        _grainFactory.GetPlayerMessengerGrain(playerId).CreateCategoryAsync(name, ct);

    public Task<MessengerCategoryResultType> RenameCategoryAsync(
        PlayerId playerId,
        int categoryId,
        string name,
        CancellationToken ct
    ) => _grainFactory.GetPlayerMessengerGrain(playerId).RenameCategoryAsync(categoryId, name, ct);

    public Task<MessengerCategoryResultType> DeleteCategoryAsync(
        PlayerId playerId,
        int categoryId,
        CancellationToken ct
    ) => _grainFactory.GetPlayerMessengerGrain(playerId).DeleteCategoryAsync(categoryId, ct);

    public Task<MessengerCategoryResultType> SetFriendCategoryAsync(
        PlayerId playerId,
        PlayerId friendId,
        int categoryId,
        CancellationToken ct
    ) =>
        _grainFactory
            .GetPlayerMessengerGrain(playerId)
            .SetFriendCategoryAsync(friendId, categoryId, ct);

    public async Task SetFollowingAllowedAsync(
        PlayerId playerId,
        bool followingAllowed,
        CancellationToken ct
    )
    {
        await _grainFactory
            .GetPlayerSettingsGrain(playerId)
            .SetFollowingDisabledAsync(!followingAllowed, ct)
            .ConfigureAwait(false);
        await _grainFactory
            .GetPlayerMessengerGrain(playerId)
            .UpdateFriendsFollowingAsync(followingAllowed, ct)
            .ConfigureAwait(false);
    }

    public async Task<IComposer?> FollowFriendAsync(
        PlayerId playerId,
        PlayerId friendId,
        CancellationToken ct
    )
    {
        var errorCode =
            friendId <= 0
                ? FollowFriendErrorCodeType.NotFriend
                : await _roomService
                    .FollowFriendAsync(playerId, friendId, ct)
                    .ConfigureAwait(false);

        if (errorCode is null)
            _grainFactory.CountRewardTrackAction(
                playerId,
                RewardTrackActionTypes.FOLLOW_FRIEND,
                _logger
            );

        return errorCode is { } error
            ? new FollowFriendFailedMessageComposer { ErrorCode = error }
            : null;
    }

    public async Task VisitUserAsync(PlayerId playerId, string targetName, CancellationToken ct)
    {
        var targetId = await _grainFactory
            .GetPlayerDirectoryGrain()
            .GetPlayerIdAsync(targetName, ct)
            .ConfigureAwait(false);

        if (targetId is not PlayerId target || target == playerId)
            return;

        var activeRoom = await _grainFactory
            .GetPlayerPresenceGrain(target)
            .GetActiveRoomAsync(ct)
            .ConfigureAwait(false);

        // The forward only names the room: the client then enters it the normal way, so its
        // door, password and bans still apply.
        if (activeRoom.RoomId <= 0)
            return;

        await _grainFactory
            .ForwardPlayerToRoomAsync(playerId, activeRoom.RoomId, ct)
            .ConfigureAwait(false);
    }

    public async Task FindNewFriendsAsync(PlayerId playerId, CancellationToken ct)
    {
        var roomId = await _grainFactory
            .GetRoomDirectoryGrain()
            .GetFriendFindingRoomAsync(playerId, ct)
            .ConfigureAwait(false);
        var found = roomId is { } room && room > 0;

        // Both go out on the player's own queue, so the result is shown before the forward,
        // as HabboFriendBarView.onFindFriendsNotification expects.
        await _grainFactory
            .SendComposerToPlayerAsync(
                playerId,
                new FindFriendsProcessResultMessageComposer { Success = found },
                ct
            )
            .ConfigureAwait(false);

        if (!found)
        {
            _logger.LogDebug("No populated room to send player {PlayerId} to", playerId);

            return;
        }

        await _grainFactory
            .ForwardPlayerToRoomAsync(playerId, roomId!.Value, ct)
            .ConfigureAwait(false);
    }
}

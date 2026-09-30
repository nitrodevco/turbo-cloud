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

    public Task SetRelationshipStatusAsync(
        PlayerId playerId,
        PlayerId friendId,
        int relationType,
        CancellationToken ct
    ) =>
        _grainFactory
            .GetPlayerMessengerGrain(playerId)
            .SetRelationshipStatusAsync(friendId, (MessengerFriendRelationType)relationType, ct);

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

        // An empty page is not sent: MainView.loadMessageHistory has nothing to add.
        return history.Count == 0
            ? null
            : new ConsoleMessageHistoryMessageComposer { ChatId = chatId, Messages = history };
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

        // HabboFriendList.onRoomInviteError lists who was not reached.
        return failed.Count == 0
            ? null
            : new RoomInviteErrorMessageComposer
            {
                ErrorCode = RoomInviteErrorCodeType.RecipientsFailed,
                FailedRecipients = [.. failed.Select(x => x.Value)],
            };
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
            .GetRandomPopulatedRoomAsync(ct)
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

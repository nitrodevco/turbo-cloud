using System.Collections.Generic;
using System.Collections.Immutable;
using Turbo.Primitives.Messages.Outgoing.Notifications;
using Turbo.Primitives.Orleans;
using Turbo.Rooms.Configuration;
using Turbo.Rooms.Wired;

namespace Turbo.Rooms.Grains.Systems;

public sealed partial class RoomWiredSystem
{
    private const string ERROR_NOTICE_TYPE = "wired.error";

    // The monitor errors Habbo has a bubble text for: room.error.wired.<name> (official texts).
    private static readonly ImmutableDictionary<WiredErrorKind, string> ERROR_NOTICE_TEXTS =
        new Dictionary<WiredErrorKind, string>
        {
            [WiredErrorKind.ExecutionCap] = "${room.error.wired.execution_cap}",
            [WiredErrorKind.DelayedEventsCap] = "${room.error.wired.delayed_events_cap}",
            [WiredErrorKind.ExecutorOverload] = "${room.error.wired.executor_overload}",
            [WiredErrorKind.MarkedAsHeavy] = "${room.error.wired.marked_as_heavy}",
            [WiredErrorKind.TooManyVariables] = "${room.error.wired.too_many_variables}",
            [WiredErrorKind.TransactionFailure] = "${room.error.wired.transaction_failure}",
        }.ToImmutableDictionary();

    private readonly long[] _errorNoticeLastMs = new long[WiredErrors.All.Length];
    private readonly bool[] _errorNoticeSent = new bool[WiredErrors.All.Length];

    private bool ErrorNoticeCoolingDown(WiredErrorKind kind, long now) =>
        _errorNoticeSent[(int)kind]
        && now - _errorNoticeLastMs[(int)kind] < _roomGrain._wiredConfig.ErrorNoticeCooldownMs;

    /// <summary>
    /// The <c>wired.error</c> bubble for a monitor error that has a <c>room.error.wired.*</c> text,
    /// to whom <see cref="WiredConfig.ErrorNoticeRecipients"/> says, at most once per
    /// <see cref="WiredConfig.ErrorNoticeCooldownMs"/> for each error.
    /// </summary>
    private void SendErrorNotice(WiredErrorKind kind, long now)
    {
        var config = _roomGrain._wiredConfig;

        if (!ERROR_NOTICE_TEXTS.TryGetValue(kind, out var message))
            return;

        if (ErrorNoticeCoolingDown(kind, now))
            return;

        var i = (int)kind;
        _errorNoticeSent[i] = true;
        _errorNoticeLastMs[i] = now;

        if (config.ErrorNoticeRecipients == WiredErrorNoticeRecipients.None)
            return;

        var parameters = ImmutableDictionary<string, string>.Empty.Add("message", message);

        if (config.ErrorNoticeImage.Length > 0)
            parameters = parameters.Add("image", config.ErrorNoticeImage);

        var notice = new NotificationDialogMessageComposer
        {
            NotificationType = ERROR_NOTICE_TYPE,
            Parameters = parameters,
        };

        if (config.ErrorNoticeRecipients == WiredErrorNoticeRecipients.Room)
        {
            _roomGrain.SendComposerToRoomAndForget(notice);

            return;
        }

        if (_roomGrain._state.RoomSnapshot is not { } room)
            return;

        _roomGrain
            ._grainFactory.SendComposerToPlayerAsync(room.OwnerId, notice, default)
            .LogAndForget(
                _roomGrain._logger,
                "send a wired error notice to the owner of room {RoomId}",
                _roomGrain.RoomId
            );
    }
}

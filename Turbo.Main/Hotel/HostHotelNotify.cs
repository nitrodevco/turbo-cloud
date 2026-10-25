using System.Collections.Generic;
using System.Collections.Immutable;
using System.Threading;
using System.Threading.Tasks;
using Orleans;
using Turbo.Contracts.Hotel;
using Turbo.Events;
using Turbo.Primitives.Messages.Outgoing.Notifications;
using Turbo.Primitives.Orleans;
using Turbo.Primitives.Players;
using Turbo.Primitives.Players.Notifications;
using Turbo.Primitives.Plugins.Events;

namespace Turbo.Main.Hotel;

/// <summary>
/// <see cref="IHotelNotify"/> over the player notice service (the alert window, its text from the
/// hotel's texts), the client's notification dialog and the event system's plugin notices.
/// </summary>
internal sealed class HostHotelNotify(
    IGrainFactory grains,
    IPlayerNoticeService notices,
    EventSystem events
) : IHotelNotify
{
    public async Task<bool> AlertAsync(
        PlayerId player,
        string textKey,
        string defaultText,
        IReadOnlyList<string> parameters,
        CancellationToken ct
    ) =>
        await notices.SendAsync(player, textKey, defaultText, parameters, ct).ConfigureAwait(false)
        == PlayerNoticeDelivery.Sent;

    public Task<bool> NotificationAsync(
        PlayerId player,
        string type,
        IReadOnlyDictionary<string, string> parameters,
        CancellationToken ct
    ) =>
        grains.TrySendComposerToPlayerAsync(
            player,
            new NotificationDialogMessageComposer
            {
                NotificationType = type,
                Parameters = parameters.ToImmutableDictionary(),
            },
            ct
        );

    public Task StaffNoticeAsync(PluginNoticeEvent notice, CancellationToken ct) =>
        events.PublishAsync(notice, ct);
}

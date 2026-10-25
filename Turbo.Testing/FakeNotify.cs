using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Turbo.Contracts.Hotel;
using Turbo.Primitives.Players;
using Turbo.Primitives.Plugins.Events;

namespace Turbo.Testing;

/// <summary>An alert a player was shown: its text key, and its text as the hotel's default fills it.</summary>
public sealed record FakeAlert(PlayerId Player, string TextKey, string Text);

/// <summary>A client notification a player was sent.</summary>
public sealed record FakeNotification(
    PlayerId Player,
    string Type,
    IReadOnlyDictionary<string, string> Parameters
);

/// <summary>The notices of a <see cref="FakeHotel"/>: what players were shown (only while online) and what staff were told.</summary>
public sealed class FakeNotify : IHotelNotify
{
    private readonly FakeHotel _hotel;
    private readonly List<FakeAlert> _alerts = [];
    private readonly List<FakeNotification> _notifications = [];
    private readonly List<PluginNoticeEvent> _staff = [];

    internal FakeNotify(FakeHotel hotel) => _hotel = hotel;

    public IReadOnlyList<FakeAlert> Alerts => _alerts;

    public IReadOnlyList<FakeNotification> Notifications => _notifications;

    public IReadOnlyList<PluginNoticeEvent> StaffNotices => _staff;

    public Task<bool> AlertAsync(
        PlayerId player,
        string textKey,
        string defaultText,
        IReadOnlyList<string> parameters,
        CancellationToken ct
    )
    {
        if (!_hotel.Players.IsOnline(player))
            return Task.FromResult(false);

        var text = defaultText;

        for (var i = 0; i < parameters.Count; i++)
            text = text.Replace($"%{i}%", parameters[i], System.StringComparison.Ordinal);

        _alerts.Add(new(player, textKey, text));

        return Task.FromResult(true);
    }

    public Task<bool> NotificationAsync(
        PlayerId player,
        string type,
        IReadOnlyDictionary<string, string> parameters,
        CancellationToken ct
    )
    {
        if (!_hotel.Players.IsOnline(player))
            return Task.FromResult(false);

        _notifications.Add(new(player, type, parameters));

        return Task.FromResult(true);
    }

    public Task StaffNoticeAsync(PluginNoticeEvent notice, CancellationToken ct)
    {
        _staff.Add(notice);

        return Task.CompletedTask;
    }

    /// <summary>The last alert of that text key the player was shown; fails when there is none.</summary>
    public FakeAlert AssertAlerted(PlayerId player, string textKey) =>
        _alerts.LastOrDefault(x => x.Player == player && x.TextKey == textKey)
        ?? throw new HotelAssertionException(
            $"Expected player {player} alerted with {textKey}; they were shown {FakeHotelText.List(_alerts.Where(x => x.Player == player).Select(x => x.TextKey))}."
        );

    public void AssertNotAlerted(PlayerId player)
    {
        if (_alerts.Any(x => x.Player == player))
            throw new HotelAssertionException(
                $"Expected no alert for player {player}; they were shown {FakeHotelText.List(_alerts.Where(x => x.Player == player).Select(x => x.TextKey))}."
            );
    }
}

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Turbo.Commands;
using Turbo.Database.Entities.Room;
using Turbo.Primitives.Commands;
using Turbo.Primitives.Players;
using Turbo.Primitives.Players.Notifications;
using Turbo.Primitives.Players.Snapshots;
using Turbo.Primitives.Rooms;
using Turbo.Primitives.Texts;

namespace Turbo.Testing;

/// <summary>
/// A player who typed an operator command in a <see cref="TestRoom"/>, as core's
/// <c>PlayerOperatorExecutor</c> is: their nodes as the hotel grants them, who was in the room as
/// they typed it, a reply whispered in the room (a notice when they are no longer there), a
/// longer answer kept in <see cref="TestRoom.Actions"/>.
/// </summary>
internal sealed class TestOperatorExecutor(TestHotel hotel, TestPlayer player, TestRoom room)
    : IOperatorExecutor
{
    public PlayerId? PlayerId => player.Id;

    public string Name => player.Name;

    public RoomId? RoomId => room.Id;

    public IReadOnlyList<PlayerId> RoomPlayerIds { get; } = [.. room.Players.Select(p => p.Id)];

    public Task<bool> HasAsync(string node, CancellationToken ct) =>
        Task.FromResult(hotel.Grants(player.Fake, node));

    public async Task ReplyAsync(string text, CancellationToken ct)
    {
        if (await hotel.Fake.Rooms.WhisperAsync(room.Id, player.Id, text, ct).ConfigureAwait(false))
            return;

        await hotel.Fake.Notify.AlertAsync(player.Id, "command.feedback", "%0%", [text], ct)
            .ConfigureAwait(false);
    }

    public Task NoticeAsync(IReadOnlyList<string> lines, CancellationToken ct)
    {
        room.Act($"Notice({player.Name}: {string.Join(" | ", lines)})");

        return Task.CompletedTask;
    }
}

/// <summary>
/// The hotel's notices to players, as <see cref="FakeNotify"/> keeps them: a notice is an alert
/// (<see cref="FakeNotify.Alerts"/>), a persistent notification a notification, each worded by the
/// hotel's texts when they have the key. Sent to a player online, otherwise
/// <see cref="PlayerNoticeDelivery.Offline"/>.
/// </summary>
internal sealed class TestNotices(FakeHotel fake, IHotelTextProvider texts) : IPlayerNoticeService
{
    public async Task<PlayerNoticeDelivery> SendAsync(
        PlayerId playerId,
        string textKey,
        string defaultText,
        IReadOnlyList<string> parameters,
        CancellationToken ct
    ) =>
        await fake
            .Notify.AlertAsync(
                playerId,
                textKey,
                await texts.GetTextAsync(textKey, ct).ConfigureAwait(false) ?? defaultText,
                parameters,
                ct
            )
            .ConfigureAwait(false)
            ? PlayerNoticeDelivery.Sent
            : PlayerNoticeDelivery.Offline;

    public Task<PlayerNoticeDelivery> SendCurrencyRewardAsync(
        PlayerId playerId,
        long amount,
        CurrencyTypeSnapshot currency,
        CancellationToken ct
    ) =>
        SendAsync(
            playerId,
            "player.reward.currency",
            "You received %0% %1%.",
            [amount.ToString(CultureInfo.InvariantCulture), currency.Name],
            ct
        );

    public async Task<PlayerNoticeDelivery> SendPersistentAsync(
        PlayerId playerId,
        string notificationType,
        ImmutableDictionary<string, string> parameters,
        CancellationToken ct
    ) =>
        await fake
            .Notify.NotificationAsync(playerId, notificationType, parameters, ct)
            .ConfigureAwait(false)
            ? PlayerNoticeDelivery.Sent
            : PlayerNoticeDelivery.Offline;

    public Task DeliverPersistentAsync(PlayerId playerId, CancellationToken ct) =>
        Task.CompletedTask;
}

/// <summary>The hotel's texts: the live plugins' (their <c>gamedata/external_texts.txt</c>).</summary>
internal sealed class TestTexts(TestHotel hotel) : IHotelTextProvider
{
    public Task<HotelTexts> GetTextsAsync(IEnumerable<string> keys, CancellationToken ct) =>
        Task.FromResult(
            new HotelTexts(
                keys.Distinct(StringComparer.Ordinal)
                    .Select(key => (Key: key, Text: Text(key)))
                    .Where(x => x.Text is not null)
                    .ToDictionary(x => x.Key, x => x.Text!, StringComparer.Ordinal)
            )
        );

    public Task<HotelTexts> GetTextsByPrefixAsync(string prefix, CancellationToken ct) =>
        GetTextsAsync(
            hotel
                .Plugins.Values.SelectMany(p => p.TextKeys)
                .Where(key => key.StartsWith(prefix, StringComparison.Ordinal)),
            ct
        );

    public Task<string?> GetTextAsync(string key, CancellationToken ct) =>
        Task.FromResult(Text(key));

    public void Invalidate() { }

    private string? Text(string key) =>
        hotel.Plugins.Values.Select(p => p.Text(key)).FirstOrDefault(t => !string.IsNullOrEmpty(t));
}

/// <summary>The hotel's command log: each use of an operator command the hotel wrote down, in order.</summary>
public sealed class TestCommandLog : ICommandLogWriter
{
    private readonly ConcurrentQueue<CommandLogEntity> _entries = new();

    /// <summary>
    /// The uses written down: those of a player holding <c>command.log</c>, and every line that
    /// named a group of players (a selector), as on a server.
    /// </summary>
    public IReadOnlyList<CommandLogEntity> Entries => [.. _entries];

    Task ICommandLogWriter.WriteAsync(CommandLogEntity entry, CancellationToken ct)
    {
        _entries.Enqueue(entry);

        return Task.CompletedTask;
    }
}

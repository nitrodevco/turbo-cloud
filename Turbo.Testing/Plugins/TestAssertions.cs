using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Extensions.Logging;
using Turbo.Contracts.Approvals;
using Turbo.Contracts.Wired;
using Turbo.Primitives.Catalog.Enums;
using Turbo.Primitives.Commands;

namespace Turbo.Testing;

/// <summary>
/// Assertions in the game's words: <c>bob.Should().HaveReceived&lt;SkillMessage&gt;()</c>,
/// <c>hotel.Purchases.Should().HaveRefused(offer.Id)</c>, <c>room.Should().HaveFired&lt;T&gt;()</c>.
/// A failed one throws <see cref="HotelAssertionException"/> saying what there was instead, so they
/// work under any test framework.
/// </summary>
public static class TestAssertions
{
    public static PlayerAssertions Should(this TestPlayer player) => new(player);

    public static RoomAssertions Should(this TestRoom room) => new(room);

    public static PurchaseAssertions Should(this TestPurchases purchases) => new(purchases);

    public static EventAssertions Should(this TestEvents events) => new(events);

    public static LogAssertions Should(this TestLogs logs) => new(logs);

    public static ApprovalAssertions Should(this TestApprovals approvals) => new(approvals);

    internal static string List<T>(IEnumerable<T> items) =>
        items.Any() ? string.Join(", ", items.Select(x => x?.ToString())) : "nothing";
}

public sealed class PlayerAssertions(TestPlayer player)
{
    /// <summary>The player was sent a <typeparamref name="T"/> (that <paramref name="match"/> takes); the last such.</summary>
    public T HaveReceived<T>(Func<T, bool>? match = null)
    {
        var found = player.Received.OfType<T>().Where(x => match?.Invoke(x) ?? true).ToList();

        return found.Count > 0
            ? found[^1]
            : throw new HotelAssertionException(
                $"{player} was sent no {typeof(T).Name}{(match is null ? "" : " that matches")}; they were sent {TestAssertions.List(player.Received)}."
            );
    }

    /// <summary>The player was sent no <typeparamref name="T"/>.</summary>
    public PlayerAssertions NotHaveReceived<T>()
    {
        if (player.Received.OfType<T>().FirstOrDefault() is { } sent)
            throw new HotelAssertionException($"{player} was sent {sent}.");

        return this;
    }

    /// <summary>The player was whispered a line holding <paramref name="text"/> (a command's reply among them); the last such.</summary>
    public string HaveBeenWhispered(string text) =>
        player.Whispers.LastOrDefault(x => x.Contains(text, StringComparison.Ordinal))
        ?? throw new HotelAssertionException(
            $"{player} was whispered nothing holding \"{text}\"; they were whispered {TestAssertions.List(player.Whispers)}."
        );

    /// <summary>The player was whispered nothing.</summary>
    public PlayerAssertions NotHaveBeenWhispered()
    {
        if (player.Whispers.Count > 0)
            throw new HotelAssertionException(
                $"{player} was whispered {TestAssertions.List(player.Whispers)}."
            );

        return this;
    }

    /// <summary>The player was shown a client notification of <paramref name="type"/>; the last such.</summary>
    public FakeNotification HaveBeenNotified(string type) =>
        player.Notifications.LastOrDefault(x => x.Type == type)
        ?? throw new HotelAssertionException(
            $"{player} was shown no {type} notification; they were shown {TestAssertions.List(player.Notifications.Select(x => x.Type))}."
        );

    /// <summary>The player's last command came to <paramref name="outcome"/>.</summary>
    public PlayerAssertions HaveHadCommand(CommandOutcome outcome)
    {
        if (player.LastCommand != outcome)
            throw new HotelAssertionException(
                $"{player}'s last command came to {player.LastCommand?.ToString() ?? "nothing (none was run)"}, not {outcome}."
            );

        return this;
    }

    /// <summary>The player holds <paramref name="amount"/> of <paramref name="currency"/>.</summary>
    public PlayerAssertions HaveBalance(string currency, int amount)
    {
        var balance = player.Hotel.Wallet.Balance(player.Id, currency);

        if (balance != amount)
            throw new HotelAssertionException(
                $"{player} holds {balance} {currency}, not {amount}."
            );

        return this;
    }

    /// <summary>The player holds the badge <paramref name="code"/>.</summary>
    public PlayerAssertions HaveBadge(string code)
    {
        player.Hotel.Inventory.AssertHasBadge(player.Id, code);

        return this;
    }

    /// <summary>The player stands in room <paramref name="roomId"/>.</summary>
    public PlayerAssertions BeInRoom(int roomId)
    {
        if (player.Room?.Id.Value != roomId)
            throw new HotelAssertionException(
                $"{player} is in {player.Room?.ToString() ?? "no room"}, not room {roomId}."
            );

        return this;
    }
}

public sealed class RoomAssertions(TestRoom room)
{
    /// <summary>Wired fired <typeparamref name="TEvent"/> in the room (that <paramref name="match"/> takes); the last such.</summary>
    public TEvent HaveFired<TEvent>(Func<TEvent, bool>? match = null)
        where TEvent : IWiredEvent => room.Hotel.Wired.AssertFired(room.Id, match);

    /// <summary>The room's code said a line holding <paramref name="text"/>; the last such.</summary>
    public TestRoomSpeech HaveSaid(string text) =>
        room.Said.LastOrDefault(x => x.Text.Contains(text, StringComparison.Ordinal))
        ?? throw new HotelAssertionException(
            $"Nothing holding \"{text}\" was said in {room}; it heard {TestAssertions.List(room.Said.Select(x => x.Text))}."
        );

    /// <summary>A plugin sent everyone in the room a <typeparamref name="T"/>; the last such.</summary>
    public T HaveBeenSent<T>() =>
        room.Sent.OfType<T>().LastOrDefault()
        ?? throw new HotelAssertionException(
            $"{room} was sent no {typeof(T).Name}; it was sent {TestAssertions.List(room.Sent)}."
        );

    /// <summary>A plugin holds the room loaded (<c>IHotelRooms.HoldAsync</c>) and its lease has not run out.</summary>
    public RoomAssertions BeHeld()
    {
        if (!room.Hotel.Fake.Rooms.IsHeld(room.Id))
            throw new HotelAssertionException($"No plugin holds {room}.");

        return this;
    }

    /// <summary>No plugin holds the room.</summary>
    public RoomAssertions NotBeHeld()
    {
        if (room.Hotel.Fake.Rooms.IsHeld(room.Id))
            throw new HotelAssertionException($"A plugin holds {room}.");

        return this;
    }

    /// <summary>A command or furni asked the room for <paramref name="action"/> (<c>Kick(Bob)</c>, or its start).</summary>
    public RoomAssertions HaveActed(string action)
    {
        if (!room.Actions.Any(x => x.StartsWith(action, StringComparison.Ordinal)))
            throw new HotelAssertionException(
                $"{room} was asked for no {action}; it was asked for {TestAssertions.List(room.Actions)}."
            );

        return this;
    }
}

public sealed class ApprovalAssertions(TestApprovals approvals)
{
    /// <summary>A request of <paramref name="kind"/> waits for a decision; the newest such.</summary>
    public Approval HavePending(string kind)
    {
        var pending = Read(ApprovalState.Pending, kind);

        return pending.Count > 0
            ? pending[0]
            : throw new HotelAssertionException(
                $"No request of {kind} is pending; there are {Describe(Read(null, null))}."
            );
    }

    /// <summary>No request (of <paramref name="kind"/>, when given) waits for a decision.</summary>
    public ApprovalAssertions NotHavePending(string? kind = null)
    {
        var pending = Read(ApprovalState.Pending, kind);

        if (pending.Count > 0)
            throw new HotelAssertionException($"Requests are pending: {Describe(pending)}.");

        return this;
    }

    /// <summary>The newest request of <paramref name="kind"/> is <paramref name="state"/> (executed, denied, expired, ...).</summary>
    public Approval HaveBeen(string kind, ApprovalState state)
    {
        var newest =
            Read(null, kind).FirstOrDefault()
            ?? throw new HotelAssertionException($"No request of {kind} was asked for.");

        return newest.State == state
            ? newest
            : throw new HotelAssertionException(
                $"The request {newest.Id} of {kind} is {newest.State}, not {state}{(newest.Result is { } result ? $" ({result})" : "")}."
            );
    }

    // The approvals live in SQLite in memory: reading them never waits on anything.
    private IReadOnlyList<Approval> Read(ApprovalState? state, string? kind) =>
        approvals.ListAsync(state, kind, default).GetAwaiter().GetResult();

    private static string Describe(IReadOnlyList<Approval> list) =>
        TestAssertions.List(list.Select(x => $"{x.Id} {x.Kind} ({x.State})"));
}

public sealed class PurchaseAssertions(TestPurchases purchases)
{
    /// <summary>
    /// The last purchase of offer <paramref name="offerId"/> was refused: with
    /// <paramref name="error"/> when given, and a reason holding <paramref name="reason"/> when given.
    /// </summary>
    public TestPurchase HaveRefused(
        int offerId,
        CatalogPurchaseErrorType? error = null,
        string? reason = null
    )
    {
        var purchase = Last(offerId);

        if (purchase.Decision.Allowed)
            throw new HotelAssertionException($"Offer {offerId} was allowed: {purchase.Decision}.");

        if (error is { } e && purchase.Decision.Error != e)
            throw new HotelAssertionException(
                $"Offer {offerId} was refused with {purchase.Decision.Error}, not {e}."
            );

        if (
            reason is not null
            && purchase.Decision.Reason?.Contains(reason, StringComparison.Ordinal) != true
        )
            throw new HotelAssertionException(
                $"Offer {offerId} was refused for \"{purchase.Decision.Reason}\", not \"{reason}\"."
            );

        return purchase;
    }

    /// <summary>The last purchase of offer <paramref name="offerId"/> was allowed; with its decision (price and units as the policies left them).</summary>
    public TestPurchase HaveAllowed(int offerId)
    {
        var purchase = Last(offerId);

        return purchase.Decision.Allowed
            ? purchase
            : throw new HotelAssertionException(
                $"Offer {offerId} was refused: {purchase.Decision.Reason ?? purchase.Decision.Error.ToString()}."
            );
    }

    private TestPurchase Last(int offerId) =>
        purchases.Made.LastOrDefault(x => x.Request.Offer.Id == offerId)
        ?? throw new HotelAssertionException(
            $"Offer {offerId} was never bought; the purchases were {TestAssertions.List(purchases.Made.Select(x => x.Request.Offer.Id))}."
        );
}

public sealed class EventAssertions(TestEvents events)
{
    /// <summary>A <typeparamref name="T"/> was published (that <paramref name="match"/> takes); the last such.</summary>
    public T HavePublished<T>(Func<T, bool>? match = null) =>
        events.Of<T>().LastOrDefault(x => match?.Invoke(x) ?? true)
        ?? throw new HotelAssertionException(
            $"No {typeof(T).Name}{(match is null ? "" : " that matches")} was published; there were {TestAssertions.List(events.Published.Select(x => x.GetType().Name))}."
        );

    /// <summary>No <typeparamref name="T"/> was published.</summary>
    public EventAssertions NotHavePublished<T>()
    {
        if (events.Of<T>().FirstOrDefault() is { } published)
            throw new HotelAssertionException($"{published} was published.");

        return this;
    }
}

public sealed class LogAssertions(TestLogs logs)
{
    /// <summary>Nothing logged a warning or worse: a load, a policy or a handler that failed and was passed over.</summary>
    public LogAssertions HaveNoWarnings()
    {
        if (logs.AtLeast(LogLevel.Warning) is { Count: > 0 } bad)
            throw new HotelAssertionException(
                $"Logged: {string.Join(Environment.NewLine, bad.Select(x => $"{x.Level} {x.Category}: {x.Message} {x.Exception?.Message}"))}"
            );

        return this;
    }

    /// <summary>Something logged a line holding <paramref name="text"/> at <paramref name="level"/> or above.</summary>
    public TestLogEntry HaveLogged(string text, LogLevel level = LogLevel.Information) =>
        logs.AtLeast(level).LastOrDefault(x => x.Message.Contains(text, StringComparison.Ordinal))
        ?? throw new HotelAssertionException($"Nothing logged \"{text}\" at {level} or above.");
}

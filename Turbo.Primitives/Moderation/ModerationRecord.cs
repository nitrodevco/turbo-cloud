using System;
using Turbo.Primitives.Action;
using Turbo.Primitives.Commands;
using Turbo.Primitives.Moderation.Enums;
using Turbo.Primitives.Players;
using Turbo.Primitives.Rooms;

namespace Turbo.Primitives.Moderation;

/// <summary>
/// One moderation action, for <see cref="IModerationLog"/>: who did what to whom, where, why and
/// until when. Everything but the action and where it came from is optional, because not every
/// action has a target, a room or an end.
/// </summary>
public sealed record ModerationRecord
{
    public required ModerationActionType Action { get; init; }

    public required ModerationSource Source { get; init; }

    /// <summary>The player who took the action; null for the console and the hotel itself.</summary>
    public PlayerId? ActorId { get; init; }

    public PlayerId? TargetPlayerId { get; init; }

    public RoomId? RoomId { get; init; }

    public int? GuildId { get; init; }

    public string? Reason { get; init; }

    /// <summary>When what it does ends; null for an action that does not last, or lasts for good.</summary>
    public DateTime? ExpiresAt { get; init; }

    /// <summary>
    /// What the action was about, where its kind has one: a badge code, a message, a count.
    /// </summary>
    public string? Detail { get; init; }

    /// <summary>Where an action in a room came from, by who started it.</summary>
    public static ModerationSource SourceOf(ActionContext ctx) =>
        ctx.Origin switch
        {
            ActionOrigin.Player => ModerationSource.Game,
            ActionOrigin.Panel => ModerationSource.Panel,
            _ => ModerationSource.System,
        };

    /// <summary>Where a command came from, by who ran it.</summary>
    public static ModerationSource SourceOf(IOperatorExecutor executor) =>
        executor.IsConsole ? ModerationSource.Console
        : executor.Source == "panel" ? ModerationSource.Panel
        : ModerationSource.Game;

    /// <summary>The player behind an action in a room, if a player started it.</summary>
    public static PlayerId? ActorOf(ActionContext ctx) =>
        (ctx.Origin is ActionOrigin.Player or ActionOrigin.Panel) && ctx.PlayerId > 0
            ? ctx.PlayerId
            : null;
}

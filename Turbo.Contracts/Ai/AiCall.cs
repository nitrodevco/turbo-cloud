using System;
using System.Collections.Immutable;
using System.Linq;
using Turbo.Contracts.Plugins.Mcp;
using Turbo.Primitives.Players;

namespace Turbo.Contracts.Ai;

/// <summary>One call's who and how: whom the agent acts as, its memory, and overrides.</summary>
public sealed record AiCall
{
    /// <summary>
    /// The identity the agent's tools run as: a player (their permission nodes, asked at each tool
    /// call), the system with the nodes the plugin names, or nobody (no tools). An agent never
    /// holds more than its identity does.
    /// </summary>
    public AiActor As { get; init; } = AiActor.Nobody;

    /// <summary>The player the call is for: it counts against their daily tokens and rate limit. The <see cref="As"/> player when null.</summary>
    public PlayerId? For { get; init; }

    /// <summary>The conversation it goes on (and is saved to); none for a call that remembers nothing.</summary>
    public AiMemory? Memory { get; init; }

    /// <summary>Only this provider (by the hotel's name), no failover.</summary>
    public string? Provider { get; init; }

    /// <summary>The model for the first provider tried, over the agent's and the provider's.</summary>
    public string? Model { get; init; }

    /// <summary>How long one model answer may take, over the agent's and the provider's.</summary>
    public TimeSpan? Timeout { get; init; }

    /// <summary>What this run may spend, over the agent's <see cref="AiAgent.Budget"/>.</summary>
    public AiBudget? Budget { get; init; }

    /// <summary>The player whose tokens and requests it counts against, if any.</summary>
    public PlayerId? Payer => For ?? As.Player;
}

/// <summary>Who an agent's tools run as. Never more than the identity holds.</summary>
public sealed record AiActor
{
    private AiActor(PlayerId? player, ImmutableArray<string> nodes)
    {
        Player = player;
        Nodes = nodes;
    }

    /// <summary>The player it acts as; null for the system and for nobody.</summary>
    public PlayerId? Player { get; }

    /// <summary>The system's nodes (<see cref="System"/>); a player's are asked of the hotel.</summary>
    public ImmutableArray<string> Nodes { get; }

    /// <summary>No identity: no tool is offered.</summary>
    public static AiActor Nobody { get; } = new(null, []);

    /// <summary>As the player: their permission nodes now, asked at each tool call.</summary>
    public static AiActor AsPlayer(PlayerId player) => new(player, []);

    /// <summary>
    /// As the hotel itself, holding exactly <paramref name="nodes"/> (<c>*</c> all of them): a
    /// plugin names what its agent may do without a player behind it.
    /// </summary>
    public static AiActor System(params string[] nodes) =>
        new(null, [.. nodes.Where(x => !string.IsNullOrWhiteSpace(x))]);

    /// <summary>What a tool's context names as its caller: the player, or <c>PlayerId(0)</c> for the system.</summary>
    public McpCaller ToCaller() => new(Player ?? new PlayerId(0));
}

/// <summary>
/// A conversation the hotel keeps (the <c>ai_conversations</c> table), owned by one participant:
/// a player, or a key the plugin chooses (a room, a bot, a ticket). Only its owner's calls go on it.
/// </summary>
public sealed record AiMemory
{
    private AiMemory(string participant, PlayerId? player, long? id, bool fresh)
    {
        Participant = participant;
        Player = player;
        ConversationId = id;
        Fresh = fresh;
    }

    /// <summary>Who owns it: <c>player:&lt;id&gt;</c> or <c>key:&lt;key&gt;</c>.</summary>
    public string Participant { get; }

    /// <summary>The player who owns it, if a player does.</summary>
    public PlayerId? Player { get; }

    /// <summary>A conversation by its id (<see cref="AiResponse.ConversationId"/>); null for the owner's latest with this agent.</summary>
    public long? ConversationId { get; }

    /// <summary>Starts a new conversation instead of going on with the latest.</summary>
    public bool Fresh { get; }

    /// <summary>The player's latest conversation with the agent, or a new one.</summary>
    public static AiMemory ForPlayer(PlayerId player) =>
        new($"player:{player.Value}", player, null, false);

    /// <summary>The latest conversation under <paramref name="key"/> with the agent, or a new one.</summary>
    public static AiMemory ForKey(string key)
    {
        if (string.IsNullOrWhiteSpace(key) || key.Length > 120)
            throw new ArgumentException("A memory key is 1 to 120 characters.", nameof(key));

        return new($"key:{key}", null, null, false);
    }

    /// <summary>The conversation <paramref name="id"/>, if this memory's owner owns it (<see cref="ForPlayer"/> or <see cref="ForKey"/> first).</summary>
    public AiMemory Continue(long id) => new(Participant, Player, id, false);

    /// <summary>A new conversation for the same owner.</summary>
    public AiMemory New() => new(Participant, Player, null, true);

    /// <summary>The conversation <paramref name="id"/> of <paramref name="participant"/> as kept, for resuming it.</summary>
    internal static AiMemory Of(string participant, long id) =>
        new(
            participant,
            participant.StartsWith("player:", StringComparison.Ordinal)
            && int.TryParse(participant.AsSpan(7), out var player)
                ? new PlayerId(player)
                : null,
            id,
            false
        );
}

/// <summary>
/// What the model is asked: the plugin's own words, and text a player wrote, which is sent as data
/// (<c>{"playerWritten": "..."}</c>) with a note that it is never instructions. Write
/// <c>new AiPrompt("Answer the player's question.", question)</c>, never the question pasted
/// into the plugin's words.
/// </summary>
public sealed record AiPrompt(string? Text, PlayerText? PlayerWritten = null)
{
    public static implicit operator AiPrompt(string text) => new(text);

    /// <summary>A player's words alone, as data.</summary>
    public static AiPrompt FromPlayer(PlayerText text) => new(null, text);
}

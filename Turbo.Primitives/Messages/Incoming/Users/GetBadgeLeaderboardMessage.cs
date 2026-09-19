using Turbo.Primitives.Networking;

namespace Turbo.Primitives.Messages.Incoming.Users;

public record GetBadgeLeaderboardMessage : IMessageEvent
{
    public required int Type { get; init; }

    /// <summary>The tier of a rarity board, -1 otherwise.</summary>
    public required int Rarity { get; init; }

    /// <summary>The client fetches the board in chunks of several pages; this is the chunk's index.</summary>
    public required int ChunkIndex { get; init; }
    public required int ChunkSize { get; init; }
}

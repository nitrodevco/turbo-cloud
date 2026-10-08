using System.Collections.Generic;
using Turbo.Primitives.Networking;

namespace Turbo.Primitives.Messages.Incoming.Sound;

public record GetSongInfoMessage : IMessageEvent
{
    /// <summary>The songs the client has no info for yet; it batches them once a second.</summary>
    public required List<int> SongIds { get; init; }
}

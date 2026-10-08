using Turbo.Primitives.Networking;

namespace Turbo.Primitives.Messages.Incoming.Sound;

public record RemoveJukeboxDiskMessage : IMessageEvent
{
    /// <summary>The place in the playlist to empty.</summary>
    public required int Slot { get; init; }
}

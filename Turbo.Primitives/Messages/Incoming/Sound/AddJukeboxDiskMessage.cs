using Turbo.Primitives.Networking;

namespace Turbo.Primitives.Messages.Incoming.Sound;

public record AddJukeboxDiskMessage : IMessageEvent
{
    /// <summary>The song disk's item id.</summary>
    public required int DiskId { get; init; }

    /// <summary>Where in the playlist it goes; the client sends the playlist's length.</summary>
    public required int Slot { get; init; }
}

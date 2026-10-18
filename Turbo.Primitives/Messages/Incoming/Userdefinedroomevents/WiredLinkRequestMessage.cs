using Turbo.Primitives.Networking;

namespace Turbo.Primitives.Messages.Incoming.Userdefinedroomevents;

/// <summary>
/// Any request of a Room Linker's link window: get its configuration, save it, or create,
/// rename, delete or list the connections of a network. Each is its own client message;
/// <see cref="Operation"/> (a <c>WiredLinkOperations</c> value) says which one this was.
/// </summary>
public record WiredLinkRequestMessage : IMessageEvent
{
    public required int FurniId { get; init; }

    public required int Operation { get; init; }

    /// <summary>The network the request is about; "0" for none.</summary>
    public string NetworkId { get; init; } = "0";

    /// <summary>The name a network is created or renamed with.</summary>
    public string Name { get; init; } = string.Empty;

    /// <summary>On a save: whether the linker also receives link messages of its own room.</summary>
    public bool ReceiveLocal { get; init; }
}

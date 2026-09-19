using Turbo.Primitives.Networking;

namespace Turbo.Primitives.Messages.Incoming.Userdefinedroomevents.Wiredmenu;

/// <summary>
/// The wired menu edits, gives or takes away a variable on one furni or user.
/// <see cref="Operation"/> is a <c>WiredVariableMenuOperationType</c> id; it comes from a client,
/// so the handler checks it before it becomes the enum.
/// </summary>
public record WiredSetObjectVariableValueMessage : IMessageEvent
{
    public required int VariableTarget { get; init; }
    public required int ObjectIdForType { get; init; }
    public required string VariableId { get; init; }
    public required int Value { get; init; }
    public required int Operation { get; init; }
}

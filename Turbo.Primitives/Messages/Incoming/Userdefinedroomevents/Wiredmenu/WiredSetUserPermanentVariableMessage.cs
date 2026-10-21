using Turbo.Primitives.Networking;

namespace Turbo.Primitives.Messages.Incoming.Userdefinedroomevents.Wiredmenu;

/// <summary>
/// Variable Management edits, gives or takes away a variable on one user, pet or bot (AS3
/// <c>WiredSetUserPermanentVariableComposer</c>). <see cref="Operation"/> is a
/// <c>WiredVariableMenuOperationType</c> id; it comes from a client, so the handler checks it
/// before it becomes the enum.
/// </summary>
public record WiredSetUserPermanentVariableMessage : IMessageEvent
{
    public required int EntityType { get; init; }
    public required int EntityId { get; init; }
    public required string VariableId { get; init; }
    public required int Value { get; init; }
    public required int Operation { get; init; }
}

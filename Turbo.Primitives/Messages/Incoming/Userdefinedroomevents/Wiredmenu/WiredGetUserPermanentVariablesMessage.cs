using Turbo.Primitives.Networking;

namespace Turbo.Primitives.Messages.Incoming.Userdefinedroomevents.Wiredmenu;

/// <summary>
/// Variable Management asks what one user, pet or bot holds (AS3
/// <c>WiredGetUserPermanentVariablesComposer</c>: entity type 1 user, 2 pet, 4 bot, and the
/// player, pet or bot id).
/// </summary>
public record WiredGetUserPermanentVariablesMessage : IMessageEvent
{
    public required int EntityType { get; init; }
    public required int EntityId { get; init; }
}

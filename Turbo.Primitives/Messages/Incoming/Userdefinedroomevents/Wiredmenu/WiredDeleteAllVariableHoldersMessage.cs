using Turbo.Primitives.Networking;

namespace Turbo.Primitives.Messages.Incoming.Userdefinedroomevents.Wiredmenu;

/// <summary>The wired menu deletes a variable for every furni or user that holds it.</summary>
public record WiredDeleteAllVariableHoldersMessage : IMessageEvent
{
    public required string SelectedVariableId { get; init; }
}

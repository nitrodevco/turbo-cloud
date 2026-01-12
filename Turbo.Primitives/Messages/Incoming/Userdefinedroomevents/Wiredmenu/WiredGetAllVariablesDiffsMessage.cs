using Orleans;
using Turbo.Primitives.Networking;

namespace Turbo.Primitives.Messages.Incoming.Userdefinedroomevents.Wiredmenu;

[GenerateSerializer, Immutable]
public record WiredGetAllVariablesDiffsMessage : IMessageEvent { }

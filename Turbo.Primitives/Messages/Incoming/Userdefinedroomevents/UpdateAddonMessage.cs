using Orleans;
using Turbo.Primitives.Networking;

namespace Turbo.Primitives.Messages.Incoming.Userdefinedroomevents;

[GenerateSerializer, Immutable]
public record UpdateAddonMessage : UpdateWiredMessage, IMessageEvent { }

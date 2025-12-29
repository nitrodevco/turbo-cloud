using Orleans;
using Turbo.Primitives.Networking;

namespace Turbo.Primitives.Messages.Incoming.Userdefinedroomevents;

[GenerateSerializer, Immutable]
public record UpdateActionMessage : UpdateWiredMessage, IMessageEvent { }

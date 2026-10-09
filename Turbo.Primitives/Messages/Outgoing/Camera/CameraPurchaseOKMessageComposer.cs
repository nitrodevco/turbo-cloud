using Orleans;
using Turbo.Primitives.Networking;

namespace Turbo.Primitives.Messages.Outgoing.Camera;

/// <summary><c>CameraPurchaseOKMessageParser</c>: no fields.</summary>
[GenerateSerializer, Immutable]
public sealed record CameraPurchaseOKMessageComposer : IComposer;

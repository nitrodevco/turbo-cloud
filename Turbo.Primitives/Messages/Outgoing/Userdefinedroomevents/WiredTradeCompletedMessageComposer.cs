using Orleans;
using Turbo.Primitives.Networking;

namespace Turbo.Primitives.Messages.Outgoing.Userdefinedroomevents;

/// <summary>The user's wired trade went through.</summary>
[GenerateSerializer, Immutable]
public sealed record WiredTradeCompletedMessageComposer : IComposer;

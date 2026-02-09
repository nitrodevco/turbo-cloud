using Orleans;

namespace Turbo.Primitives.Messages.Outgoing.Room.Chat;

[GenerateSerializer, Immutable]
public sealed record ShoutMessageComposer : ChatMessageComposer;

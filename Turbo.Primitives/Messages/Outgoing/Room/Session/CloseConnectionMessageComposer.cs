using Orleans;
using Turbo.Contracts.Abstractions;

namespace Turbo.Primitives.Messages.Outgoing.Room.Session;

[GenerateSerializer, Immutable]
public sealed record CloseConnectionMessageComposer : IComposer;

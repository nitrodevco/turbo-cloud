using Orleans;
using Turbo.Contracts.Abstractions;

namespace Turbo.Primitives.Messages.Outgoing.Error;

[GenerateSerializer, Immutable]
public sealed record ErrorReportEventMessageComposer : IComposer
{
    // TODO: add properties if/when identified
}

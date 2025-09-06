using System;
using Turbo.Networking.Abstractions.Session;
using Turbo.Pipeline.Core.Envelope;
using Turbo.Primitives;

namespace Turbo.Messaging;

public record RawPacketEnvelope : EnvelopeBase<ReadOnlyMemory<byte>>
{
    public required ISessionContext Session { get; init; }
    public required int Header { get; init; }
}

using System.Buffers;
using SuperSocket.ProtoBase;
using Turbo.Primitives.Networking;

namespace Turbo.Networking.Package;

/// <summary>
/// Marks a session as heard from (<see cref="ISessionContext.MarkReceived"/>) when a whole
/// message comes off the wire, ahead of its handler.
/// </summary>
/// <remarks>
/// SuperSocket reads the next message while the one before it is still being handled, but
/// handles them one at a time. Marking in the handler counted a Pong only once every packet
/// queued before it had been handled, so a client whose handlers waited on slow grain calls
/// looked silent and the heartbeat closed it. Here the mark lands as the message is framed, so
/// the gap the heartbeat sees is at most one handler long. It wraps whatever filter SuperSocket
/// would use, and the filter that follows it after the WebSocket handshake.
/// </remarks>
internal sealed class ReceiveMarkingFilter<TPackage>(IPipelineFilter<TPackage> inner)
    : IPipelineFilter<TPackage>
{
    private readonly IPipelineFilter<TPackage> _inner = inner;

    // Kept apart from Context: the WebSocket handshake replaces the filter's context with its
    // extension settings once the session is set up.
    private ISessionContext? _session;
    private ReceiveMarkingFilter<TPackage>? _next;

    public IPackageDecoder<TPackage> Decoder
    {
        get => _inner.Decoder;
        set => _inner.Decoder = value;
    }

    public IPipelineFilter<TPackage> NextFilter
    {
        get
        {
            if (_inner.NextFilter is not { } next)
                return null!;

            if (!ReferenceEquals(_next?._inner, next))
                _next = new ReceiveMarkingFilter<TPackage>(next) { _session = _session };

            return _next;
        }
    }

    public object Context
    {
        get => _inner.Context;
        set
        {
            if (value is ISessionContext session)
                _session = session;

            _inner.Context = value;
        }
    }

    public TPackage Filter(ref SequenceReader<byte> reader)
    {
        var package = _inner.Filter(ref reader);

        if (package is not null)
            _session?.MarkReceived();

        return package;
    }

    public void Reset() => _inner.Reset();
}

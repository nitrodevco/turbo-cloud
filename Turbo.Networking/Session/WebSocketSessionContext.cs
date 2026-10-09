using System;
using System.Buffers;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using SuperSocket.WebSocket.Server;
using Turbo.Networking.Package;
using Turbo.Primitives.Crypto;
using Turbo.Primitives.Networking;
using Turbo.Primitives.Rooms;

namespace Turbo.Networking.Session;

public class WebSocketSessionContext(PackageEncoder packageEncoder, ILogger<ISessionContext> logger)
    : WebSocketSession(),
        ISessionContext,
        ISessionOutbound
{
    // A buffer that grew past this for one large packet is dropped afterwards rather than kept
    // for the life of the connection.
    private const int MAX_RETAINED_SEND_BUFFER_BYTES = 64 * 1024;
    private const int INITIAL_SEND_BUFFER_BYTES = 4096;

    private readonly PackageEncoder _packageEncoder = packageEncoder;
    private readonly SessionContextState _state = new(logger);

    // Reused for every outgoing packet. Safe because sends on a session are serialised by
    // SessionContextState, and SuperSocket has copied the frame into the pipe by the time a send
    // completes.
    private ArrayBufferWriter<byte> _sendBuffer = new(INITIAL_SEND_BUFFER_BYTES);

    public SessionKey SessionKey => this.SessionID;
    public bool PolicyDone
    {
        get => _state.PolicyDone;
        set => _state.PolicyDone = value;
    }
    public string RevisionId => _state.RevisionId;
    public IRc4Engine? CryptoIn => _state.CryptoIn;
    public IRc4Engine? CryptoOut => _state.CryptoOut;
    public RoomId ActiveRoomId => _state.ActiveRoomId;
    public DateTime LastReceivedUtc => _state.LastReceivedUtc;

    public void MarkReceived() => _state.MarkReceived();

    public ArrayBufferWriter<byte>? WsBuffer { get; } = new(4096);

    public string? ServerCloseReason => _state.CloseReason;

    public Task CloseSessionAsync(string reason = "closed by the server") =>
        _state.CloseAsync(
            this,
            reason,
            CloseAsync,
            () => Connection.CloseAsync(SuperSocket.Connection.CloseReason.LocalClosing)
        );

    public void SetRevisionId(string revisionId) => _state.RevisionId = revisionId;

    public void SetupEncryption(byte[] key, bool setCryptoOut = false) =>
        _state.SetupEncryption(key, setCryptoOut);

    public void SetActiveRoomId(RoomId roomId) => _state.SetActiveRoomId(roomId);

    public Task SendComposerAsync(IComposer composer, CancellationToken ct) =>
        _state.SendAsync(
            this,
            (Session: this, Composer: composer),
            static (s, token) => s.Session.SendEncodedAsync(s.Composer, token),
            composer,
            1,
            ct
        );

    // Each composer still goes out as its own binary frame, as it did one send at a time; the
    // batch saves the lock and the per-send allocations, not the frames.
    public Task SendComposersAsync(IReadOnlyList<IComposer> composers, CancellationToken ct) =>
        _state.SendAsync(
            this,
            (Session: this, Composers: composers),
            static async (s, token) =>
            {
                foreach (var composer in s.Composers)
                    await s.Session.SendEncodedAsync(composer, token).ConfigureAwait(false);
            },
            composers[0],
            composers.Count,
            ct
        );

    // A WebSocket frame carries the whole packet, so it is encoded into a buffer and sent as one
    // binary message rather than written through the connection's pipe like TCP.
    private async ValueTask SendEncodedAsync(IComposer composer, CancellationToken ct)
    {
        _sendBuffer.ResetWrittenCount();

        _packageEncoder.Encode(_sendBuffer, this, composer);

        await SendAsync(_sendBuffer.WrittenMemory, ct).ConfigureAwait(false);

        if (_sendBuffer.Capacity > MAX_RETAINED_SEND_BUFFER_BYTES)
            _sendBuffer = new(INITIAL_SEND_BUFFER_BYTES);
    }
}

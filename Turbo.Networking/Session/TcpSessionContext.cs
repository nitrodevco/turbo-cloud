using System;
using System.Buffers;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using SuperSocket.Server;
using Turbo.Networking.Package;
using Turbo.Primitives.Crypto;
using Turbo.Primitives.Networking;
using Turbo.Primitives.Rooms;

namespace Turbo.Networking.Session;

public class TcpSessionContext(PackageEncoder packageEncoder, ILogger<ISessionContext> logger)
    : AppSession(),
        ISessionContext,
        ISessionOutbound
{
    private readonly PackageEncoder _packageEncoder = packageEncoder;
    private readonly SessionContextState _state = new(logger);

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

    public ArrayBufferWriter<byte>? WsBuffer { get; } = null;

    public Task CloseSessionAsync() =>
        _state.CloseAsync(
            this,
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
            static (s, token) =>
                s.Session.Connection.SendAsync(
                    s.Session._packageEncoder,
                    new OutgoingPackage(s.Session, s.Composer),
                    token
                ),
            composer,
            1,
            ct
        );

    // TCP is a byte stream, so a batch is written into the pipe back to back and flushed once:
    // the same bytes in the same order as one send per composer, for one lock and one flush.
    public Task SendComposersAsync(IReadOnlyList<IComposer> composers, CancellationToken ct) =>
        _state.SendAsync(
            this,
            (Session: this, Composers: composers),
            static (s, token) =>
                s.Session.Connection.SendAsync(
                    writer =>
                    {
                        foreach (var composer in s.Composers)
                            s.Session._packageEncoder.Encode(writer, s.Session, composer);
                    },
                    token
                ),
            composers[0],
            composers.Count,
            ct
        );
}

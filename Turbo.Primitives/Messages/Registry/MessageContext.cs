using System.Collections.Frozen;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Turbo.Primitives.Action;
using Turbo.Primitives.Networking;
using Turbo.Primitives.Players;
using Turbo.Primitives.Rooms;

namespace Turbo.Messages.Registry;

public sealed class MessageContext(ISessionContext session, PlayerId playerId, RoomId roomId)
{
    private PlayerId _playerId = playerId;
    private RoomId _roomId = roomId;
    private ISessionContext _session = session;

    public PlayerId PlayerId => _playerId;
    public RoomId RoomId => _roomId;
    public SessionKey SessionKey => _session.SessionKey;

    /// <summary>
    /// The address the client connected from (<see cref="ISessionContext.RemoteAddress"/>); empty
    /// when it is not known. Personal data: for the login's policies and events, not for logs.
    /// </summary>
    public string RemoteAddress => _session.RemoteAddress ?? string.Empty;

    /// <summary>Whether this session has completed its key exchange.</summary>
    public bool IsEncrypted => _session.CryptoIn is not null;

    public ActionContext AsActionContext() =>
        new()
        {
            Origin = ActionOrigin.Player,
            SessionKey = SessionKey,
            PlayerId = PlayerId,
            RoomId = RoomId,
        };

    public async Task CloseSessionAsync(string reason = "closed by a packet handler") =>
        await _session.CloseSessionAsync(reason).ConfigureAwait(false);

    public async Task SendComposerAsync(IComposer composer, CancellationToken ct) =>
        await _session.SendComposerAsync(composer, ct).ConfigureAwait(false);

    public void SetRevisionId(string production) => _session.SetRevisionId(production);

    /// <summary>
    /// The protocol features this session's client named in its hello
    /// (<see cref="ISessionContext.ClientFeatures"/>).
    /// </summary>
    public FrozenSet<string> ClientFeatures => _session.ClientFeatures;

    public void SetClientFeatures(IEnumerable<string> advertised) =>
        _session.SetClientFeatures(advertised);

    public void SetupEncryption(byte[] key, bool setCryptoOut = false) =>
        _session.SetupEncryption(key, setCryptoOut);
}

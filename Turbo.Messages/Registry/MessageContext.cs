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

    public ActionContext AsActionContext() =>
        new()
        {
            Origin = ActionOrigin.Player,
            SessionKey = SessionKey,
            PlayerId = PlayerId,
            RoomId = RoomId,
        };

    public async Task CloseSessionAsync() =>
        await _session.CloseSessionAsync().ConfigureAwait(false);

    public async Task SendComposerAsync(IComposer composer, CancellationToken ct) =>
        await _session.SendComposerAsync(composer, ct).ConfigureAwait(false);

    public void SetRevisionId(string production) => _session.SetRevisionId(production);

    public void SetupEncryption(byte[] key, bool setCryptoOut = false) =>
        _session.SetupEncryption(key, setCryptoOut);
}

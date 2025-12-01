using System.Threading;
using System.Threading.Tasks;
using Turbo.Primitives.Action;
using Turbo.Primitives.Networking;
using Turbo.Primitives.Networking;
using Turbo.Primitives.Orleans.Snapshots.Session;
using Turbo.Primitives.Rooms;

namespace Turbo.Messages.Registry;

public sealed class MessageContext(ISessionContext session, long playerId = -1, long roomId = -1)
{
    private long _playerId = playerId;
    private RoomId _roomId = RoomId.From(roomId);
    private ISessionContext _session = session;

    public long PlayerId => _playerId;
    public RoomId RoomId => RoomId.From(_roomId.Value);
    public SessionKey SessionKey => SessionKey.From(_session.SessionKey.Value);

    public ActionContext AsActionContext() =>
        new()
        {
            Origin = ActionOrigin.Player,
            SessionKey = SessionKey,
            PlayerId = PlayerId,
            RoomId = RoomId.From(RoomId.Value),
        };

    public async Task CloseSessionAsync() =>
        await _session.CloseSessionAsync().ConfigureAwait(false);

    public async Task SendComposerAsync(IComposer composer, CancellationToken ct) =>
        await _session.SendComposerAsync(composer, ct).ConfigureAwait(false);

    public void SetRevisionId(string production) => _session.SetRevisionId(production);

    public void SetupEncryption(byte[] key, bool setCryptoOut = false) =>
        _session.SetupEncryption(key, setCryptoOut);
}

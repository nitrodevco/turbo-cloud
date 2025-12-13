using System.Threading;
using System.Threading.Tasks;
using Turbo.Primitives.Action;
using Turbo.Primitives.Networking;
using Turbo.Primitives.Orleans.Snapshots.Session;

namespace Turbo.Messages.Registry;

public sealed class MessageContext(ISessionContext session, long playerId = -1, int roomId = -1)
{
    private long _playerId = playerId;
    private int _roomId = roomId;
    private ISessionContext _session = session;

    public long PlayerId => _playerId;
    public int RoomId => _roomId;
    public SessionKey SessionKey => SessionKey.From(_session.SessionKey.Value);

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

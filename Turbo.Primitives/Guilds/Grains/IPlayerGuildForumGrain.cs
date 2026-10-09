using System.Collections.Immutable;
using System.Threading;
using System.Threading.Tasks;
using Orleans;
using Turbo.Primitives.Guilds.Forums.Snapshots;

namespace Turbo.Primitives.Guilds.Grains;

/// <summary>A player's side of the group forums: the lists, the unread count and the read markers.</summary>
public interface IPlayerGuildForumGrain : IGrainWithIntegerKey
{
    Task SendUnreadForumsCountAsync(CancellationToken ct);

    Task SendForumsListAsync(int listCode, int startIndex, int amount, CancellationToken ct);

    Task MarkReadAsync(ImmutableArray<GuildForumReadMarkerSnapshot> markers, CancellationToken ct);
}

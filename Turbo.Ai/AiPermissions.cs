using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Turbo.Contracts.Hotel;
using Turbo.Primitives.Players;

namespace Turbo.Ai;

/// <summary>Whether a player holds a permission node now: what an agent's tools are offered and run by.</summary>
public interface IAiPermissions
{
    Task<bool> HoldsAsync(PlayerId player, string node, CancellationToken ct);
}

/// <summary>The hotel's permissions (<see cref="IHotelPlayers.HasPermissionAsync"/>), asked as MCP tools' callers are.</summary>
internal sealed class HotelAiPermissions(IServiceProvider services) : IAiPermissions
{
    public Task<bool> HoldsAsync(PlayerId player, string node, CancellationToken ct) =>
        services.GetRequiredService<IHotel>().Players.HasPermissionAsync(player, node, ct);
}

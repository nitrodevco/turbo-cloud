using System.Collections.Concurrent;
using System.Collections.Generic;
using Turbo.Primitives.Players;
using Turbo.Primitives.Plugins;

namespace Turbo.Gamedata.Plugins;

/// <summary>One owner for the looks given to this host's clients, independent of audience rules.</summary>
internal sealed class ThemeClientState : IThemeClientState
{
    private readonly ConcurrentDictionary<PlayerId, ThemeClientLook> _drawn = new();

    public ThemeClientLook? DrawnBy(PlayerId player) => _drawn.GetValueOrDefault(player);

    public void NoteDrawn(PlayerId player, string? requested, string revision) =>
        _drawn[player] = new ThemeClientLook(requested, revision);

    public void ForgetDrawn(IReadOnlySet<PlayerId> online)
    {
        foreach (var player in _drawn.Keys)
            if (!online.Contains(player))
                _drawn.TryRemove(player, out _);
    }
}

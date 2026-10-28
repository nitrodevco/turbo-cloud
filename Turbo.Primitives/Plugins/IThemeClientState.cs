using System.Collections.Generic;
using Turbo.Primitives.Players;

namespace Turbo.Primitives.Plugins;

/// <summary>
/// Shared singleton tracking for session initialization and live reload on this host. Audience
/// providers must record the accepted preview (null when rejected) and the revision returned by
/// the same IThemeFileService.ThemeViewAsync result for every initialized player session. Do not keep a private copy.
/// </summary>
public interface IThemeClientState
{
    /// <summary>The last supplied look, or null when this host has no record for the player.</summary>
    public ThemeClientLook? DrawnBy(PlayerId player);

    /// <summary>Records a session load or live update, retaining only an accepted preview request.</summary>
    public void NoteDrawn(PlayerId player, string? requested, string revision);

    /// <summary>Forgets offline players so the next load starts with a fresh preview and revision.</summary>
    public void ForgetDrawn(IReadOnlySet<PlayerId> online);
}

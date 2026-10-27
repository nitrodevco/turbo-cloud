using System;
using System.Threading;
using System.Threading.Tasks;
using Turbo.Primitives.Plugins;
using Turbo.Primitives.Plugins.Messages;

namespace Turbo.Gamedata.Plugins;

/// <summary>
/// A player's choice of theme from the client's settings (<c>docs/plugin-data.md</c>, "Who gets
/// which theme"): a key of a theme they may choose, empty for the hotel's default. Not Habbo's: a
/// Turbo named message (<c>docs/plugin-messages.md</c>).
/// </summary>
[PluginMessage("turbo.theme.choose")]
public sealed record ChooseTheme(string Key) : IPluginMessage;

/// <summary>What came of a <see cref="ChooseTheme"/>: the choice kept, or why not. The client draws it from its next load.</summary>
[PluginMessage("turbo.theme.chosen")]
public sealed record ThemeChosen(string Key, bool Ok, string Message) : IPluginMessage;

/// <summary>
/// The player's look changed (<c>docs/plugin-data.md</c>, "Live reload"): the theme they draw now
/// (empty: the hotel's default), the look's revision, and where its variables are read
/// (<c>/gamedata/theme/live/&lt;revision&gt;</c>). The client draws it with no reload.
/// </summary>
[PluginMessage("turbo.theme.changed")]
public sealed record ThemeChanged(string Theme, string Revision, string Url) : IPluginMessage;

/// <summary>Keeps a player's choice of theme (<see cref="IThemeAudiences.ChooseAsync"/>) and says what came of it.</summary>
public sealed class ChooseThemeHandler(IThemeAudiences themes) : IPluginMessageHandler<ChooseTheme>
{
    public async ValueTask HandleAsync(
        ChooseTheme message,
        PluginMessageContext ctx,
        CancellationToken ct
    )
    {
        try
        {
            await themes.ChooseAsync(ctx.PlayerId, message.Key, ct).ConfigureAwait(false);
            await ctx.ReplyAsync(new ThemeChosen(message.Key, true, string.Empty), ct)
                .ConfigureAwait(false);
        }
        catch (ArgumentException ex)
        {
            await ctx.ReplyAsync(new ThemeChosen(message.Key, false, ex.Message), ct)
                .ConfigureAwait(false);
        }
    }
}

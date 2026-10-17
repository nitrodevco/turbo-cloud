using System;
using System.Collections.Generic;
using System.Text.Json.Nodes;

namespace Turbo.Gamedata.Habbo.Client.Theme;

/// <summary>
/// What a theme component makes of its skin beyond its layers, as Studio's
/// <c>server/theme/componentAdapters.ts</c> has it: the entities it draws itself
/// (<see cref="Roles"/>), which are left out of the layers, and the fields of its variant they
/// become. This is nitro-react's components' own structure (which skin entity is a dropmenu's
/// arrow), never a value: where an arrow sits, and what it looks like, is read off the skin.
/// </summary>
/// <param name="Fields">The fields, from each state's role pieces by state, then role.</param>
internal sealed record ComponentAdapter(
    IReadOnlyList<string> Roles,
    Func<Dictionary<string, Dictionary<string, SkinRolePiece>>, JsonObject> Fields
)
{
    /// <summary>The adapters, by component (cascade key).</summary>
    public static readonly IReadOnlyDictionary<string, ComponentAdapter> BY_COMPONENT =
        new Dictionary<string, ComponentAdapter>
        {
            // `DropmenuFrame`: the arrow is its own sprite at `arrowLayout`.
            ["dropmenu"] = new(
                ["arrow"],
                roles =>
                {
                    var (arrow, fields) = ArrowTextures(roles);

                    if (arrow is not null)
                        fields["arrowLayout"] = new JsonObject
                        {
                            ["right"] = ThemeNumbers.Json(arrow.Right),
                            ["top"] = ThemeNumbers.Json(arrow.Top),
                            ["width"] = ThemeNumbers.Json(arrow.Width),
                            ["height"] = ThemeNumbers.Json(arrow.Height),
                        };

                    return fields;
                }
            ),
            // `Droplist`: the same arrow, placed by its top and right offsets, and the box at least as
            // tall as the skin is drawn (the thick one is 32, its arrow 10 down).
            ["droplist"] = new(
                ["arrow"],
                roles =>
                {
                    var (arrow, fields) = ArrowTextures(roles);

                    if (arrow is not null)
                    {
                        fields["arrowTop"] = ThemeNumbers.Json(arrow.Top);
                        fields["arrowRight"] = ThemeNumbers.Json(arrow.Right);
                        fields["layout"] = new JsonObject
                        {
                            ["minHeight"] = ThemeNumbers.Json(
                                arrow.Top + arrow.Height + arrow.Bottom
                            ),
                        };
                    }

                    return fields;
                }
            ),
        };

    /// <summary>An arrow the component places itself: its bitmap, and its hovering one where the skin changes it.</summary>
    private static (SkinRolePiece? Arrow, JsonObject Fields) ArrowTextures(
        Dictionary<string, Dictionary<string, SkinRolePiece>> roles
    )
    {
        var arrow = roles.GetValueOrDefault("default")?.GetValueOrDefault("arrow");
        var hovering = roles.GetValueOrDefault("hovering")?.GetValueOrDefault("arrow");
        var fields = new JsonObject();

        if (arrow is not null)
            fields["arrowTextureKey"] = arrow.TextureKey;

        if (hovering is not null && arrow is not null && hovering.TextureKey != arrow.TextureKey)
            fields["arrowHoveringTextureKey"] = hovering.TextureKey;

        return (arrow, fields);
    }
}

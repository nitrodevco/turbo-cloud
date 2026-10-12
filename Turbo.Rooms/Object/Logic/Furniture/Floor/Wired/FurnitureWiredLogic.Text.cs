using System.Collections.Generic;
using System.Linq;
using Turbo.Primitives.Rooms.Enums.Wired;
using Turbo.Primitives.Rooms.Snapshots.Wired.Variables;
using Turbo.Primitives.Rooms.Wired;
using Turbo.Rooms.Object.Logic.Furniture.Floor.Wired.Addons;
using Turbo.Rooms.Object.Logic.Furniture.Floor.Wired.Triggers;
using Turbo.Rooms.Wired;

namespace Turbo.Rooms.Object.Logic.Furniture.Floor.Wired;

/// <summary>What the message editor of a box that shows text is told about its stack.</summary>
public abstract partial class FurnitureWiredLogic
{
    /// <summary>
    /// The editor context of a box with a message editor (Show Message, Bot Talk): the
    /// placeholders the stack on its tile fills, as the "Add placeholder" button lists them,
    /// then the stack's capturers. "User Says Keyword" contributes <c>trigger_text</c>.
    /// The order (the trigger's first, then the add-ons from the bottom up) is this server's:
    /// the only official capture is of a stack with none.
    /// </summary>
    protected List<WiredVariableContextSnapshot> TextPlaceholdersContext()
    {
        var logics = FurniModule
            .GetFloorItemsOnTile(_ctx.GetTileIdx())
            .OrderBy(item => item.Z.Value)
            .Select(item => item.Logic)
            .ToList();
        var placeholderNames = new List<string>();

        if (logics.Any(logic => logic is WiredTriggerHabboSaysKeyword))
            placeholderNames.Add(WiredPlaceholderText.TRIGGER_TEXT_NAME);

        placeholderNames.AddRange(
            logics
                .OfType<IWiredTextPlaceholder>()
                .Select(placeholder => WiredPlaceholderText.NameOf(placeholder.Token))
                .Where(name => name.Length > 0)
        );

        return
        [
            new WiredTextPlaceholdersSnapshot
            {
                ContextType = WiredContextType.TextPlaceholders,
                PlaceholderNames = [.. placeholderNames.Distinct()],
                CapturerNames =
                [
                    .. logics
                        .OfType<WiredAddonVariableCapturer>()
                        .Select(capturer => WiredPlaceholderText.NameOf(capturer.CaptureToken))
                        .Where(name => name.Length > 0)
                        .Distinct(),
                ],
            },
        ];
    }
}

using System.Collections.Generic;
using System.Linq;
using Turbo.Rooms.Object.Logic.Furniture.Floor.Wired.Addons;

namespace Turbo.Rooms.Grains.Systems;

/// <summary>The room's Global Placeholder add-ons, whose tokens every wired text of the room reads.</summary>
public sealed partial class RoomWiredSystem
{
    /// <summary>The named Global Placeholder add-ons of every stack, in stack order.</summary>
    public IEnumerable<WiredAddonGlobalPlaceholder> GlobalPlaceholders =>
        _stacksById
            .OrderBy(x => x.Key)
            .SelectMany(x => x.Value.Addons.OfType<WiredAddonGlobalPlaceholder>())
            .Where(x => x.Token.Length > 0);
}

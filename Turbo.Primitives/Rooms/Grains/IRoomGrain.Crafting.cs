using System.Threading;
using System.Threading.Tasks;
using Turbo.Primitives.Action;

namespace Turbo.Primitives.Rooms.Grains;

/// <summary>The room's crafting tables, for the one request that names no table.</summary>
public partial interface IRoomGrain
{
    /// <summary>
    /// Sends what a listed recipe takes (Flash <c>GetCraftingRecipeComposer</c> carries only the
    /// recipe code): the first crafting table in the room that lists it answers. False when none does.
    /// </summary>
    public Task<bool> SendCraftingRecipeAsync(
        ActionContext ctx,
        string recipeCode,
        CancellationToken ct
    );
}

using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Turbo.Primitives.Action;
using Turbo.Primitives.Messages.Outgoing.Crafting;
using Turbo.Primitives.Orleans;
using Turbo.Rooms.Object.Logic.Furniture.Floor;

namespace Turbo.Rooms.Grains;

public sealed partial class RoomGrain
{
    public async Task<bool> SendCraftingRecipeAsync(
        ActionContext ctx,
        string recipeCode,
        CancellationToken ct
    )
    {
        if (!AvatarModule.Players.Any(x => x.PlayerId == ctx.PlayerId))
            return false;

        var recipe = FurniModule
            .Items.Select(x => x.Logic)
            .OfType<FurnitureCraftingLogic>()
            .Select(x => x.FindListedRecipe(recipeCode))
            .FirstOrDefault(x => x is not null);

        if (recipe is null)
            return false;

        await _grainFactory.SendComposerToPlayerAsync(
            ctx.PlayerId,
            new CraftingRecipeMessageComposer
            {
                Ingredients = FurnitureCraftingLogic.Ingredients(recipe),
            },
            ct
        );

        return true;
    }
}

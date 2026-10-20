using System.Threading;
using System.Threading.Tasks;
using Turbo.Primitives.Action;
using Turbo.Primitives.Rooms.Object;
using Turbo.Primitives.Rooms.Wired.WebApi;
using Turbo.Rooms.Object.Logic.Furniture.Floor.Wired.Addons;

namespace Turbo.Rooms.Grains;

public sealed partial class RoomGrain
{
    public async Task<string?> GenerateWiredWebApiKeyAsync(
        ActionContext ctx,
        int boxId,
        bool isReadKey,
        CancellationToken ct
    )
    {
        if (
            !FurniModule.TryGetItem(RoomObjectId.Parse(boxId), out var item)
            || item.Logic is not WiredAddonVariablesWebApi addon
        )
            return null;

        // Whoever may save the box may have a key issued for it, and nobody else.
        var level = await SecurityModule.GetControllerLevelAsync(ctx);
        var (canModify, _) = WiredSystem.GetPermissions(level);

        if (!canModify || level < addon.MinimumControllerLevelToSave)
            return null;

        return addon.IssueKey(isReadKey);
    }

    public Task<string?> CheckWiredWebApiKeysAsync(
        WiredWebApiOperation operation,
        string? readKey,
        string? writeKey,
        CancellationToken ct
    ) => WiredSystem.CheckWebApiKeysAsync(operation, readKey, writeKey, ct);

    public Task<WiredWebApiResponse> HandleWiredWebApiAsync(
        WiredWebApiRequest request,
        CancellationToken ct
    ) => WiredSystem.HandleWebApiAsync(request, ct);
}

using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;
using Orleans;
using Turbo.Primitives.Action;
using Turbo.Primitives.Furniture;
using Turbo.Primitives.Furniture.Providers;
using Turbo.Primitives.Messages.Incoming.Userdefinedroomevents;
using Turbo.Primitives.Rooms.Enums;
using Turbo.Primitives.Rooms.Enums.Wired;
using Turbo.Primitives.Rooms.Object.Furniture.Floor;
using Turbo.Primitives.Rooms.Object.Logic;
using Turbo.Primitives.Rooms.Wired;
using Turbo.Primitives.Rooms.Wired.WebApi;
using Turbo.Rooms.Wired.Rules;

namespace Turbo.Rooms.Object.Logic.Furniture.Floor.Wired.Addons;

/// <summary>
/// "Variable Global Add-on: Web API" (Flash <c>AddonCodes.VARIABLES_WEB_API</c> = 2002, editor
/// <c>wired_setup.addons.VariablesWebApiAddon</c>): the keys the room's variables are read and
/// written with over the public Web API. The editor saves them as the string param
/// <c>readKey + "\t" + writeKey</c> and "Allow mass deletion" as int param 0, which it only sends
/// as 1 with a write key (<c>readStringParamFromForm</c>, <c>readIntParamsFromForm</c>). A key is
/// only ever one this box issued (<c>WiredGenerateWebApiKey</c>): a save with any other is refused
/// with <c>wiredfurni.error.invalid_api_keys</c> ("Invalid Web API keys"), so a key cannot be
/// chosen by hand. Picking the box up drops its wired data, so the keys are different when it is
/// placed again (<c>wiredfurni.web_api.pickup.body</c>).
/// </summary>
[RoomObjectLogic(WiredWebApiKeys.ADDON_LOGIC)]
public class WiredAddonVariablesWebApi(
    IGrainFactory grainFactory,
    IStuffDataFactory stuffDataFactory,
    IRoomFloorItemContext ctx
) : FurnitureWiredAddonLogic(grainFactory, stuffDataFactory, ctx)
{
    /// <summary>Random bytes per key (inference: Habbo's key format is not documented).</summary>
    private const int KEY_BYTES = 32;

    private const char KEY_SEPARATOR = '\t';

    // The key the editor got last for each field; a Generate replaces the one before, which the
    // editor no longer shows, so pressing it again and again holds no more than two keys.
    private string? _issuedReadKey;
    private string? _issuedWriteKey;

    public override int WiredCode => (int)WiredAddonType.VARIABLES_WEB_API;

    /// <summary>
    /// The keys control the room's variables from outside the hotel, so only the room's owner
    /// sets them ("Nobody else can see the contents of this box", <c>wiredfurni.params.web_api.usage_info</c>).
    /// </summary>
    public override RoomControllerType MinimumControllerLevelToSave => RoomControllerType.Owner;

    public override List<IWiredParamRule> GetIntParamRules() => [new WiredBoolParamRule(false)];

    protected override int GetStringParamMaxLength() => 512;

    public string ReadKey => WiredWebApiKeys.ReadKeyOf(_wiredData.StringParam);

    public string WriteKey => WiredWebApiKeys.WriteKeyOf(_wiredData.StringParam);

    /// <summary>"Allow mass deletion": the bulk-delete endpoint, never without a write key.</summary>
    public bool AllowsBulkDelete => WriteKey.Length > 0 && GetIntParamOrDefault(0, false);

    /// <summary>A fresh key the editor may save into the read or write field.</summary>
    public string IssueKey(bool isReadKey)
    {
        var key = Convert.ToHexStringLower(RandomNumberGenerator.GetBytes(KEY_BYTES));

        if (isReadKey)
            _issuedReadKey = key;
        else
            _issuedWriteKey = key;

        return key;
    }

    public override Task<bool> ApplyWiredUpdateAsync(
        ActionContext ctx,
        UpdateWiredMessage update,
        CancellationToken ct
    )
    {
        var read = WiredWebApiKeys.ReadKeyOf(update.StringParam);
        var write = WiredWebApiKeys.WriteKeyOf(update.StringParam);

        if (
            !IsKnownKey(read, ReadKey, _issuedReadKey)
            || !IsKnownKey(write, WriteKey, _issuedWriteKey)
        )
            throw new WiredSaveRefusedException(WiredSaveErrors.INVALID_API_KEYS);

        _issuedReadKey = null;
        _issuedWriteKey = null;

        return base.ApplyWiredUpdateAsync(
            ctx,
            update with
            {
                StringParam = read + WiredWebApiKeys.SEPARATOR + write,
            },
            ct
        );
    }

    public override FurniturePickupConfirmation? GetPickupConfirmation() =>
        FurniturePickupConfirmation.WiredWebApi;

    private static bool IsKnownKey(string key, string current, string? issued) =>
        key.Length == 0 || key == current || key == issued;
}

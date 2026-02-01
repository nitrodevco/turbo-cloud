using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Turbo.Primitives.Action;
using Turbo.Primitives.Messages.Incoming.Userdefinedroomevents;
using Turbo.Primitives.Rooms.Enums.Wired;
using Turbo.Primitives.Rooms.Snapshots.Wired;
using Turbo.Primitives.Rooms.Snapshots.Wired.Variables;

namespace Turbo.Primitives.Rooms.Wired;

public interface IWiredBox
{
    public WiredType WiredType { get; }
    public int WiredCode { get; }

    public Task LoadWiredAsync(CancellationToken ct);
    public Task FlashActivationStateAsync(CancellationToken ct);
    public List<int> GetStuffIds();
    public List<int> GetStuffIds2();
    public List<IWiredParamRule> GetIntParamRules();
    public IWiredParamRule? GetIntParamTailRule();
    public List<WiredFurniSourceType[]> GetAllowedFurniSources();
    public List<WiredPlayerSourceType[]> GetAllowedPlayerSources();
    public List<Type> GetDefinitionSpecificTypes();
    public List<Type> GetTypeSpecificTypes();
    public List<WiredVariableContextSnapshot> GetWiredContextSnapshots();
    public List<WiredFurniSourceType[]> GetFurniSources();
    public List<WiredPlayerSourceType[]> GetPlayerSources();
    public List<WiredFurniSourceType[]> GetDefaultFurniSources();
    public List<WiredPlayerSourceType[]> GetDefaultPlayerSources();
    public Task<bool> ApplyWiredUpdateAsync(
        ActionContext ctx,
        UpdateWiredMessage update,
        CancellationToken ct
    );
    public WiredDataSnapshot GetSnapshot();
}

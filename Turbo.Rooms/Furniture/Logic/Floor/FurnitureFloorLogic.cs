using System.Threading;
using System.Threading.Tasks;
using Turbo.Primitives.Actor;
using Turbo.Primitives.Rooms.Furniture.Floor;
using Turbo.Primitives.Rooms.Furniture.Logic;
using Turbo.Primitives.Rooms.Furniture.StuffData;

namespace Turbo.Rooms.Furniture.Logic.Floor;

[FurnitureLogic("default_floor")]
public class FurnitureFloorLogic
    : FurnitureLogicBase<IRoomFloorItem, IRoomFloorItemContext>,
        IFurnitureFloorLogic
{
    public virtual StuffDataType StuffDataKey => StuffDataType.LegacyKey;
    protected IStuffData _stuffData;

    public FurnitureFloorLogic(IStuffDataFactory stuffDataFactory, IRoomFloorItemContext ctx)
        : base(stuffDataFactory, ctx)
    {
        _stuffData = CreateStuffData(_ctx.Item.PendingStuffDataRaw);
    }

    public IStuffData StuffData => _stuffData;

    public override async Task<bool> SetStateAsync(int state)
    {
        _stuffData.SetState(state.ToString());

        _ctx.Item.MarkDirty();

        await _ctx.RefreshStuffDataAsync(CancellationToken.None);

        return true;
    }

    public virtual bool CanStack() => _ctx.Definition.CanStack;

    public virtual bool CanWalk() => _ctx.Definition.CanWalk;

    public virtual bool CanSit() => _ctx.Definition.CanSit;

    public virtual bool CanLay() => _ctx.Definition.CanLay;

    public override async Task OnUseAsync(ActorContext ctx, int param, CancellationToken ct)
    {
        param = GetNextToggleableState();

        await SetStateAsync(param);
    }

    public virtual Task OnStopAsync(ActorContext ctx, CancellationToken ct) => Task.CompletedTask;

    protected virtual int GetNextToggleableState()
    {
        var totalStates = _ctx.Item.Definition.TotalStates;

        if (totalStates == 0 || StuffData is null)
            return 0;

        return (StuffData.GetState() + 1) % totalStates;
    }

    protected virtual IStuffData CreateStuffData(string json = "") =>
        _stuffDataFactory.CreateStuffDataFromJson((int)StuffDataKey, json);
}

using System;
using System.Threading;
using System.Threading.Tasks;
using Turbo.Primitives.Furniture.ExtraData;
using Turbo.Primitives.Furniture.Providers;
using Turbo.Primitives.Rooms.Object.Avatars;
using Turbo.Primitives.Rooms.Object.Furniture.Floor;
using Turbo.Primitives.Rooms.Object.Logic;

namespace Turbo.Rooms.Object.Logic.Furniture.Floor;

/// <summary>
/// A furni that hands out hand items: fridges, drinks machines and ice cream machines (Sulake's
/// <c>VendingMachineFurni</c> and <c>IceCreamMachineFurni</c>), and the static ones that hand
/// something over without a machine (<c>HandItemProviderFurni</c>). What it gives is its
/// definition's <see cref="VendingMachineData"/>, one of its items at random; who may use it is
/// the definition's usage policy, which Sulake's data makes everyone's for nearly all of them.
/// How it serves is <see cref="FurnitureServingLogic"/>'s.
/// </summary>
[RoomObjectLogic("vending_machine")]
public class FurnitureVendingMachineLogic : FurnitureServingLogic
{
    private readonly VendingMachineData _data;

    public FurnitureVendingMachineLogic(
        IStuffDataFactory stuffDataFactory,
        IRoomFloorItemContext ctx
    )
        : base(stuffDataFactory, ctx)
    {
        _data =
            FurnitureExtraDataSections.Read<VendingMachineData>(
                ctx.RoomObject.ExtraData,
                ctx.Definition.ExtraData,
                VendingMachineData.SECTION,
                _roomGrain._logger
            ) ?? new VendingMachineData();
    }

    protected override bool CanServe => !_data.HandItems.IsDefaultOrEmpty;

    protected override bool Animates => _data.Animates;

    protected override Task HandOverAsync(IRoomAvatar avatar, CancellationToken ct) =>
        AvatarModule.SetHandItemAsync(
            avatar,
            _data.HandItems[Random.Shared.Next(_data.HandItems.Length)],
            ct
        );
}

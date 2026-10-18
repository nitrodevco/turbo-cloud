using System;
using Turbo.Primitives.Movements;
using Turbo.Primitives.Movements.Enums;
using Turbo.Primitives.Players.Wallet;

namespace Turbo.Players.Grains;

internal sealed partial class PlayerWalletGrain
{
    // A currency is one asset per kind: no id and no definition, the kind says which.
    private AssetPlace Wallet => new(PlayerId.Value, PlaceKind.Wallet, 0, null);

    // The i-th currency of one call is movement FirstSeq + i of its action.
    private AssetMovement Debited(
        MovementCause cause,
        int index,
        DateTime occurredAtUtc,
        CurrencyKind kind,
        int amount
    ) =>
        AssetMovement.For(
            cause,
            checked((short)(cause.FirstSeq + index)),
            occurredAtUtc,
            new AssetRef(AssetKind.Currency, 0, 0, kind),
            Wallet,
            AssetPlace.Nowhere,
            amount
        );

    private AssetMovement Credited(MovementCause cause, CurrencyKind kind, int amount) =>
        AssetMovement.For(
            cause,
            cause.FirstSeq,
            DateTime.UtcNow,
            new AssetRef(AssetKind.Currency, 0, 0, kind),
            AssetPlace.Nowhere,
            Wallet,
            amount
        );
}

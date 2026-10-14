using FluentAssertions;
using Turbo.Primitives.Rooms.Enums.Wired;
using Turbo.Primitives.Rooms.Wired.Variable;
using Turbo.Primitives.WiredTrading.Enums;
using Turbo.Primitives.WiredTrading.Snapshots;
using Turbo.Rooms.Wired.Variables;
using Turbo.Rooms.Wired.Variables.User;
using Turbo.Tests.Support;
using Xunit;

namespace Turbo.Tests.Rooms;

/// <summary>
/// Habbo's <c>@transaction.*</c> user variables (-281..-286, official client capture,
/// WIN63-202610121709) read the player's open contract transaction: held while one is open,
/// <c>in_trade</c> and <c>can_accept</c> as flags, state 1 "Adding Items" / 2 "Confirmed".
/// Ours had the ids mapped but no variable behind them.
/// </summary>
public sealed class WiredTransactionVariableTests
{
    private const int PLAYER_INDEX = 5;
    private const int CONTRACT = 7;

    private readonly WiredRoom _room = new(8, 8);

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task A_player_holds_the_transaction_variables_while_their_transaction_is_open()
    {
        var avatar = _room.Enter(PLAYER_INDEX, 1, 1);

        Read().Should().BeEmpty("nothing is held before a transaction opens");

        await _room.Harness.Room.ReportWiredTransactionProgressAsync(
            avatar.PlayerId,
            new()
            {
                StartedAtMs = 1_760_000_000_000,
                ContractId = CONTRACT,
                State = WiredTransactionStateType.Confirmed,
                CanAccept = true,
                Multiplier = 3,
            },
            Ct
        );

        Read()
            .Should()
            .BeEquivalentTo(
                new Dictionary<string, long>
                {
                    ["@transaction.in_trade"] = WiredVariableValue.HeldFlag,
                    ["@transaction.start_time"] = 1_760_000_000_000,
                    ["@transaction.contract_id"] = CONTRACT,
                    ["@transaction.state"] = 2,
                    ["@transaction.can_accept"] = WiredVariableValue.HeldFlag,
                    ["@transaction.current_multiplier"] = 3,
                }
            );

        await _room.Harness.Room.ReportWiredTransactionProgressAsync(
            avatar.PlayerId,
            new()
            {
                StartedAtMs = 1_760_000_000_000,
                ContractId = CONTRACT,
                State = WiredTransactionStateType.AddingItems,
                CanAccept = false,
                Multiplier = 0,
            },
            Ct
        );

        Read().Should().NotContainKey("@transaction.can_accept");
        Read()["@transaction.state"].Should().Be(1);

        await _room.Harness.Room.ReportWiredTransactionProgressAsync(avatar.PlayerId, null, Ct);

        Read().Should().BeEmpty("the transaction ended");
    }

    [Fact]
    public void Habbo_hides_habbicon_can_move_freely_and_the_walk_helper_flag_from_the_editor()
    {
        WiredInternalVariable[] hidden =
        [
            new UserHabbiconVariable(_room.Harness.Room),
            new Turbo.Rooms.Wired.Variables.Context.ContextHabbiconVariable(_room.Harness.Room),
            new Turbo.Rooms.Wired.Variables.Furniture.FurnitureCanMoveFreelyVariable(
                _room.Harness.Room
            ),
            new Turbo.Rooms.Wired.Variables.Furniture.Smart.FurnitureWalkHelperIsMultiWalkVariable(
                _room.Harness.Room
            ),
        ];

        hidden
            .Select(x => x.GetVarSnapshot())
            .Should()
            .OnlyContain(x => x.Flags.Has(WiredVariableFlags.IsInvisible));
        var habbicon = new UserHabbiconVariable(_room.Harness.Room).GetVarSnapshot().Flags;
        habbicon.Has(WiredVariableFlags.CanReadCreationTime).Should().BeTrue();
        habbicon.Has(WiredVariableFlags.CanReadLastUpdateTime).Should().BeTrue();
    }

    private Dictionary<string, long> Read()
    {
        WiredInternalVariable[] variables =
        [
            new UserTransactionInTradeVariable(_room.Harness.Room),
            new UserTransactionStartTimeVariable(_room.Harness.Room),
            new UserTransactionContractIdVariable(_room.Harness.Room),
            new UserTransactionStateVariable(_room.Harness.Room),
            new UserTransactionCanAcceptVariable(_room.Harness.Room),
            new UserTransactionCurrentMultiplierVariable(_room.Harness.Room),
        ];
        Dictionary<string, long> held = [];

        foreach (var variable in variables)
        {
            var snapshot = variable.GetVarSnapshot();

            if (
                variable.TryGetValue(
                    new WiredVariableKey(
                        snapshot.VariableId,
                        WiredVariableTargetType.User,
                        PLAYER_INDEX
                    ),
                    out var value
                )
            )
                held[snapshot.VariableName] = value;
        }

        return held;
    }
}

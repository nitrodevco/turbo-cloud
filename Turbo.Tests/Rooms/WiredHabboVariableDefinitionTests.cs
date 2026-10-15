using System.Globalization;
using System.Text.Json;
using FluentAssertions;
using Turbo.Primitives.Messages.Incoming.Userdefinedroomevents;
using Turbo.Primitives.Rooms.Enums.Wired;
using Turbo.Primitives.Rooms.Snapshots.Wired.Variables;
using Turbo.Primitives.Rooms.Wired;
using Turbo.Primitives.Rooms.Wired.Variable;
using Turbo.Rooms.Grains.Systems;
using Turbo.Rooms.Object.Logic.Furniture.Floor.Wired.Actions;
using Turbo.Rooms.Wired.Variables;
using Turbo.Rooms.Wired.Variables.Furniture;
using Turbo.Tests.Support;
using Xunit;

namespace Turbo.Tests.Rooms;

/// <summary>
/// The built-in variables as hhus describes them in WiredAllVariablesDiffs (recorded
/// WIN63-202610121709 in room 79874981; Fixtures/hhus-variables-WIN63-202610121709.json): Habbo's
/// fixed ids ("-120" for a furni's @position.x, "~131" for ~chest.locked), names, which are always
/// available, which hold a value, can be written or given and removed, and the labels of their values.
/// </summary>
public sealed class WiredHabboVariableDefinitionTests
{
    // Their labels are hotel texts, which the test room does not have.
    private static readonly HashSet<string> LabelsFromTexts =
    [
        "@dance",
        "@sign",
        "@effect",
        "@handitem",
    ];

    // Variables Habbo has that are not built yet.
    private static readonly HashSet<string> NotBuiltYet = [];

    private readonly WiredRoom _room = new(8, 8);

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public void Every_built_in_variable_has_Habbos_id_name_availability_and_labels()
    {
        var official = Official();
        var ours = Ours().ToDictionary(x => (x.TargetType, x.VariableName));

        foreach (var habbo in official)
        {
            var key = ((WiredVariableTargetType)habbo.Target, habbo.Name);

            if (NotBuiltYet.Contains(habbo.Name))
            {
                ours.Should().NotContainKey(key, "it is listed as not built yet");

                continue;
            }

            ours.Should().ContainKey(key);
            var snapshot = ours[key];

            snapshot.VariableId.ToString().Should().Be(habbo.Id, "{0} id", habbo.Name);
            snapshot
                .Flags.Has(WiredVariableFlags.AlwaysAvailable)
                .Should()
                .Be(habbo.AlwaysAvailable, "{0} alwaysAvailable", habbo.Name);
            foreach (var (flag, expected) in habbo.Flags)
                snapshot.Flags.Has(flag).Should().Be(expected, "{0} {1}", habbo.Name, flag);

            snapshot
                .Flags.Has(WiredVariableFlags.HasTextConnector)
                .Should()
                .Be(habbo.Labels is not null, "{0} has a text connector", habbo.Name);

            if (habbo.Labels is null || LabelsFromTexts.Contains(habbo.Name))
                continue;

            snapshot
                .TextConnectors.Select(x =>
                    (x.Key.Value.ToString(CultureInfo.InvariantCulture), x.Value)
                )
                .Should()
                .BeEquivalentTo(habbo.Labels, "{0} labels", habbo.Name);
        }

        ours.Keys.Where(x => !official.Any(o => ((WiredVariableTargetType)o.Target, o.Name) == x))
            .Should()
            .BeEmpty("every built-in variable is one Habbo has, under Habbo's name");
    }

    [Fact]
    public async Task A_box_saved_with_a_variables_old_hashed_id_reopens_with_Habbos_id()
    {
        var positionX = new FurniturePositionXVariable(_room.Harness.Room);
        _room.Harness.Fakes.Handlers["BuildVariablesForRoom"] = _ =>
            new IWiredVariable[] { positionX };
        await _room.Harness.Module<RoomWiredSystem>().ProcessWiredAsync(11_000, false, Ct);
        var change = _room.AddBox<WiredActionChangeVariable>(2, 0, 0, "wf_act_change_var_val");

        (
            await _room.SaveAsync<UpdateActionMessage>(
                2,
                intParams:
                [
                    (int)WiredVariableTargetType.Furni,
                    (int)WiredVariableOperationType.Add,
                    0,
                    0,
                    1,
                    (int)WiredVariableTargetType.Global,
                    0,
                ],
                definitionSpecifics: [0],
                variableIds: [positionX.LegacyVariableId.ToString(), "0"]
            )
        )
            .Should()
            .BeTrue();

        change.GetSnapshot().VariableIds.Select(x => x.ToString()).Should().Equal("-120", "0");
    }

    private List<WiredVariableSnapshot> Ours() =>
        [
            .. typeof(WiredInternalVariable)
                .Assembly.GetTypes()
                .Where(t => !t.IsAbstract && t.IsSubclassOf(typeof(WiredInternalVariable)))
                .Select(t =>
                    (
                        (WiredInternalVariable)Activator.CreateInstance(t, _room.Harness.Room)!
                    ).GetVarSnapshot()
                )
                .Where(x => x.VariableName.StartsWith('@') || x.VariableName.StartsWith('~')),
        ];

    private static List<HabboVariable> Official()
    {
        using var stream =
            typeof(WiredHabboVariableDefinitionTests).Assembly.GetManifestResourceStream(
                "Rooms.Fixtures.hhus-variables-WIN63-202610121709.json"
            )!;
        using var json = JsonDocument.Parse(stream);

        return
        [
            .. json
                .RootElement.EnumerateArray()
                .Select(x => new HabboVariable(
                    x.GetProperty("id").GetString()!,
                    x.GetProperty("name").GetString()!,
                    x.GetProperty("target").GetInt32(),
                    x.GetProperty("alwaysAvailable").GetBoolean(),
                    [
                        (WiredVariableFlags.HasValue, x.GetProperty("hasValue").GetBoolean()),
                        (
                            WiredVariableFlags.CanWriteValue,
                            x.GetProperty("canWriteValue").GetBoolean()
                        ),
                        (
                            WiredVariableFlags.CanCreateAndDelete,
                            x.GetProperty("canCreateAndDelete").GetBoolean()
                        ),
                    ],
                    x.GetProperty("textConnector").ValueKind == JsonValueKind.Array
                        ?
                        [
                            .. x.GetProperty("textConnector")
                                .EnumerateArray()
                                .Select(pair =>
                                    (
                                        pair[0].GetInt64().ToString(CultureInfo.InvariantCulture),
                                        pair[1].GetString()!
                                    )
                                ),
                        ]
                        : null
                )),
        ];
    }

    private sealed record HabboVariable(
        string Id,
        string Name,
        int Target,
        bool AlwaysAvailable,
        List<(WiredVariableFlags, bool)> Flags,
        List<(string, string)>? Labels
    );
}

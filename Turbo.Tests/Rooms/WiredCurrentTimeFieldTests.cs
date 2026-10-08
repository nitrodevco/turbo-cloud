using System.Runtime.CompilerServices;
using FluentAssertions;
using Turbo.Primitives.Rooms.Enums.Wired;
using Turbo.Primitives.Rooms.Snapshots;
using Turbo.Primitives.Rooms.Wired.Variable;
using Turbo.Rooms.Wired.Variables;
using Turbo.Rooms.Wired.Variables.Room;
using Turbo.Tests.Support;
using Xunit;

namespace Turbo.Tests.Rooms;

/// <summary>
/// The Wired Faculty tutorial "Real-Time Clock" (11/03/2025) sets the room's time zone in the
/// Creator Tools settings and reads <c>@current_time.hour_of_day</c> from the internal variables
/// into its number blocks. Only <c>@current_time</c> (unix milliseconds) existed.
/// </summary>
public sealed class WiredCurrentTimeFieldTests
{
    private const string TOKYO = "Asia/Tokyo";

    private readonly WiredRoom _room = new(8, 8);

    [Fact]
    public void The_hour_of_day_is_read_in_the_rooms_time_zone()
    {
        SetTimeZone(TOKYO);
        var tokyo = TimeZoneInfo.ConvertTime(
            DateTimeOffset.UtcNow,
            TimeZoneInfo.FindSystemTimeZoneById(TOKYO)
        );

        Read(new RoomCurrentTimeHourOfDayVariable(_room.Harness.Room))
            .Should()
            .BeOneOf(tokyo.Hour, tokyo.AddMinutes(1).Hour);
        Read(new RoomCurrentTimeDayOfMonthVariable(_room.Harness.Room))
            .Should()
            .BeOneOf(tokyo.Day, tokyo.AddMinutes(1).Day);
        Read(new RoomCurrentTimeYearVariable(_room.Harness.Room))
            .Should()
            .BeOneOf(tokyo.Year, tokyo.AddMinutes(1).Year);
        Read(new RoomCurrentTimeDayOfWeekVariable(_room.Harness.Room)).Should().BeInRange(1, 7);
    }

    [Fact]
    public void Every_field_the_time_utilities_name_is_an_internal_variable()
    {
        var names = typeof(RoomCurrentTimeFieldVariable)
            .Assembly.GetTypes()
            .Where(t => !t.IsAbstract && t.IsSubclassOf(typeof(RoomCurrentTimeFieldVariable)))
            .Select(t =>
                ((WiredInternalVariable)Activator.CreateInstance(t, _room.Harness.Room)!)
                    .GetVarSnapshot()
                    .VariableName
            );

        names
            .Should()
            .BeEquivalentTo(
                "@current_time.milliseconds_of_seconds",
                "@current_time.seconds_of_minute",
                "@current_time.minute_of_hour",
                "@current_time.hour_of_day",
                "@current_time.day_of_week",
                "@current_time.day_of_month",
                "@current_time.day_of_year",
                "@current_time.week_of_year",
                "@current_time.month_of_year",
                "@current_time.year"
            );
    }

    [Fact]
    public void The_editor_lists_them_after_current_time_milliseconds_first()
    {
        // The editor lists variables by id, highest first.
        var listed = typeof(RoomCurrentTimeFieldVariable)
            .Assembly.GetTypes()
            .Where(t => !t.IsAbstract && t.IsSubclassOf(typeof(RoomCurrentTimeFieldVariable)))
            .Append(typeof(RoomCurrentTimeVariable))
            .Select(t =>
                (
                    (WiredInternalVariable)Activator.CreateInstance(t, _room.Harness.Room)!
                ).GetVarSnapshot()
            )
            .OrderByDescending(x => x.VariableId.Value)
            .Select(x => x.VariableName);

        listed
            .Should()
            .Equal(
                "@current_time",
                "@current_time.milliseconds_of_seconds",
                "@current_time.seconds_of_minute",
                "@current_time.minute_of_hour",
                "@current_time.hour_of_day",
                "@current_time.day_of_week",
                "@current_time.day_of_month",
                "@current_time.day_of_year",
                "@current_time.week_of_year",
                "@current_time.month_of_year",
                "@current_time.year"
            );
    }

    private void SetTimeZone(string id)
    {
        var info = (RoomSnapshot)RuntimeHelpers.GetUninitializedObject(typeof(RoomSnapshot));

        RoomHarness.SetMember(info, "WiredTimezone", id);
        RoomHarness.SetMember(_room.Harness.State, "RoomSnapshot", info);
    }

    private static long Read(WiredInternalVariable variable)
    {
        var snapshot = variable.GetVarSnapshot();

        snapshot.TargetType.Should().Be(WiredVariableTargetType.Global);
        variable
            .TryGetValue(
                new WiredVariableKey(snapshot.VariableId, WiredVariableTargetType.Global, 0),
                out var value
            )
            .Should()
            .BeTrue();

        return value;
    }
}

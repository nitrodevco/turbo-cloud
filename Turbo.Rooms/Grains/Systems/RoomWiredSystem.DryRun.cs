using System.Threading;
using System.Threading.Tasks;
using Turbo.Primitives.Rooms.Events;
using Turbo.Primitives.Rooms.Wired;
using Turbo.Rooms.Wired;

namespace Turbo.Rooms.Grains.Systems;

/// <summary>
/// Asking a stack what it would do, without running it: a Says Keyword box hides a line only
/// when its stack would take the line, and the chat path has to know before the line is shown.
/// </summary>
public sealed partial class RoomWiredSystem
{
    /// <summary>
    /// Whether <paramref name="trigger"/> would fire its stack's effects for
    /// <paramref name="evt"/> now: wired is on, the trigger takes the event and the stack's
    /// conditions pass. The selectors and add-ons are applied to a throwaway context and nothing
    /// runs or flashes; an add-on that keeps a count (Execution Limit) is asked without counting.
    /// A stack not built yet (the box was just placed or saved) cannot be asked: the trigger
    /// alone then decides, as the stack will be asked once it is built.
    /// </summary>
    public async Task<bool> WouldPassAsync(
        IWiredTrigger trigger,
        RoomEvent evt,
        CancellationToken ct
    )
    {
        if (_disabledBy.Count > 0 || IsKilled(NowMs()))
            return false;

        foreach (var stack in _stacksById.Values)
        {
            if (stack is null || !stack.Triggers.Contains(trigger))
                continue;

            if (!await trigger.MatchesEventAsync(evt, ct))
                return false;

            var ctx = await CreateFiringContextAsync(trigger, evt, stack, dryRun: true, ct);
            var outer = _contextValues;
            var outerRunning = _running;

            _contextValues = ctx.ContextValues;
            _running = new WiredRunningExecution(
                ctx.SelectorPool,
                ctx.Signal,
                ctx.Event,
                ctx.Trigger
            );

            try
            {
                return await DecideAsync(ctx, calledByStack: false, ct) == true;
            }
            finally
            {
                _contextValues = outer;
                _running = outerRunning;
            }
        }

        return await trigger.MatchesEventAsync(evt, ct);
    }
}

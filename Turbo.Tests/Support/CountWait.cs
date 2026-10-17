namespace Turbo.Tests.Support;

/// <summary>
/// Waits for something told, not awaited (a send, an event), to have happened a given number of
/// times. Reaching the count is not enough for a test that asserts exactly that many (or none):
/// nothing here signals that every told publish has finished, so it then waits until the count has
/// held still for <see cref="Quiet"/>, and a wrong extra arriving in that time fails the test.
/// That quiet window is the one fixed wait; the rest ends as soon as the count is reached.
/// </summary>
public static class CountWait
{
    public static readonly TimeSpan Quiet = TimeSpan.FromMilliseconds(100);

    private static readonly TimeSpan Bound = TimeSpan.FromSeconds(5);

    public static async Task ForAsync(Func<int> count, int expected)
    {
        // Lets what is already queued run first (under SessionHarness.InTurns, every turn queued).
        await Task.Yield();

        var started = DateTime.UtcNow;

        while (count() < expected && DateTime.UtcNow - started < Bound)
            await Task.Delay(10);

        var seen = count();
        var stillSince = DateTime.UtcNow;

        while (DateTime.UtcNow - stillSince < Quiet && DateTime.UtcNow - started < Bound + Quiet)
        {
            await Task.Delay(10);

            if (count() != seen)
            {
                seen = count();
                stillSince = DateTime.UtcNow;
            }
        }
    }
}

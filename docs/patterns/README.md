# Pattern Samples

Reference implementations used for AI prompts and contributor consistency.
Start from these shapes and adapt to the target module.

- `HandlerPattern.cs`: packet handler shape — mirrors
  `Turbo.PacketHandlers/Navigator/CanCreateRoomMessageHandler.cs`
- `ServicePattern.cs`: domain service orchestration shape — mirrors `Turbo.Navigator/NavigatorService.cs`
- `UnitTestPattern.cs`: edge-case-first unit test shape

**No project compiles these files.** `TurboCloudQualityGate` checks only that they exist, so a
type they name can be renamed or deleted and nothing will say so — which is how the previous
versions came to return a `PlayerSummary` that has not existed for some time, and to show a
handler shape (sealed, `ct.ThrowIfCancellationRequested()`, a null check on the parsed message)
that none of the 502 real handlers uses. Each sample now names the real file it mirrors: read
that file, not just the sample, and when you change a convention, change the sample in the same
commit.

`UnitTestPattern.cs` is the exception in another way: the solution has no test project and
neither `xunit` nor `FluentAssertions` is in `Directory.Packages.props`, so it describes the
shape to write tests in rather than something that can run today.

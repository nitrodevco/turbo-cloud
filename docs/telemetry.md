# Local OpenTelemetry

Telemetry is disabled by default. Enable it in `appsettings.Development.json` or with environment variables, then start the local Aspire dashboard:

```powershell
./scripts/telemetry/start.ps1
```

The dashboard UI is at <http://localhost:18888>. Its browser login token is printed in the container logs:

```powershell
docker logs turbo-aspire-dashboard
```

The Compose file pins the dashboard image by digest and binds the UI and OTLP/gRPC receiver to loopback only. The dashboard keeps browser authentication enabled by default. Stop it with `./scripts/telemetry/stop.ps1`.

Enable export in `appsettings.Development.json`:

```json
{
  "Turbo": {
    "Telemetry": {
      "Enabled": true,
      "Endpoint": "http://localhost:4317",
      "ServiceName": "turbo-cloud",
      "TraceSampleRatio": 1.0
    }
  }
}
```

The same values can be set with `TURBO__Telemetry__Enabled=true`, `TURBO__Telemetry__Endpoint`, `TURBO__Telemetry__ServiceName`, and `TURBO__Telemetry__TraceSampleRatio`. The endpoint must be an absolute HTTP or HTTPS URI. The sample ratio must be between 0 and 1. OTLP uses gRPC; use the dashboard's loopback port `4317`.

Traces subscribe to `Turbo.Rooms` and Orleans application and lifecycle sources. Metrics subscribe to `Turbo.Rooms`, `Microsoft.Orleans`, and `System.Runtime`. `ISiloBuilder.AddActivityPropagation` is enabled only with telemetry. The host is disposed during shutdown so the OTLP batch processors can flush.

## Room telemetry

Room entry is measured from both direct and navigator entry roots. Stages identify where time is spent:

- `room.entry.direct` and `room.entry.navigator`
- `room.entry.access`, `room.activate`, `room.hydrate`
- `room.entry.prepare_player`, `room.entry.player_summary`
- `room.load.map`, `room.load.furniture`, `room.load.pets`, `room.load.bots`, `room.load.permissions`
- `room.entry.view`, `room.entry.queue_initial_packets`
- `room.entry.membership`, `room.entry.subscribe`, `room.entry.avatar`

The `turbo.room.operation.duration` histogram is measured in seconds and tagged with stage and outcome. The `turbo.room.stream.delivery.delay` histogram is measured in seconds from UTC publication to eligibility for presence subscribers; it does not measure socket receipt or client rendering. Clock synchronization is required for meaningful cross-process stream delay values.

Custom room metrics never carry room or player identifiers. Application room spans may carry the numeric `room.id` attribute to correlate stages for a room. Orleans propagation adds its RPC service/method and target/source grain IDs to grain-call spans, so Orleans spans can contain grain identifiers beyond `room.id`. Orleans also creates exception message and full stacktrace tags on failed sampled calls; the host removes `exception.message`, `exception.stacktrace`, `exception.stack_trace`, and `error.message` before export while retaining exception type and error status. Do not add player identity, packet contents, SQL text, or other user payload to application telemetry. This host does not enable SQL or packet-content instrumentation.

The `System.Runtime` meter includes .NET runtime and GC measurements such as allocation, heap size, collection counts, and pause time. Orleans spans provide application grain-call and lifecycle context, while `Microsoft.Orleans` exports its runtime meters including activation counts, message activity, and request latency.

## Command telemetry

Chat commands are measured by source and meter Turbo.Commands, subscribed with the room telemetry. A line that runs a command makes a command.execute span, tagged command.name, oom.id and command.outcome, and records 	urbo.command.duration (seconds) tagged command and outcome. command is the registered name, so its cardinality is the number of loaded commands. outcome is one of completed, efused, oom_level, ind_failed, etoed, lood, rror or canceled. A command holds its room's turn, so the duration is how long that room waited; one over Turbo:Rooms:CommandSlowWarningMs is also logged as a warning. Lines that are not commands are not measured, and cost nothing when telemetry is off. Spans and metrics never carry a player, the arguments or a reply.

## Reproducible room check

Run the focused regression tests (these use local listeners and do not export to the dashboard):

```powershell
dotnet test Turbo.Tests/Turbo.Tests.csproj --filter FullyQualifiedName~RoomTelemetryTests
```

For a live check, enable telemetry, start the dashboard and Turbo, then enter a fresh room once to produce a cold activation, exit, and enter it again for a warm activation. In the dashboard, select the `turbo-cloud` service and compare the room entry stages for both traces. Confirm the duration histogram includes stage/outcome dimensions and the stream delay is present only where a publication timestamp was captured. The automated tests exercise the real entry handler and room service with fake grain boundaries. A completed operation means the server handled the request; access may still be denied or require a doorbell. These measurements do not infer client receipt or rendering time.


## Checking rejected-entry work

A denied direct entry should contain `room.entry.access` but no `room.activate` or
`room.load.*` stages. Room grain activation can still hydrate metadata, rights and bans;
this optimization skips content loading, not all database work.

Allowed entry checks access, loads contents, then checks access again in case bans,
capacity or deletion changed during loading. Expect two access spans for an allowed
attempt; compare `room.entry.direct` durations when comparing whole requests. Loading
still happens before leaving the player's old room. A later doorbell approval also
ensures contents are ready before sending the entry view.

The focused regression check is:

```powershell
dotnet test Turbo.Tests/Turbo.Tests.csproj --filter FullyQualifiedName~RoomEntryPreparationTests
```

For a gameplay comparison, use the same inactive furnished password room and an incorrect
password on both branches, then compare the denied-entry traces. Measure successful cold
and warm entries separately: the extra access check is a tradeoff, not a promised speedup
for successful entry.

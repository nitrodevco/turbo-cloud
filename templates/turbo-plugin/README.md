# Skills

A Turbo plugin with both halves in one folder:

```
server/          the server half (C#): SkillsPlugin (its services), its settings, permission node,
                 :skills command, messages and their handlers, a weekly scheduled task, all found
                 as it loads
client/src/      the client module (TypeScript + React): a window, a toolbar icon, a patch to the purse;
                 made/ holds what `npm run make` adds
wwwroot/         files clients load beside the module (its icon), and template-patches.json: changes to
                 core windows as data, no code (nitro-next docs/template-patches.md); the built
                 module lands here in dist/
gamedata/        the plugin's texts, given to the hotel as it loads (staff can change them)
tests/           server tests (xunit) and client/tests (node --test)
manifest.json    key, version, assembly
```

## Three commands

```sh
dotnet new install <turbo-cloud>/templates/turbo-plugin        # once per machine
dotnet new turbo-plugin -n Skills --key skills --turbo <turbo-cloud> --nitro <nitro-next>
cd Skills && npm run dev
```

`npm run dev` installs the client's packages the first time, builds both halves into
`dist/skills/` and keeps it current. It first checks the server it builds against (`TurboBin`, see
[The rest](#the-rest)): one older than this plugin is named as the cause, with what to rebuild.

`npm run clean-sample` takes the sample (the skill book, its command, messages, weekly reset,
window, purse button and their tests and texts) out, leaving an empty plugin to build up with
`npm run make`. Run it once, before the plugin is yours.

## The hotel it runs on

A hotel in the Development environment, with the hosts a plugin uses turned on (they are off by
default). One block for the server's `appsettings.Development.json`, or the same keys as
environment variables (`Turbo__Gamedata__Enabled=true`, ...):

```json
{
  "Turbo": {
    "Plugin": { "DevPluginPaths": [ "<this folder>/dist/skills" ] },
    "Gamedata": { "Enabled": true, "PublicUrl": "http://127.0.0.1:8094" },
    "Web": {
      "Enabled": true,
      "ClientUrl": "http://localhost:5173/?sso={ticket}",
      "PluginRoutes": { "PublicUrl": "http://127.0.0.1:8092" }
    },
    "Admin": { "Enabled": true, "PanelUrl": "http://localhost:5174" },
    "Owner": { "Name": "<your player name>" }
  }
}
```

| Key | Why |
| --- | --- |
| `Plugin:DevPluginPaths` | The hotel loads the plugin from `dist/` and reloads it as it changes. |
| `Gamedata` | Serves the client module and `wwwroot/` (`/gamedata/plugins/skills/`) and the plugin's texts. `PublicUrl` makes the module's address absolute, so a client on another port can load it. |
| `Web` | Serves the plugin's web routes (`/api/plugins/skills/...`, turbo-cloud `docs/plugin-web-routes.md`). Clients call them at `PluginRoutes:PublicUrl`; `ClientUrl` is the client's address, whose origin may call them. |
| `Admin` | The admin API, for the panel's Settings and Plugins pages (Schedule, Run now). `PanelUrl` is the panel's own address: passkeys are bound to it. The client's dev server has 5173, so run the panel on another port (`yarn dev --port 5174` in turbo-admin). |
| `Owner:Name` | Your player becomes the hotel's owner: the `admin` group, and at the next start a setup link for your panel passkey in the server log (turbo-cloud `docs/admin-panel.md`, "The first admin"). |

The client (nitro-next) loads a module from another origin only when its configuration names it:
`"client.modules.hosts": [ "127.0.0.1:8094" ]` beside its `socket.url` (nitro-next
`docs/client-modules.md`). The admin API takes `Authorization: Bearer <session token>`, the token
a panel sign-in gives (turbo-cloud `docs/admin-panel.md`, "Calling the admin API").

As it runs:

- A saved `.cs` file rebuilds the server half; the hotel reloads the plugin (no restart).
- A saved client file rebundles the module; the hotel sends clients its new version and they run
  it in place (no page reload).
- `gamedata/`, `wwwroot/` and `manifest.json` are copied as they change.

## Recipes

`npm run make <kind> <name>` adds one piece where the server and the client find it by themselves:
nothing to register, and it goes when the plugin unloads. It prints the files it wrote and the
guide to read (turbo-cloud `docs/`).

| Command | Writes | Found by |
| --- | --- | --- |
| `npm run make command hug` | `server/Commands/HugCommand.cs`: `:hug` and its permission node `skills.command.hug` | the command and permission scans |
| `npm run make message levelled --to client --fields level:int,name:string` | `server/Messages/LevelledMessage.cs` (the record) and `client/src/made/skills.levelled.ts` (its name, schema, type and a listener) | the message scan; `client/src/made/index.ts` |
| `npm run make message ping` | the record, its handler and a `sendPing` for the client (`--to server`, the default) | the message and handler scans |
| `npm run make logic skill_stove` | `server/Logic/SkillStoveLogic.cs`: the logic of furniture whose logic is `skill_stove` (`FurniLogic`: use, walk on, walk off) | the plugin logic scan |
| `npm run make wired effect whisper_room` | `server/Wired/WhisperRoomEffect.cs`: the wired box `wf_act_whisper_room` (`trigger`, `condition`, `selector` for the others; a trigger comes with its event record, fired by `hotel.Wired.FireAsync`) | the plugin logic scan |
| `npm run make listener entered` | `server/Listeners/EnteredListener.cs`: hears every room's events | the room event listener scan |
| `npm run make setting rate --type int --default 5` | `Rate` in the `[PluginSettings]` class (`int`, `long`, `bool`, `string`, `double`) | the panel's Settings page |
| `npm run make route rares/values --anonymous` | `server/Routes/RaresValuesRoute.cs`: `GET /api/plugins/skills/rares/values` (`--method POST`) | the web route scan |
| `npm run make window news` | `client/src/made/NewsWindow.tsx`, opened by the link `skills/news`, and its title text | `client/src/made/index.ts` |
| `npm run make patch habbo-friend-list-com/friend_list_xml` | an entry in `wwwroot/template-patches.json` to fill with ops | the client, as the plugin loads |
| `npm run make migration AddPrizes` | `server/Database/SkillsDb.cs` (once; its `TablePrefix` in `manifest.json`) and the migration, by `dotnet ef migrations add` | the host, which migrates the tables as the plugin loads |
| `npm run make schedule weekly-rotation --cron "0 18 * * MON"` | `server/Tasks/WeeklyRotation.cs`: a scheduled task (`--every 00:05:00` for an interval) | the scheduled task scan |
| `npm run make policy purchase club-discount` | `server/Policies/ClubDiscountPolicy.cs`: allows, refuses or changes a purchase (`room-entry`, `placement`, `registration`, `avatar-effect`, `purchase-fulfilment`, `public-room-order` for the others) | the game policy scan |

`make migration` needs `dotnet ef` once per machine (`dotnet tool install --global dotnet-ef`) and
a built server (`TurboBin`): the context needs no design-time factory. The first run writes the
context with a placeholder row class (`[Table("rows")]`) and its migration: to start from your own
table, run `npm run make migration Init` once, edit the row class, `dotnet ef migrations remove`, and
run it again, or write the context first (turbo-cloud `docs/database.md`). Furni logic and wired
boxes are built against `Turbo.Contracts` alone (turbo-cloud `docs/plugin-wired.md`); the furniture
definition whose logic is the key is what the hotel places.

## The rest

| Command | What |
| --- | --- |
| `npm run build` | The same build once |
| `npm test` | `dotnet test tests/Skills.Tests` and the client tests |
| `npm run typecheck` | The client module's types |
| `npm run package` | A Release build and `dist/skills-<version>.zip`, the package the admin panel's plugin upload takes |

The server half builds against a Turbo server's own assemblies, never copied beside the plugin:
`TurboBin` (`-p:TurboBin=...` or the `TurboBin` environment variable) is the folder holding them,
by default the `--turbo` checkout's `Turbo.Main/bin/Debug/net10.0`. Point it at the bin folder of the
server you run. `node tools/host.mjs` checks it, as `npm run dev`, `build`, `test` and
`make migration` do before they build: a server built before something the plugin uses (the
scheduler, say) is named as the cause, with the namespace it lacks and what to rebuild.

The client module is written against `@nitrodevco/client-module-sdk` (nitro-next
`docs/client-modules.md`, "Writing a module with the SDK"): `defineModule`, `useApi`, the client's
own React and theme. `api.links.handle(prefix, ...)` adds a handler: every handler of a prefix gets
each of its links, so `index.tsx`'s and the ones `make window` writes share the plugin's key. The
purse button (`skills.purse.button` in `gamedata/external_texts.txt`) is as wide as the purse's
help button, 50 pixels, and the purse has no room for more: a caption longer than about seven
letters is cut off. The two halves talk in named messages (turbo-cloud `docs/plugin-messages.md`):
records with `[PluginMessage("skills.<name>")]` in `server/SkillsMessages.cs`, read and sent by name
in `client/src/skill.ts` (`api.messages.on/send`). The server numbers them and tells the client, so
there is nothing to keep in step but the names and fields.

The server half reaches the hotel (players, rooms, wallet, furni, notices) through `IHotel`, taken
by constructor (`SkillBook` alerts a player who levels up); its tests use `FakeHotel` from
`Turbo.Testing.dll` in the same bin folder, with assertions such as
`hotel.Notify.AssertAlerted(alice, SkillBook.LEVELLED)` (turbo-cloud `docs/hotel-api.md`).

## The plugin's tables from a route or a task

The plugin's database context is scoped (one per request), and its routes, scheduled tasks,
commands, listeners and singletons are built once, as the plugin loads. So they take
`IServiceScopeFactory` and read the tables in a scope of their own; one that takes the context by
constructor does not load, and the plugin's status says so.

```csharp
public sealed class ValuesRoute(IServiceScopeFactory scopes) : IPluginWebRoute
{
    public string Method => "GET";

    public string Pattern => "values";

    public async Task<PluginWebResult> HandleAsync(PluginWebRequest request, CancellationToken ct)
    {
        await using var scope = scopes.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<SkillsDb>();

        return PluginWebResult.Ok(await db.Rows.CountAsync(ct));
    }
}
```

## A scheduled task

`server/SkillsWeeklyReset.cs` is a job the hotel runs on a schedule: a public class implementing
`IScheduledTask` with `[Schedule("<cron>")]` or `[Every("<interval>")]`. Nothing registers it: the
server finds it as the plugin loads, runs it once per cluster (never twice at once) and takes it
away on unload. The admin panel's Plugins page lists it with its last and next run and a Run now
button. `npm run make schedule <name>` writes another; by hand it is:

```csharp
[Schedule("0 18 * * MON")]                 // Mondays 18:00; or [Every("00:05:00")]
public sealed class WeeklyRotation(MyService service) : IScheduledTask
{
    public Task RunAsync(ScheduledTaskRun run, CancellationToken ct) => service.RotateAsync(ct);
}
```

A task about what one server holds in memory (its online players, a cache) says
`[Every("00:00:30", PerServer = true)]` (`--per-server`): every server runs it on its own clock.
Recipes, the cron format and the schedule-from-a-setting form: turbo-cloud `docs/plugin-scheduler.md`.

## Once the hotel is up, and until the plugin stops

`StartingAsync` runs as the plugin loads, which at boot is before the hotel has started. Work that
needs the whole hotel goes in `HotelReadyAsync(ctx, ct)`: called once the hotel has started, and
at once after a reload. Work the plugin starts itself takes `ctx.Stopping`, cancelled as the
plugin starts to stop; no token source of its own, no `ApplicationStarted.Register`:

```csharp
protected override Task HotelReadyAsync(IPluginContext ctx, CancellationToken ct) =>
    ctx.Services.GetRequiredService<MyService>().WarmUpAsync(ct); // ct is ctx.Stopping
```

A service the plugin registers (`services.AddSingleton<SkillBook>()`, a hosted service) takes host
services by constructor as its commands do (`IGrainFactory`, `TimeProvider`, `IHotel`), with no
factory.

## A policy, and what happened

A policy decides before something is done: a public class implementing `IPurchasePolicy`,
`IRoomEntryPolicy` or `IPlacementPolicy` (`Turbo.Primitives.Policies`) allows, refuses with a reason
the player is shown, or changes the action (a purchase's price, a room to go to instead):

```csharp
public sealed class ClubDiscountPolicy(IHotel hotel) : IPurchasePolicy
{
    public async ValueTask<PurchaseDecision> DecideAsync(
        PurchaseRequest request, PurchaseDecision proposed, CancellationToken ct) =>
        (await hotel.Players.ClubAsync(request.PlayerId, ct)).Active
            ? proposed with { CostCredits = proposed.CostCredits / 2 }
            : proposed;
}
```

Two kinds do more than decide: an `IAvatarEffectPolicy` chooses the effect an avatar wears and the
one each player is shown on it (a command that turns effects off), and an `IPurchaseFulfilment`
hands over an offer or room ad in the hotel's place, once the hotel has taken its price.

What happened afterwards is an event: an `IEventHandler<PurchaseCompletedEvent>` (or
`PlayerLoggedInEvent`, `PlayerRegisteredEvent`, `RoomEnteredEvent`) hears it once it is done, never
for a refused action, and cannot undo it. Both are found as the plugin loads, and both are in
`Turbo.Primitives`, which the project references already (`using Turbo.Events.Registry;` for the
handler): turbo-cloud `docs/plugin-policies.md`. So are the plugin's own publishing
(`IEventPublisher` from the host services: `docs/plugin-notices.md`) and a handler of a client's
packet (`IMessageHandler<T>` and `MessageContext`, `using Turbo.Messages.Registry;`:
`docs/plugin-packets.md`).

Guides (turbo-cloud `docs/`): `extensions.md` (the model), `hotel-api.md`, `client-modules.md`, `plugin-messages.md`,
`commands.md`, `permissions.md`, `settings.md`, `database.md` (when the plugin keeps data),
`plugin-scheduler.md`, `plugin-policies.md`.

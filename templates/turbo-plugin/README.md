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
`dist/skills/` and keeps it current. Name that folder once in the hotel's settings and run the
server in Development:

```json
{ "Turbo": { "Plugin": { "DevPluginPaths": [ "<this folder>/dist/skills" ] } } }
```

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
| `npm run make logic skill_stove` | `server/Logic/SkillStoveLogic.cs`: the logic of furniture whose logic is `skill_stove` | the room object logic scan |
| `npm run make listener entered` | `server/Listeners/EnteredListener.cs`: hears every room's events | the room event listener scan |
| `npm run make setting rate --type int --default 5` | `Rate` in the `[PluginSettings]` class (`int`, `long`, `bool`, `string`, `double`) | the panel's Settings page |
| `npm run make route rares/values --anonymous` | `server/Routes/RaresValuesRoute.cs`: `GET /api/plugins/skills/rares/values` (`--method POST`) | the web route scan |
| `npm run make window news` | `client/src/made/NewsWindow.tsx`, opened by the link `skills/news`, and its title text | `client/src/made/index.ts` |
| `npm run make patch habbo-friend-list-com/friend_list_xml` | an entry in `wwwroot/template-patches.json` to fill with ops | the client, as the plugin loads |
| `npm run make migration AddPrizes` | `server/Database/SkillsDb.cs` (once; its `TablePrefix` in `manifest.json`) and the migration, by `dotnet ef migrations add` | the host, which migrates the tables as the plugin loads |

`make migration` needs `dotnet ef` once per machine (`dotnet tool install --global dotnet-ef`) and
a built server (`TurboBin`): the context needs no design-time factory. Furni logic derives from
`FurnitureFloorLogic` in `Turbo.Rooms`, which `make logic` adds a reference to.

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
server you run.

The client module is written against `@nitrodevco/client-module-sdk` (nitro-next
`docs/client-modules.md`, "Writing a module with the SDK"): `defineModule`, `useApi`, the client's
own React and theme. The two halves talk in named messages (turbo-cloud `docs/plugin-messages.md`):
records with `[PluginMessage("skills.<name>")]` in `server/SkillsMessages.cs`, read and sent by name
in `client/src/skill.ts` (`api.messages.on/send`). The server numbers them and tells the client, so
there is nothing to keep in step but the names and fields.

The server half reaches the hotel (players, rooms, wallet, furni, notices) through `IHotel`, taken
by constructor (`SkillBook` alerts a player who levels up); its tests use `FakeHotel` from
`Turbo.Testing.dll` in the same bin folder, with assertions such as
`hotel.Notify.AssertAlerted(alice, SkillBook.LEVELLED)` (turbo-cloud `docs/hotel-api.md`).

## A scheduled task

`server/SkillsWeeklyReset.cs` is a job the hotel runs on a schedule: a public class implementing
`IScheduledTask` with `[Schedule("<cron>")]` or `[Every("<interval>")]`. Nothing registers it: the
server finds it as the plugin loads, runs it once per cluster (never twice at once) and takes it
away on unload. The admin panel's Plugins page lists it with its last and next run and a Run now
button. Another one is a copy of that file:

```csharp
[Schedule("0 18 * * MON")]                 // Mondays 18:00; or [Every("00:05:00")]
public sealed class WeeklyRotation(MyService service) : IScheduledTask
{
    public Task RunAsync(ScheduledTaskRun run, CancellationToken ct) => service.RotateAsync(ct);
}
```

Recipes, the cron format and the schedule-from-a-setting form: turbo-cloud `docs/plugin-scheduler.md`.

Guides (turbo-cloud `docs/`): `extensions.md` (the model), `hotel-api.md`, `client-modules.md`, `plugin-messages.md`,
`commands.md`, `permissions.md`, `settings.md`, `database.md` (when the plugin keeps data),
`plugin-scheduler.md`.

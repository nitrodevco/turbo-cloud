# Skills

A Turbo plugin with both halves in one folder:

```
server/          the server half (C#): SkillsPlugin, its settings, permission node, :skills command,
                 packets and their handlers
client/src/      the client module (TypeScript + React): a window, a toolbar icon, a patch to the purse
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
own React and theme. The packets are numbered by hand in `server/SkillsMessages.cs` and
`client/src/skill.ts`; keep the two in step and pick numbers no other plugin uses.

Guides (turbo-cloud `docs/`): `extensions.md` (the model), `client-modules.md`, `plugin-packets.md`,
`commands.md`, `permissions.md`, `settings.md`, `database.md` (when the plugin keeps data).

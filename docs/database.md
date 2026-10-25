# The database and its migrations

Turbo keeps its state in MySQL or MariaDB, and the schema moves forward with the server through
EF Core migrations. **By default the server migrates itself**: start it against an empty database,
or one made by an older version, and it brings the database up to date, says what it did, and
carries on. Nothing else is needed to get going, and nothing needs the .NET SDK on the server.

```
Database core: creating the database, 47 migration(s)
Database core: applying 20260208101510_InitialCreate (1/47)
Database core: applied 20260208101510_InitialCreate in 2.9 s
...
Database core: up to date at 20261006013342_AddHotelSettings, 47 migration(s) applied in 21.6 s
```

## Getting a database

Anything MySQL 8 or MariaDB 10.6+ will do, with a user that may create the database (or create the
database yourself and give the user rights on it). For local work, Docker is the quickest:

```bash
docker run -d --name turbo-mysql -e MYSQL_ROOT_PASSWORD=turbo-dev -e MYSQL_DATABASE=turbo \
  -p 127.0.0.1:3306:3306 mysql:8.4 --character-set-server=utf8mb4 --collation-server=utf8mb4_unicode_ci
```

then set `Turbo:Database:ConnectionString` (`appsettings.Development.json`, or the environment as
`Turbo__Database__ConnectionString`):

```
server=127.0.0.1;port=3306;user=root;password=turbo-dev;database=turbo
```

and start Turbo. That is the whole setup.

## The settings (`Turbo:Database`)

| Setting | Default | What it does |
| --- | --- | --- |
| `Migrate` | `Auto` | `Auto` applies what is pending at startup. `Check` applies nothing and refuses to start while anything is pending. `Off` does not look at the schema. |
| `AllowDestructiveMigrations` | `false` | Whether a migration that drops a table or a column may run on a database that has data. |
| `MigrationLockSeconds` | `60` | How long a migration waits for another one to finish. |
| `MigrationCommandTimeoutMinutes` | `30` | How long one statement of a migration may run. The usual 30 seconds is too short for an `ALTER` on a table with millions of rows. |
| `ServerVersion` | _(ask the server)_ | `mysql 8.4.0` or `mariadb 11.4.0`. Skips the question at startup, and lets `migrate --script` run with no connection. |

In the Ploi environment these are `TURBO_DB_MIGRATE`, `TURBO_DB_ALLOW_DESTRUCTIVE_MIGRATIONS`,
`TURBO_DB_MIGRATION_LOCK_SECONDS` and `TURBO_DB_SERVER_VERSION`.

`Auto` is right for a hotel on one server, which is nearly all of them. `Check` is for a hotel
whose deploy runs `Turbo.Main migrate` first (the Ploi scripts do) and wants the server to refuse a
database that is behind rather than quietly fix it. `Off` is for whoever owns the schema by hand.

## What the server will not do

A migration is never "just run". It stops, says why in one readable message, and exits with a
non-zero code (so a supervisor can see the server did not come up), in these cases:

- **The database was made by a newer version.** It has migrations this build does not ship.
  Migrations cannot run backwards, so an older server must not touch it: run the newer version, or
  restore a backup taken before the upgrade. (Minecraft refuses a world saved by a newer version
  for the same reason.) Applies in `Auto` and `Check`.
- **A migration would delete data.** A pending migration that drops a table or a column is
  refused on a database that already has migrations applied. Back up, then set
  `AllowDestructiveMigrations` to `true` (or run `Turbo.Main migrate --allow-destructive`) for
  that one start. A brand new database is never asked: there is nothing to lose.
- **Another process is migrating.** One migrator per database at a time; a second waits up to
  `MigrationLockSeconds`, then stops and says so.
- **Nothing can be reached.** No connection string, or a server that is not running, is reported as
  that, with the setting to check, instead of a stack trace.
- **A migration fails.** The message names it and says how many earlier ones were applied. MySQL
  cannot roll back a schema change, so the database is left as the failure found it. Fix the cause
  (or restore a backup) and start again: applying resumes at that migration.

Before applying to a database that has data, the server warns that a backup is the only way back.
Take one.

## `Turbo.Main migrate`

The same migrator, run by hand, for a deploy step or a look at where things stand:

```bash
dotnet Turbo.Main.dll migrate                    # apply what is pending, then exit
dotnet Turbo.Main.dll migrate --status           # exit 0 up to date, 2 behind, 1 on a problem
dotnet Turbo.Main.dll migrate --script turbo.sql # write the SQL for a DBA to review or run
dotnet Turbo.Main.dll migrate --allow-destructive
```

- It starts nothing else: no sockets, no plugins, no game.
- `--script` writes every migration as SQL that is safe to run on a database at any earlier
  migration, and twice (an idempotent script), and changes nothing. With `Turbo:Database:ServerVersion`
  set it needs no connection at all.
- It migrates the emulator's own tables. **Plugins' tables are migrated when each plugin loads**,
  by the same migrator, so the same settings (and the same refusals) apply to them. A plugin whose
  tables cannot be migrated (a database from a newer version, a destructive migration not allowed,
  or `Migrate` set to `Check` while it is behind) is **not loaded**: the server carries on without
  it and the log says why. For that reason `Check` suits a hotel with no plugins that have tables;
  with plugins, leave `Auto`.
- It ignores `Migrate=Off` and `Check`: asking for a migration is the operator deciding.

## Several things at once

One lock per database (MySQL's `GET_LOCK`, taken on a connection of its own) covers the emulator
and every plugin. Two servers started together, or a server started while a deploy migrates, take
turns, and the one that waited reads the state again once it has the lock, so nothing is applied
twice. The server releases the lock when its connection ends, so a migrator that is killed
mid-way never leaves the next start waiting on a lock nobody holds.

## For people writing migrations

The migrator's guarantees rest on how migrations are written:

- **A shipped migration is never edited.** A hotel that has applied it will not apply it again.
  A fix is a new migration.
- **Migrations only move forward.** There is no promise that `Down` works; a backup is the way
  back.
- **Add the migration with the change.** `dotnet ef migrations add Name` from `Turbo.Database`
  (see `CONTRIBUTING.md`). A test fails if an entity changed and its migration is missing: that is
  the "Unknown column" that stops every login.
- **Prefer adding to taking away.** To rename or remove a column, add the new one in one release
  and remove the old one in a later one, once nothing reads it. A migration that drops anything
  makes every hotel with data stop and ask its operator; that should be a deliberate release.
- **Seed data is idempotent.** Insert what is missing, never assume the table is empty.
- **One logical change per migration**, named for it, so the log reads well.
- **Plugins** keep their own history table (`__EFMigrationsHistory_<prefix>`) and table prefix, and
  are migrated by the same migrator as they load, so they get all of the above.

## A plugin's tables

A plugin's context derives from `PluginDbContextBase<T>` (`Turbo.Database.Context`) and declares
its tables, nothing else:

```csharp
public sealed class SkillsDb : PluginDbContextBase<SkillsDb>
{
    public DbSet<SkillRow> Skills => Set<SkillRow>();
}
```

- As the plugin loads, the host finds the context in its assembly, registers it (scoped, as any
  context) and migrates its tables; an uninstall that drops the plugin's data drops them. Every
  table is named with the manifest's `TablePrefix` (`"skills_"`), and no foreign key cascades.
- `dotnet ef migrations add <Name>` in the plugin's project needs no design-time factory: with no
  host to give it its options, the context reads the prefix from the plugin's `manifest.json` (in
  the working folder, the build folder or above them) and names the history table by it. Making a
  migration needs no database; `-- --connection "<connection string>"` or `TURBO_DB` gives one,
  `TURBO_DB_VERSION` the server version (default `8.0.0-mysql`). The template's
  `npm run make migration <Name>` writes the context and runs it.
- A context the plugin's own tests build over another provider (SQLite) declares a constructor
  taking `DbContextOptions<TContext>` and `TablePrefixProvider` beside the parameterless one: the
  host then gives it its options through that constructor, and `dotnet ef` still uses the
  parameterless one.
- A context registered by hand (`services.AddPluginDatabaseContext<TContext, TModule>()`) is left
  as the plugin registers it.
- Seed rows go in `OnModelCreating` with EF Core's `HasData`, after `base.OnModelCreating` (which
  names the tables by the prefix); the next migration inserts them.

### From a route, a task or a singleton

The context is scoped: one per request, never shared between two at once. A plugin's web routes,
scheduled tasks, commands, room listeners, message handlers and singletons are built once, as the
plugin loads, and live as long as it does. So they never take the context by constructor; they take
`IServiceScopeFactory` and open a scope per request or run:

```csharp
public sealed class ValuesRoute(IServiceScopeFactory scopes) : IPluginWebRoute
{
    public string Method => "GET";

    public string Pattern => "values";

    public async Task<PluginWebResult> HandleAsync(PluginWebRequest request, CancellationToken ct)
    {
        await using var scope = scopes.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<SkillsDb>();

        return PluginWebResult.Ok(await db.Skills.CountAsync(ct));
    }
}
```

A service the plugin registers as a singleton (`services.AddSingleton<SkillBook>()`) does the same.
One that takes the context by constructor is refused as the plugin loads (its service provider
checks scopes): the plugin does not load, and its status on the panel's Plugins page and the log say
`Cannot resolve scoped service 'SkillsDb' from root provider`, or `Cannot consume scoped service`,
followed by this advice.

## Deploying with Ploi

`scripts/ploi/deploy.sh` runs the release's own `Turbo.Main migrate` before it switches the
running server over, so a failed migration stops the deploy with the old release still serving.
See `scripts/ploi/README.md`.

## When something is wrong

| You see | It means | Do |
| --- | --- | --- |
| `... has migrations this version of Turbo does not know` | The database was used by a newer Turbo. | Run the newer version, or restore a backup from before the upgrade. |
| `... pending migrations would delete some of it` | A release drops a table or column. | Back up, then allow destructive migrations for one start. |
| `Another process has been migrating this database` | A second Turbo or a deploy holds the lock. | Wait for it. The lock is freed the moment its holder's connection ends; if the holder died without closing it, MySQL frees it when it notices (see `SHOW PROCESSLIST`). |
| `... migration X failed` | X could not run; earlier ones are applied. | Fix what the message says, or restore a backup, and start again. |
| `... migration(s) behind` | `Migrate` is `Check` and the database is not current. | Run `Turbo.Main migrate`, or set `Migrate` to `Auto`. |

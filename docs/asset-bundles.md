# Asset bundles

The client draws furniture, avatar effects and pets from `.nitro` bundles: a zip holding the
library's asset data (`<name>.json`), its packed images (`<name>.png`) and their frames
(`<name>_spritesheet.json`). The hotel takes them from Habbo: a **sync** downloads each library
Habbo has that the hotel lacks, or has an older revision of, and converts it (`Turbo.Assets`,
`NitroConverter`). The bundles are kept on this server, and **published** to where the client loads
them: a folder on this server, or an FTP, FTPS or SFTP server. The admin panel's **Assets** page
does all of this, and checks the bundles against the hotel's furniture, effects and pets.

## Where bundles are kept

`Turbo:Assets:Directory` (default `assets`, under the server's folder) is laid out as an asset host
serves it, so it can itself be served, or published as it is:

| Kind | Folder | Named as | Client setting |
| --- | --- | --- | --- |
| Furniture | `bundled/furniture/<name>.nitro` | the classname without its `*N` colour | `furni.asset.url` |
| Effect | `bundled/effects/<lib>.nitro` | the effect map's `lib` | `avatar.asset.effect.url` |
| Pet | `bundled/pet/<name>.nitro` | the name `pet.configuration` lists | `pet.asset.url` |

Each bundle has a row in `asset_bundles`: its kind and name, the revision it was taken at, where it
came from (`habbo` or `upload`), its SHA-1 and size, the ids that load it (the effects sharing an
effect's library, a pet's type), and, when it has no file, why. Files are written beside their
place and moved over it, so nothing ever reads half of one. Every server converting into the folder
must share it.

## Syncing from Habbo

A sync (`Turbo.Gamedata/Assets`):

1. Reads Habbo's `external_variables` for the client revision (`flash-assets-<revision>` in
   `flash.client.url`) and the pet list (`pet.configuration`).
2. Lists what Habbo has:
   - **Furniture**, from Habbo's furnidata: each asset name, at the highest revision any item of it
     has. The file is `Turbo:Gamedata:FurnitureFileUrl`.
   - **Effects**, from `effectmap.xml`: each library, with the effect ids that use it and the highest
     revision. Files and the map are `Turbo:Assets:GordonFileUrl` (`{name}` is `effectmap.xml`, or
     `<lib>.swf`).
   - **Pets**, from `pet.configuration`: each name, its type being its place in the list, at the
     client revision.
3. Skips what is up to date: a library whose row has the same revision and a file, or failed at that
   revision in a way that won't pass (Habbo has no file, the file doesn't convert). An **upload is
   never replaced** by a sync.
4. Downloads the rest (`DownloadConcurrency` at once, retried as the gamedata client retries) and
   converts it (`ConvertConcurrency` at once): furniture and pets as they are, effects as `fx`. A
   download that failed is tried again on the next sync; a 404 or 403 is not, until the revision
   changes.

A Habbo check (the timer, or **Check now** on the Gamedata page) starts a sync when
`Turbo:Assets:SyncAfterCheck` is on and none is running. The first sync on an empty folder takes the
whole hotel: about 14,000 furniture libraries, which takes a while and several GB.

## Publishing

A publish target is kept in `asset_publish_targets`, edited on the Assets page:

| Field | |
| --- | --- |
| Name | What staff call it. |
| Protocol | `folder` (a path on this server), `ftp`, `ftps` (explicit TLS) or `sftp`. |
| Host, Port, User, Password | For FTP, FTPS and SFTP. Port 0 is the protocol's own. |
| Remote path | The folder the asset host serves from; `bundled/...` goes under it. A path on this server for `folder`. |
| Public URL | Where the client reaches it, for the addresses the panel shows. |
| Allow self-signed | FTPS only. |

The password is sealed with AES-GCM before it is saved, and never sent back to the panel; leaving it
empty when editing keeps it. The key is `Turbo:Assets:SecretKey` (32 bytes, base64), or, when that is
empty, one made once and kept in the bundle folder as `.publish-key`, so a database dump alone does
not give the passwords away. SFTP host keys are trusted on first connection and must match after; a
changed key refuses to connect until staff forget the old one.

A publish sends only what the target lacks or holds an older copy of, by the hash recorded for it in
`asset_published_files`, largest first, `PublishConcurrency` at once. Each file is uploaded beside its
place and renamed over it. What is sent is recorded as it goes, so a publish that stops resumes where
it was. A **dry run** only counts. **Delete removed** also deletes from the target what the hotel no
longer has. Each publish is kept in `asset_publishes`, the target's history.

## Checks

| Check | Severity | What it finds |
| --- | --- | --- |
| `furniture-missing` | error | Furniture definitions whose asset name has no bundle: the client draws a placeholder. |
| `file-missing` | error | Bundles whose row says they have a file that the folder doesn't have. |
| `failed` | warning | Libraries that could not be downloaded or converted, with why. |
| `effects-missing` | warning | Effects the catalog sells that no bundle carries. |
| `pets-missing` | warning | Pet types with breeds that have no bundle. |
| `furniture-unused` | warning | Furniture bundles no definition names. |

Each lists how many, and up to `CheckSampleLimit` of them.

## Settings (`Turbo:Assets`)

| Setting | Default | |
| --- | --- | --- |
| `Directory` | `assets` | Where bundles are kept. |
| `GordonFileUrl` | `https://images.habbo.{domain}/gordon/flash-assets-{revision}/{name}` | Where Habbo's client libraries and effect map are. |
| `SyncAfterCheck` | `true` | Sync after a Habbo check. |
| `DownloadConcurrency` | `6` | Downloads at once. |
| `ConvertConcurrency` | `4` | Conversions at once. |
| `PublishConcurrency` | `4` | Uploads at once. |
| `PublishTimeoutSeconds` | `30` | How long connecting to a target may take. |
| `UploadMaxMegabytes` | `64` | The largest file an upload in the panel may be. |
| `JobLogLimit` | `300` | Lines of a job's log kept. |
| `PageSize` | `60` | Bundles per page of the panel's list. |
| `CheckSampleLimit` | `25` | Examples each check lists. |
| `SecretKey` | empty | The key passwords are sealed with (secret). |

## Admin API

Under `/api/assets`. Reading needs `admin.gamedata.view`; changing needs `gamedata.manage` as well.
Kinds are `furniture`, `effect` and `pet`; protocols `folder`, `ftp`, `ftps`, `sftp`. Times are UTC.
A refusal is the panel's usual `{ "error": "..." }` with 400, 404 or 409.

**Job** (`AssetJob`): `{ id, kind: "sync"|"publish", title, status: "running"|"done"|"failed"|"canceled",
phase, total, done, failed, log: string[], error, result, playerId, startedAt, finishedAt }`. One job
runs at a time; starting another while one runs is 409.

| Call | Answer |
| --- | --- |
| `GET /assets` | `{ directory, canManage, kinds: [ { kind, bundles, failed, bytes } ], job: AssetJob \| null, checks: { errors, warnings }, targets }` |
| `GET /assets/bundles?kind=&q=&status=&page=` | `{ items: [ AssetBundle ], total, pageSize }`. `status` is `all`, `ok`, `failed` or `unused`; `q` matches the name, or an id. `page` from 0. |
| `GET /assets/bundles/{kind}/{name}` | `AssetBundle` and `{ path, files: [ { name, size } ] }`, what the zip holds. |
| `GET /assets/bundles/{kind}/{name}/file` | The `.nitro` file. |
| `POST /assets/bundles` | Multipart `file` (a `.swf`, `.hab` or `.nitro`), `kind`, and `name` (default: the file's). Converts and keeps it as an upload; answers the `AssetBundle`. |
| `DELETE /assets/bundles/{kind}/{name}` | 204. A Habbo library is downloaded again by the next sync. |
| `POST /assets/sync` | Starts a sync; answers the `AssetJob`. |
| `GET /assets/job` | The job running or last run; 204 before the first. |
| `POST /assets/job/cancel` | 204; 409 when none runs. |
| `GET /assets/checks` | `{ items: [ { id, severity: "error"\|"warning", title, detail, count, samples: string[], kind, status } ] }`. `kind` and `status` (when set) open the bundle list the check is about. |
| `GET /assets/targets` | `{ items: [ AssetTarget ] }` |
| `POST /assets/targets` | Body `AssetTargetInput`; answers the `AssetTarget`. |
| `PUT /assets/targets/{id}` | Body `AssetTargetInput`; `password` null keeps it, `""` clears it. |
| `DELETE /assets/targets/{id}` | 204, with what it recorded and its history. |
| `POST /assets/targets/{id}/test` | `{ ok, message }`: connected and listed the folder, or why not. |
| `POST /assets/targets/{id}/publish` | Body `{ dryRun, deleteRemoved }`; answers the `AssetJob`. |
| `POST /assets/targets/{id}/forget-host-key` | 204: the next SFTP connection trusts the key it is shown. |
| `GET /assets/targets/{id}/history` | `{ items: [ { id, startedAt, finishedAt, playerId, playerName, dryRun, uploaded, skipped, deleted, bytes, error } ] }`, newest first. |

`AssetBundle`: `{ kind, name, revision, source: "habbo"|"upload", hash, size, ids: number[], error,
updatedAt, used }`. `used` is whether the hotel names it: a furniture definition for furniture, a
breed's type for a pet; an effect is always used.

`AssetTarget`: `{ id, name, protocol, host, port, user, hasPassword, remotePath, publicUrl,
allowSelfSigned, hostKey, pending, lastPublish }`. `pending` is how many bundles it lacks or holds an
older copy of; `lastPublish` is the newest history entry, or null.

`AssetTargetInput`: `{ name, protocol, host, port, user, password, remotePath, publicUrl, allowSelfSigned }`.

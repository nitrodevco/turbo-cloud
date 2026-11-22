# Host Feature Rules

- [Hand items and vending](#hand-items-and-vending)
- [Assets](#assets)
- [Themes](#themes)
- [AI continuations](#ai-continuations)

Rules for host-side features an operator drives from the admin panel: hand items and vending
configuration, asset discovery and jobs, themes, and AI continuations.
[`CONTEXT.md`](../../CONTEXT.md) says which type owns each of them.

## Hand items and vending

- Hand-item catalogues read locally stored client actions, figure assets and imported texts.
  Keep server hand-item IDs distinct from sprite-part IDs. Vending edits preserve unrelated
  extra-data sections and custom IDs, and use the definition history rather than direct writes.
- Furniture import applies known vending defaults only while creating floor definitions assigned
  the vending logic. Preserve existing server fields and keep historical migration seed data frozen.
- Vending repairs require an explicit versioned preview selection. Revalidate after beginning the
  selected-row transaction, preserve custom JSON and saved access unless explicitly accepted,
  and commit the entire selected batch with its history. Import defaults never rewrite existing
  definitions implicitly.

## Assets

- Asset discovery captures client releases per run. Preview, sync and background discovery share
  the catalog's coordination and publish only completed, uncancelled results. Shutdown drains
  queued callers as well as active reads before disposing the catalog's resources.
- Asset jobs admit and retain their task under the same lock as shutdown. Stop rejects new jobs,
  cancels the active job and awaits its final metadata flush while its dependencies remain alive.

## Themes

- Theme session initialization consumes variables and revision from one `IThemeFileService.ThemeViewAsync`
  result. Keep one materialization cache for session and live views; preserve default null projections,
  text URL placement and bounded revision retention. Do not pair independent variables/look reads.
- Theme creation and package installation (staging, the key reservation, publishing under the
  reload gate, rollback) are a boundary of the plugin manager: [`CONTEXT.md`](../../CONTEXT.md),
  *Hard boundaries*.

## AI continuations

- **AI continuations report queue rejection and close interrupted tool history.** An approval
  must not report success when its continuation was not queued. Cancellation after a tool may
  have changed state must persist an error result for every unanswered call, without replaying
  the tool. Updates to one provider's key merge the authoritative settings map in a database
  transaction; never replace the map from a silo's cached options.

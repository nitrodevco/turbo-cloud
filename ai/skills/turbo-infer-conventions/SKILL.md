---
name: turbo-infer-conventions
description: Sweep this Turbo plugin's own code for the conventions it already follows (folder layout, naming, message and permission node names, settings and texts, tables, test style, client module layout), show each with the files that prove it, and once the author approves, record them as project rules with npm run ai:rule. Use when asked to learn, infer or write down this plugin's conventions, or before a large change in a plugin you have not worked in.
---

# Infer a plugin's conventions

## When to use this skill

A plugin that has grown has its own way of doing what the kit leaves open: where a kind of class
goes, how its messages and nodes are named, how settings are read, how tests are written. An
assistant that does not know them writes correct code that does not look like the rest. This skill
finds those conventions in the code, shows the evidence, and records the ones the author approves
as project rules (`.ai/rules/*.md`), which `npm run ai` indexes in every agent's instructions.

Read only; change no code to fit a convention.

## 1. Read what is recorded

Read `.ai/rules/*.md` and the `<turbo-boost>` block's "Project rules". A convention already there
is not proposed again; one the code no longer follows is proposed as an edit, with the evidence.

## 2. Sweep

Search the plugin (`server/`, `client/src/`, `tests/`, `gamedata/`, `manifest.json`) area by area.
For each, count the places that follow a pattern and note those that do not:

| Area | Look at |
| --- | --- |
| Layout | the folders under `server/` and the kind of class in each; whether `npm run make`'s folders (`Commands/`, `Messages/`, `Logic/`, `Policies/`, `Tasks/`...) are used or others; `client/src/` folders; one public type per file, file named after it |
| Naming | class suffixes (`...Command`, `...Handler`, `...Policy`, `...Tool`, `...Entity`), namespaces (the assembly's, or by folder), constants (`UPPER_SNAKE` or not), records or classes for messages |
| Messages | names after the key (`<key>.snake_case` or `<key>.camelCase`), the record's name, which side answers, one file per message or grouped |
| Permission nodes | `<key>.command.<name>`, `<key>.<area>.<action>` or `plugin.<key>.<name>`; where they are declared; which are `GrantedByDefault` |
| Settings and texts | one `[PluginSettings]` class or several, how code reads them, where defaults live; text keys (`<key>.<area>.<name>`), defaults in code (`DefaultTexts`) or only in `gamedata/external_texts.txt` |
| Database | the context's name, entity suffix, table names after `TablePrefix`, migration names, where queries live (a store class, never room code) |
| Logging and errors | `[LoggerMessage]` or `logger.Log...`, how a refused action answers the player |
| Tests | `TestHotel` or `FakeHotel`, one test class per class, method names (a sentence with `_`), assertions (`Should()` or `Assert`), shared helpers, what a test names (the player's view or the class's) |
| Client module | windows and their files, state (a store per window or one), texts through `api.texts.get` with a default, how messages are listened to |

Leave out what the kit's guideline already requires of every plugin (sources only, the key in
names, `IHotel` from the constructor): record only what is this plugin's choice.

A pattern is a convention when **at least three places** follow it and **almost none** do
otherwise. Fewer, or split: not a convention; say so if the author asked about that area.

## 3. Show it, and wait for approval

One table, one row per convention:

| # | Rule (one sentence: what to do) | Evidence | Exceptions |
| --- | --- | --- | --- |
| 1 | Name messages `<key>.snake_case`, the record `<Name>Message` in `server/Messages/<Name>Message.cs` | 9 of 9: `server/Messages/LevelledMessage.cs:7`, `server/Messages/TrainMessage.cs:6`, ... | none |
| 2 | Read settings through the injected `SkillsSettings`, never `IConfiguration` | 6 of 7: `server/SkillBook.cs:14`, ... | `server/Tasks/WeeklyReset.cs:22` reads the raw section |

Evidence is a count and two or three `file:line`, read from the files (never guessed). Then ask
which to record, and whether to reword any. Record nothing before the author answers; an
exception is theirs to keep or fix.

## 4. Record the approved ones

One command per approved rule; it writes `.ai/rules/<title>.md` and refreshes the index in every
agent's instructions file:

```sh
npm run ai:rule -- "Messages are <key>.snake_case" "Name a message <key>.snake_case and its record <Name>Message, one file each in server/Messages: every message here does, and the client's listeners are found by those names." --seen "server/Messages/LevelledMessage.cs, server/Messages/TrainMessage.cs"
```

The title is the rule in a few words; the text says what to do and why it is so here; `--seen`
names where the code does it. To change a rule later, edit its file and run `npm run ai:update`.

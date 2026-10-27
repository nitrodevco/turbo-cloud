# Errors & Logging

- [Introduction](#introduction)
- [Logging](#logging)
    - [Writing Log Entries](#writing-log-entries)
    - [Log Levels](#log-levels)
    - [A Plugin's Log Category](#a-plugins-log-category)
- [What Happens When Code Throws](#what-happens-when-code-throws)
    - [Handlers and Event Handlers](#handlers-and-event-handlers)
    - [Behaviours and Policies](#behaviours-and-policies)
    - [Commands](#commands)
    - [Furni Logic Messages](#furni-logic-messages)
- [When a Plugin Does Not Load](#when-a-plugin-does-not-load)
- [Telling the Player](#telling-the-player)

## Introduction

Turbo logs through .NET's `ILogger`, to the console by default and to
[OpenTelemetry](telemetry.md) when you turn it on. Most of the time you don't catch exceptions at
all: the code that calls yours catches, logs and carries on, in a way chosen for each kind of hook.
This page says what each one does, so you know what a player sees when your code fails.

## Logging

### Writing Log Entries

Take `ILogger<T>` by constructor, in core or in a plugin:

```csharp
public sealed class SkillBook(ILogger<SkillBook> logger)
{
    public void Reset(PlayerId player)
    {
        logger.LogInformation("Reset the skill of {Player}", player);
    }
}
```

Use message templates (`{Player}`), not interpolated strings, so the value is kept as a field for
whoever reads the logs.

### Log Levels

`Logging:LogLevel` in the configuration sets the lowest level written, per category. The shipped
`appsettings.json` writes Turbo's own categories at `Information` and the noisy libraries at
`Warning`; the `appsettings.Development.json` that bootstrap creates writes Turbo's at `Debug`. Every packet in
and out is logged at `Debug` too (`Incoming ...`, `Outgoing ...`), but its two categories,
`Turbo.Networking.Package.PackageHandler` and `PackageEncoder`, stay at `Warning` until you lower
them.

On a live hotel, turn on debug lines while you look into something:

```shell
TURBO__Logging__LogLevel__Turbo=Debug
```

### A Plugin's Log Category

A plugin's loggers are named after the plugin: a `SkillBook` in the `Skills` plugin logs as
`Skills:Skills.SkillBook`. So the plugin's name is on every line it writes, and one setting sets the
level for the whole plugin:

```json
{
    "Logging": { "LogLevel": { "Skills": "Debug" } }
}
```

Plugins are compiled with their sources in their debug symbols, so a stack trace from a plugin
names the plugin's own file and line.

## What Happens When Code Throws

### Handlers and Event Handlers

A packet handler, a plugin message handler or an `IEventHandler<T>` that throws is logged once, with
the packet or event and the session, and the other handlers of the same message still run. Don't
wrap a handler's body in `try`/`catch` to log it: the pipeline already did. Catch only an exception
you turn into a reply the client expects.

### Behaviours and Policies

These decide whether something happens, so they fail closed where letting it through would be
wrong:

| Throws | Result |
| --- | --- |
| a behaviour, before it calls `next` | the message is dropped; nobody is answered |
| a purchase policy | the purchase is refused, nothing is charged, the buyer sees the hotel's purchase error |
| any other policy | logged and passed over: it allows |

See [Policies & Events](plugin-policies.md#order-and-conflicts).

### Commands

A chat command that throws is caught, logged and answered with a whisper, so the player knows it
did not work. Return `CommandResult.Fail("status")` for a failure you expect (a target that
doesn't exist), with its words in the hotel's texts. See [Commands](commands.md).

### Furni Logic Messages

`hotel.Rooms.InvokeFurniLogicAsync` never throws for the logic's failure: its reply's status is
`Failed`, and the room logs the exception. See [Furni Logic](furni-logic.md#messages-from-your-own-code).

## When a Plugin Does Not Load

A plugin that does not compile, or throws as it starts, does not stop the server. It is marked
**Failed** (or **Rejected**, for code a plugin may not run) with the reason, which the admin panel's
Plugins page shows and the log has with its file and line:

```
server/SkillsCommand.cs(42,9): CS0103 The name 'y' does not exist in the current context
```

`npm run check` in the plugin's folder shows the same errors before you install it.
[Plugin Lifecycle](plugin-loading.md#when-a-plugin-does-not-load) lists each failure and what to
do about it.

## Telling the Player

A log line is for you. For the player, answer in the client's own terms: a command's result text,
an alert (`hotel.Notify.AlertAsync`), a policy's refusal reason (shown in a pop-up), or a reply
packet the client already understands. Their words belong in the hotel's texts, so a hotel can
reword and translate them.

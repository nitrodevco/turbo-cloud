# Plugin notices

A plugin can tell other plugins that something happened (a new client error, a bug report, a
room copy finished) without depending on them, through `PluginNoticeEvent`
(`Turbo.Primitives/Plugins/Events`) on the hotel's event system. A relay plugin (a Discord
webhook, a log, a mail) handles it; a plugin that publishes one does not know or care whether a
relay is loaded.

Plugins cannot share their own types: each loads in its own load context, so a type one plugin
declares is another type in the plugin that reads it. The notice is the host's type, which every
plugin sees as the same one, and it holds only texts and named values.

## Publishing

Take `EventSystem` (`Turbo.Events`) from the host services and publish. The plugin references
`Turbo.Events` from the turbo-cloud checkout it builds against, as it does `Turbo.Primitives`.

```csharp
private EventSystem? _events;

public Task StartAsync(IServiceProvider services, CancellationToken ct)
{
    _events = services.GetRequiredService<IHostServices>().GetRequiredService<EventSystem>();

    return Task.CompletedTask;
}

// Later, when an issue is new:
await _events.PublishAsync(
    new PluginNoticeEvent
    {
        Source = "clienterrors",           // the plugin's key
        Topic = "issue.new",               // stable: relays route by it
        Title = $"New client error #{issue.Id}",
        Text = issue.Message,
        Url = $"{panel}/plugin/clienterrors/issue/{issue.Id}",
        Fields = [new("Build", report.Build), new("Browser", browser)],
        Level = PluginNoticeLevel.Error,
        Key = issue.Id.ToString(),         // one notice per issue, even if raised twice
    },
    ct
);
```

Publishing waits for the handlers, so a relay hands the notice to its own queue and returns; a
relay that throws is logged and does not fail the publisher. Publish after the change is saved,
and keep the texts to what a hotel's staff may read: a relay passes them on as they are.

| Field | |
| --- | --- |
| `Source` | The publishing plugin's manifest key. |
| `Topic` | What happened, a stable code of the source's own (at most 64 characters). A relay lists each `Source` and `Topic` it hears, so the hotel can route or mute each. |
| `Title`, `Text` | One line (256), and more in plain text (4000). |
| `Url` | Where to read more. |
| `Fields` | Named values in order, at most 20. |
| `Level` | `Info`, `Success`, `Warning` or `Error`: a colour or an icon. |
| `Key` | The thing the notice is about. A relay passes on one notice per `Source`, `Topic` and `Key`; null makes every notice its own. |

A relay cuts what is longer than the limits rather than refuse it.

## Relaying

A relay declares a handler; the plugin's assembly scan registers it when the plugin loads and
removes it when it unloads, like any event handler:

```csharp
public sealed class NoticeRelay(MyQueue queue) : IEventHandler<PluginNoticeEvent>
{
    public ValueTask HandleAsync(PluginNoticeEvent notice, EventContext ctx, CancellationToken ct)
    {
        queue.TryAdd(notice); // never a network call here: the publisher is waiting

        return ValueTask.CompletedTask;
    }
}
```

---
name: turbo-client-modules
description: Write a Turbo plugin's client module (client/src/index.tsx, TypeScript + React on @nitrodevco/client-module-sdk) - windows, toolbar and me menu entries, links, context menus, client chat commands, events and state, template patches on core windows, texts, assets, web calls, and secondary rooms fed with carried core packets. Use when a plugin needs anything on the player's screen.
---

# Client modules

## When to use this skill

Anything the player sees or clicks: a window, an icon, a button inside a core window, a drawing
over avatars, another room drawn beside the current one.

## How it is built and run

- The entry is `client/src/index.tsx` (or `index.ts`); the **hotel** builds it with esbuild into one
  file `<key>.js` as the plugin loads, and clients run it beside the client's own code. Never ship
  built JavaScript.
- It imports only its own files, `@nitrodevco/client-module-sdk` (and `/theme`), `react`, `zustand`:
  the last three are the client's own at run time. No other npm package.
- A module that does not build fails the plugin with each error's `file:line:column`
  (`plugin_errors` on the dev hotel shows them). `npm run typecheck` checks types locally.
- Files it loads (icons, data) are in `wwwroot/`, by `assetUrl('icon.png')`.
- Whatever it adds through the API is taken out when it stops; `deactivate` only undoes what it did
  outside the API. Everything it draws sits in its own error boundary.

## A module

```tsx
import { assetUrl, defineModule, useApi, type ClientModuleWindowProps } from '@nitrodevco/client-module-sdk';
import { Button, Frame, ThemeText } from '@nitrodevco/client-module-sdk/theme';
import { useState } from 'react';

const Counter = ({ close }: ClientModuleWindowProps) => {
    const api = useApi();
    const [ clicks, setClicks ] = useState(0);

    return (
        <Frame id={`${api.key}-counter`} variant="3" caption={api.texts.get('demo.counter.title', 'Counter')} centered onClose={close} layout={{ width: 200, height: 100 }}>
            <ThemeText text={String(clicks)} textStyle="u_regular" />
            <Button variant="101" onPointerTap={() => setClicks(clicks + 1)}>+1</Button>
        </Frame>
    );
};

export default defineModule({
    activate(api) {
        api.windows.register('counter', Counter);
        api.toolbar.add({ id: 'counter', icon: assetUrl('counter.png'), tooltip: api.texts.get('demo.toolbar', 'Counter'), onClick: () => api.windows.toggle('counter') });
        api.links.handle('demo', (link) => { if (link === 'demo/counter') api.windows.open('counter'); });
        api.chatCommands.register({ name: 'counter', description: 'Open the counter', run: () => api.windows.open('counter') });
        api.contextMenu.add({ id: 'counter', menu: 'ownUser', label: 'Counter', onClick: () => api.windows.open('counter') });
    },
});
```

`npm run make window news` adds a window opened by the link `<key>/news`.

## The API (`api.*`)

| Part | Use |
| --- | --- |
| `state.get/subscribe/use(selector)`, `state.hasPermission(node)` | the own user, club, currencies, the room they are in; read-only. Select a part or value, never build a new object in `use` |
| `events.on(name, fn)` | `room.entered`/`room.left`, `room.avatar.added/removed/clicked`, `room.furni.added/removed/clicked`, `room.chat` |
| `windows.register(id, Component, { onTop? })`, `open/close/toggle(id, params?)` | windows; the component draws its own `Frame` or `TemplateWindow` |
| `toolbar.add`, `meMenu.add`, `contextMenu.add({ menu: 'user'/'ownUser'/'furni' })` | entries in the client's bars and menus |
| `links.handle(prefix, fn, { path? })`, `links.open(link)` | in-client links; use the plugin's key as prefix |
| `chatCommands.register({ name, parameters?, run })` | `:name` handled in the client (return `false` to send the line on to the server) |
| `messages.on/send(name, data)` | the plugin's named messages (turbo-plugin-messages skill) |
| `http.get/post/put/patch/delete(path, body?)` | the plugin's own web routes, signed in as the player; resolves `{ ok, status, data, error }` |
| `texts.get(key, fallback, replacements?)`, `config.get(key)` | the hotel's texts and variables (`gamedata/external_texts.txt`) |
| `templates.patch(id, ops)`, `templates.bind(id, element, binding)` | change a core window as data (below) |
| `channels.publish/subscribe`, `defineChannel<T>(key)` | share data between modules |
| `room.avatarOverlay(render)`, `room.onUnhandledPointer(fn)`, `room.createSecondary(...)` | draw over avatars; clicks the room did not use; another room |
| `hotelView.addLayer/addWidget`, `notifications.add`, `sounds.play` | reception, notification bubbles, sounds |
| `packets.*`, `capabilities.*` | hand-numbered extension packets (prefer named messages) |

## Changing a core window: template patches

Windows are Habbo's own layouts; elements are addressed by the names the window's code uses
(`habbo-toolbar-com/purse_xml`, element `help_button`). Never ship a copy of a library.

```ts
const PURSE = 'habbo-toolbar-com/purse_xml';

api.templates.patch(PURSE, [
    { element: 'settings_button', insert: 'after', add: { like: 'help_button', name: 'demo', y: 63, children: [ { like: 'help_button/@0', name: 'label', caption: api.texts.get('demo.purse.button', 'Demo') } ] } },
    { element: '@8', set: { height: 62 + 21 } },
]);
api.templates.bind(PURSE, 'demo', { onPointerTap: () => api.windows.toggle('counter') });
```

Ops: `set` (colour, style, caption, visible, rect...), `remove`, `insert` (before/after/inside),
`move`, `bind`, `arrange`. A plugin with no code ships them as `wwwroot/template-patches.json`
(`npm run make patch <library>/<asset>`). Conflicts and missed targets are reported (alt-click: the
extension inspector). Client guide: nitro-next `docs/template-patches.md`.

## Secondary rooms (another room drawn beside this one)

Fed by a room watch's envelope (turbo-room-holds-watches skill), whose carried field the client
reads with its own parser:

<!-- typed with: declare const roomB: number; -->

```ts
const portal = api.room.createSecondary(roomB, { offset: { x: 12, y: 0, z: 0 }, greyscale: 1, alpha: 0.75 });

api.messages.on('demo.far_room', ({ roomId, carried }) => {
    if (roomId === roomB) portal?.apply(carried);
});
api.room.onUnhandledPointer(({ type, secondaryTile }) => {
    if ((type !== 'click') || (secondaryTile?.roomId !== roomB)) return;

    api.messages.send('demo.walk', secondaryTile);

    return true; // taken: the room does not deselect
});
```

- `apply` takes floor/height maps, the entry tile, papers, floor and wall items and their moves,
  avatars and their moves; anything else (chat, settings) answers `false` with a warning.
- When the session walks into a drawn room, the client takes it as it is (no rebuild, the camera
  eases on). `keepWhenLeft(options)` keeps the room left drawn; `dispose()` removes it. A module's
  rooms go when it stops.
- `api.packets.parseCore(header, body)` reads a core packet a plugin's own packet carries; nothing
  but `apply` hears it.

## Rules

- Texts go in `gamedata/external_texts.txt` (`<key>.*`) and are read with `api.texts.get` with a fallback.
- Keep logic (parsing a message, maths) in plain `.ts` files tested with `node --test` in
  `client/tests`; components stay thin.
- Never reach into client internals or stores: only `api`. `api.version` is 1.

Guides: turbo-cloud `client-modules.md`; nitro-next `docs/client-modules.md` (the full API),
`docs/template-patches.md`.

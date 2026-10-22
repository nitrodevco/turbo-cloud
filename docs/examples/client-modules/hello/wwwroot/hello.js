/**
 * The "hello" client module: the smallest module that touches each part of the client module API
 * v1 (docs/client-modules.md). It needs no build step - it is plain JavaScript and takes React and
 * the theme from `api.libs` instead of importing them.
 *
 * - a toolbar icon (hello.png, beside this file) that opens and closes a window;
 * - the window: the theme's `Frame` with a line of text and the number of clicks so far;
 * - a me menu entry and the `hello/open` link, which open the same window;
 * - a small bar over the user's own avatar in a room.
 *
 * Texts come from the hotel's texts with English fallbacks (`hello.module.*`).
 */
let api;

const HelloWindow = ({ close }) => {
    const { createElement: h, useState } = api.libs.react;
    const { Box, Button, Frame, ThemeText } = api.libs.theme;
    const [ clicks, setClicks ] = useState(0);

    return h(Frame, {
        id: `${api.key}-hello`,
        variant: '3',
        caption: api.texts.get('hello.module.title', 'Hello'),
        centered: true,
        resizeDirection: 'none',
        onClose: close,
        layout: { width: 260, height: 150 },
    }, h(Box, { layout: { flexDirection: 'column', gap: 8, padding: 10, width: 258 } },
        h(ThemeText, { text: api.texts.get('hello.module.text', 'This window comes from a client module.'), textStyle: 'u_regular' }),
        h(ThemeText, { text: api.texts.get('hello.module.clicks', 'Clicks: %count%', { count: String(clicks) }), textStyle: 'u_regular' }),
        h(Button, { variant: '101', onPointerTap: () => setClicks(clicks + 1), layout: { width: 100, height: 22 } }, api.texts.get('hello.module.button', 'Click me')),
    ));
};

/** A 40 x 6 bar: dark track, green fill. */
const drawBar = (graphics) => {
    graphics.clear();
    graphics.rect(0, 0, 40, 6).fill(0x000000);
    graphics.rect(1, 1, 26, 4).fill(0x3ec43e);
};

const OwnAvatarBar = () => api.libs.react.createElement(api.libs.theme.Box, { layout: { width: 40, height: 6 } },
    api.libs.react.createElement('pixiGraphics', { draw: drawBar, layout: false }));

export default {
    activate(moduleApi) {
        api = moduleApi;

        api.windows.register('hello', HelloWindow);
        api.toolbar.add({
            id: 'hello',
            icon: new URL('hello.png', api.baseUrl).href,
            tooltip: api.texts.get('hello.module.title', 'Hello'),
            onClick: () => api.windows.toggle('hello'),
        });
        api.meMenu.add({ id: 'hello', caption: api.texts.get('hello.module.title', 'Hello'), onClick: () => api.windows.open('hello') });
        api.links.handle('hello', (link) => {
            if (link === 'hello/open') api.windows.open('hello');
        });
        api.room.avatarOverlay(avatar => (avatar.isOwnUser ? api.libs.react.createElement(OwnAvatarBar) : null));
    },

    // Everything added above is taken out by the client when the module stops; nothing else to undo.
    deactivate() { },
};

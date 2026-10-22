// The example client module (client.modules api v1): a toolbar icon that opens a small window,
// and a bar drawn above the player's own avatar. Its words and the bar's colour are the hotel's
// texts and variables the plugin brings in gamedata/ (docs/client-modules.md).

let cleanups = [];

export function activate(api) {
    const title = api.texts.get('hello.window.title');
    const body = api.texts.get('hello.window.body');
    const color = api.config.get('hello.bar.color') ?? '#33cc99';
    const width = Number(api.config.get('hello.bar.width') ?? 40);

    cleanups.push(
        api.windows.register('hello', () => ({ title, body })),
        api.toolbar.add({
            id: 'hello',
            tooltip: api.texts.get('hello.toolbar.tooltip'),
            onClick: () => api.windows.open('hello'),
        }),
        api.room.avatarOverlay(({ own }) => (own ? { width, height: 4, color } : null))
    );
}

export function deactivate() {
    for (const cleanup of cleanups) cleanup?.();

    cleanups = [];
}

export default { activate, deactivate };

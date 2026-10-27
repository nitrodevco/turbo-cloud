/**
 * The "roomwindow" client module: draws the room behind each window of the room the player is in,
 * greyed, beside the room on screen, and walks the player into it when they click its floor
 * (docs/examples/room-window/README.md). The server builds it from this file; it imports nothing.
 *
 * - `roomwindow.look` (sent on entering a room, then every 30 s to renew the watches) asks the
 *   plugin for the room's windows; `roomwindow.views` answers where each far room is drawn.
 * - `api.room.createSecondary` draws a far room once its first packet comes: each `roomwindow.room`
 *   envelope carries one, read by the client's own parser, which `apply` runs through the client's
 *   room handlers.
 * - `api.room.onUnhandledPointer` gives the far room's floor tile under a click (`secondaryTile`):
 *   `roomwindow.cross` asks the window to walk the player there. As they go through, the client
 *   takes the drawn room as the room it enters (no reload), and `api.room.keepWhenLeft` keeps the
 *   room they left drawn behind them, fed again once that room's window answers its own look.
 */
let api;
let renew;
/**
 * The rooms behind the windows, by room id: the view (which window, where it is drawn) and, once the
 * room's first packet came, its handle. A player the watch refuses (banned from that room) gets the
 * view and no packet, so they are shown nothing.
 */
const far = new Map();
/** The room the player left through a window, kept drawn until the new room's windows say where. */
let kept;

const STYLE = { greyscale: 1, alpha: 0.8 };
const RENEW_MS = 30000;

const look = () => api.messages.send('roomwindow.look', {});

const offsetOf = view => ({ x: view.offsetX, y: view.offsetY, z: 0 });

/** The room's windows: keep (and move) the rooms they show, drop the rest. */
const show = ({ windows }) => {
    const wanted = new Set(windows.map(view => view.roomId));

    for (const [ roomId, entry ] of far) {
        if (wanted.has(roomId) && (entry.handle?.status ?? 'drawn') === 'drawn') continue;

        // A room the player walked into is theirs now: dispose leaves it as it is.
        entry.handle?.dispose();
        far.delete(roomId);
    }

    for (const view of windows) {
        const entry = far.get(view.roomId);

        if (entry) {
            entry.view = view;
            entry.handle?.setOptions({ offset: offsetOf(view) });
        } else if ((kept?.roomId === view.roomId) && (kept.status === 'drawn')) {
            // The room just left, still drawn: a window here leads back to it.
            kept.setOptions({ offset: offsetOf(view) });
            far.set(view.roomId, { view, handle: kept });
            kept = undefined;
        } else {
            far.set(view.roomId, { view, handle: undefined });
        }
    }

    // Kept and left, but no window here leads back to it. (Still `waiting`: the player has not
    // gone through yet.)
    if (kept?.status === 'drawn') {
        kept.dispose();
        kept = undefined;
    }
};

/** One of a far room's own packets, carried in the watch's envelope: drawn from its first. */
const feed = ({ roomId, carried }) => {
    const entry = far.get(roomId);

    if (!entry) return;

    entry.handle ??= api.room.createSecondary(roomId, { offset: offsetOf(entry.view), ...STYLE });
    entry.handle?.apply(carried);
};

const cross = ({ type, secondaryTile }) => {
    const entry = secondaryTile && far.get(secondaryTile.roomId);

    if (entry?.handle?.status !== 'drawn') return false;
    if (type !== 'click') return false;

    api.messages.send('roomwindow.cross', { windowId: entry.view.windowId, x: secondaryTile.x, y: secondaryTile.y });

    // Keep this room drawn once the player is through, where it lies seen from the far room.
    kept?.dispose();
    kept = api.room.keepWhenLeft({ offset: { x: -entry.view.offsetX, y: -entry.view.offsetY, z: 0 }, ...STYLE });

    // Taken: a click on the far room does not deselect anything in this one.
    return true;
};

export default {
    activate(moduleApi) {
        api = moduleApi;

        api.messages.on('roomwindow.views', show);
        api.messages.on('roomwindow.room', feed);
        api.room.onUnhandledPointer(cross);
        api.events.on('room.entered', () => {
            clearInterval(renew);
            look();
            renew = setInterval(look, RENEW_MS);
        });
        api.events.on('room.left', () => clearInterval(renew));

        // Started (or reloaded) while in a room.
        if (api.state.get().room) {
            look();
            renew = setInterval(look, RENEW_MS);
        }
    },

    // The client takes the module's rooms, listeners and handlers away; only the timer is ours.
    deactivate() {
        clearInterval(renew);
    },
};

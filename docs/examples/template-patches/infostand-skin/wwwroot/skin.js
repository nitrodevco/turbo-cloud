/**
 * The "infostandskin" client module: skins the user infostand (`habbo-room-ui-com/user_view`) by the
 * user it shows, without a copy of its library (nitro-next `docs/template-patches.md`).
 *
 * - The window's context (`type`, `userId`, `figure`, `isOwnUser`, ...): the patch and the name's
 *   binding are functions of it, made once per user shown, not per render.
 * - `move`: the motto and its spacer go up, under the name, keeping their names - the infostand's own
 *   code still fills and sizes the motto.
 * - `set.params`: the avatar no longer re-centres in its box (`scale` fixed both ways).
 * - `bind(..., { override: true })`: the name's colour, which the infostand's code sets every render;
 *   reported as a conflict with the window's code (`api.templates.report`), as intended.
 * - `api.templates.arrange`: after the infostand's own layout code (which makes the border its list's
 *   height plus 20), the border is made 12 pixels taller, and the window with it.
 *
 * Everything is taken out when the module stops; the infostand is drawn as before.
 */
const USER_VIEW = 'habbo-room-ui-com/user_view';

/** `0xAARRGGBB`: the border's tint for the own user and for everyone else. */
const OWN_BORDER = 0xff2e7d32;
const OTHER_BORDER = 0xff1f4f7a;

/** The name's colour (`0xRRGGBB`, a text's): gold for the own user. */
const OWN_NAME = 0xffd54f;
const OTHER_NAME = 0xbfe3ff;

/** How much taller than its list the border is. */
const EXTRA_MARGIN = 12;

export default {
    activate(api) {
        api.templates.patch(USER_VIEW, context => [
            { element: 'info_border', set: { color: context.isOwnUser ? OWN_BORDER : OTHER_BORDER } },
            { element: 'motto_container', move: 'after', to: 'images_spacer' },
            { element: 'motto_spacer', move: 'after', to: 'motto_container' },
            { element: 'avatar_image', set: { params: { scale: [ 'fixed', 'fixed' ] } } },
        ]);

        api.templates.bind(USER_VIEW, 'name_text', context => ({ color: context.isOwnUser ? OWN_NAME : OTHER_NAME }), { override: true });

        api.templates.arrange(USER_VIEW, ({ find, root }) => {
            const border = find('info_border');
            const view = root();

            if (!border || !view) return;

            border.setHeight(border.height + EXTRA_MARGIN);
            view.setHeight(border.height);
        });
    },

    deactivate() { },
};

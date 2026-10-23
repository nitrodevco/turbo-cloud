/**
 * The "skills" client module: an add-on that extends a core window and adds one of its own.
 *
 * - `api.templates.patch`: a Skills button in the purse (`habbo-toolbar-com/purse_xml`), below its
 *   help, logout and settings buttons; the purse and that list grow by the button's height. It is
 *   named `skills:skills` in the template, so no other plugin's patch can reach it by name.
 * - `api.templates.bind`: the button's click, as the purse's own code binds its buttons.
 * - `api.windows.register`: the Skills window, which asks the plugin's server half for the player's
 *   level (header 20100), shows the answer (20101) and trains (20102).
 *
 * A theme that recolours the purse (the `pursetheme` example) and this add-on change the same window
 * without either shipping a copy of its library or knowing of the other.
 */
const PURSE = 'habbo-toolbar-com/purse_xml';
const GET_SKILL = 20100;
const SKILL = 20101;
const TRAIN = 20102;
/** The purse's button list, its 4th button's slot, and how much taller everything gets. */
const BUTTON_HEIGHT = 21;

let api;
let skill = { level: 1, experience: 0, needed: 100 };
const listeners = new Set();

const setSkill = (next) => {
    skill = next;
    listeners.forEach(listener => listener(next));
};

const SkillsWindow = ({ close }) => {
    const { createElement: h, useEffect, useState } = api.libs.react;
    const { Box, Button, Frame, ThemeText } = api.libs.theme;
    const [ shown, setShown ] = useState(skill);

    useEffect(() => {
        listeners.add(setShown);
        api.packets.send(GET_SKILL);

        return () => {
            listeners.delete(setShown);
        };
    }, []);

    return h(Frame, {
        id: `${api.key}-skills`,
        variant: '3',
        caption: api.texts.get('skills.window.title', 'Skills'),
        centered: true,
        resizeDirection: 'none',
        onClose: close,
        layout: { width: 240, height: 130 },
    }, h(Box, { layout: { flexDirection: 'column', gap: 8, padding: 10, width: 238 } },
        h(ThemeText, { text: api.texts.get('skills.window.level', 'Level %level%', { level: String(shown.level) }), textStyle: 'u_regular' }),
        h(ThemeText, { text: api.texts.get('skills.window.experience', '%experience% / %needed% experience', { experience: String(shown.experience), needed: String(shown.needed) }), textStyle: 'u_regular' }),
        h(Button, { variant: '101', onPointerTap: () => api.packets.send(TRAIN), layout: { width: 100, height: 22 } }, api.texts.get('skills.window.train', 'Train')),
    ));
};

export default {
    activate(moduleApi) {
        api = moduleApi;

        api.packets.registerParser(SKILL, wrapper => ({ level: wrapper.readInt(), experience: wrapper.readInt(), needed: wrapper.readInt() }), setSkill);
        api.packets.registerComposer(GET_SKILL);
        api.packets.registerComposer(TRAIN);
        api.windows.register('skills', SkillsWindow);

        // The purse's elements as its layout has them: '' is the purse, '@0' its outer border, '@8' the
        // list holding help/logout/settings (it has no name of its own).
        api.templates.patch(PURSE, [
            // A button like the purse's own help button (its look, its input flags, its centred label), purple, below settings.
            { element: 'settings_button', insert: 'after', add: { like: 'help_button', name: 'skills', y: 63, color: 0x8e44ad, caption: 'Skills', vars: { tool_tip_caption: 'Skills' }, children: [ { like: 'help_button/@0', name: 'label', caption: api.texts.get('skills.purse.button', 'Skills') } ] } },
            { element: '@8', set: { height: 62 + BUTTON_HEIGHT } },
            { element: '@0', set: { height: 76 + BUTTON_HEIGHT } },
            { element: '', set: { height: 77 + BUTTON_HEIGHT } },
        ]);
        api.templates.bind(PURSE, 'skills', { onPointerTap: () => api.windows.toggle('skills') });
    },

    // Everything added above - the patch, the binding, the window and the packets - is taken out by
    // the client when the module stops; the purse is drawn as before.
    deactivate() { },
};

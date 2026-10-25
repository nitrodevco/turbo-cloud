/**
 * The plugin's client module: built by `npm run dev` / `npm run build` into one file,
 * `wwwroot/skills.js`, which the server lists for every client that runs modules.
 *
 * - a window of its own (`SkillsWindow`), opened from a toolbar icon and the `skills/open` link;
 * - the plugin's messages, read and sent by name (`skill.ts`);
 * - a template patch: a Skills button in the purse, a core window, as data;
 * - API 1.1: a Skills button in the own avatar menu, the `:skills` chat command, the skill asked for
 *   again in each room entered (`api.events`), and the player's name read from `api.state` in the window.
 */
import { assetUrl, defineModule } from '@nitrodevco/client-module-sdk';

import { activateMade } from './made';
import { GET_SKILL, SKILL, toSkill } from './skill';
import { skillStore } from './skillStore';
import { SkillsWindow } from './SkillsWindow';

const PURSE = 'habbo-toolbar-com/purse_xml';

export default defineModule({
    activate(api) {
        api.messages.on(SKILL, data => skillStore.set(toSkill(data)));

        api.windows.register('skills', SkillsWindow);
        api.toolbar.add({
            id: 'skills',
            icon: assetUrl('skills.png'),
            tooltip: api.texts.get('skills.toolbar.tooltip', 'Skills'),
            onClick: () => api.windows.toggle('skills'),
        });
        // Every handler of a prefix gets each of its links: the windows `npm run make window` adds
        // handle theirs beside this one.
        api.links.handle('skills', (link) => {
            if (link === 'skills/open') api.windows.open('skills');
        });

        // A button in the purse like its help button, below its settings button; the purse and its
        // button list grow by the button's height. The names are the purse layout's own (`'@8'` is
        // the list, which has none). See nitro-next docs/template-patches.md. The button is the help
        // button's 50 pixels and the purse has no room for more: keep its caption to about seven letters.
        api.templates.patch(PURSE, [
            { element: 'settings_button', insert: 'after', add: { like: 'help_button', name: 'skills', y: 63, children: [ { like: 'help_button/@0', name: 'label', caption: api.texts.get('skills.purse.button', 'Skills') } ] } },
            { element: '@8', set: { height: 62 + 21 } },
            { element: '@0', set: { height: 76 + 21 } },
            { element: '', set: { height: 77 + 21 } },
        ]);
        api.templates.bind(PURSE, 'skills', { onPointerTap: () => api.windows.toggle('skills') });

        // API 1.1. A room's training may have moved the skill: asked for again as each room is entered.
        api.events.on('room.entered', () => api.messages.send(GET_SKILL, {}));
        // A button in the menu over the player's own avatar, after the client's rows.
        api.contextMenu.add({
            id: 'skills',
            menu: 'ownUser',
            label: api.texts.get('skills.menu.button', 'Skills'),
            icon: assetUrl('skills.png'),
            onClick: () => api.windows.open('skills'),
        });
        // `:skills` in the chat input opens the window; the input lists it with the other commands.
        api.chatCommands.register({
            name: 'skills',
            description: api.texts.get('skills.command', 'Open your skills'),
            run: () => api.windows.open('skills'),
        });

        // What `npm run make` added (windows, message listeners).
        activateMade(api);
    },
});

/**
 * The plugin's client module: built by `npm run dev` / `npm run build` into one file,
 * `wwwroot/skills.js`, which the server half registers and every client that runs modules loads.
 *
 * - a window of its own (`SkillsWindow`), opened from a toolbar icon and the `skills/open` link;
 * - the plugin's packets, read and sent by number (`skill.ts`);
 * - a template patch: a Skills button in the purse, a core window, as data.
 */
import { assetUrl, defineModule } from '@nitrodevco/client-module-sdk';

import { GET_SKILL, readSkill, SKILL, TRAIN } from './skill';
import { skillStore } from './skillStore';
import { SkillsWindow } from './SkillsWindow';

const PURSE = 'habbo-toolbar-com/purse_xml';

export default defineModule({
    activate(api) {
        api.packets.registerParser(SKILL, readSkill, skill => skillStore.set(skill));
        api.packets.registerComposer(GET_SKILL);
        api.packets.registerComposer(TRAIN);

        api.windows.register('skills', SkillsWindow);
        api.toolbar.add({
            id: 'skills',
            icon: assetUrl('skills.png'),
            tooltip: api.texts.get('skills.toolbar.tooltip', 'Skills'),
            onClick: () => api.windows.toggle('skills'),
        });
        api.links.handle('skills', (link) => {
            if (link === 'skills/open') api.windows.open('skills');
        });

        // A button in the purse like its help button, below its settings button; the purse and its
        // button list grow by the button's height. The names are the purse layout's own (`'@8'` is
        // the list, which has none). See nitro-next docs/template-patches.md.
        api.templates.patch(PURSE, [
            { element: 'settings_button', insert: 'after', add: { like: 'help_button', name: 'skills', y: 63, children: [ { like: 'help_button/@0', name: 'label', caption: api.texts.get('skills.purse.button', 'Skills') } ] } },
            { element: '@8', set: { height: 62 + 21 } },
            { element: '@0', set: { height: 76 + 21 } },
            { element: '', set: { height: 77 + 21 } },
        ]);
        api.templates.bind(PURSE, 'skills', { onPointerTap: () => api.windows.toggle('skills') });
    },
});

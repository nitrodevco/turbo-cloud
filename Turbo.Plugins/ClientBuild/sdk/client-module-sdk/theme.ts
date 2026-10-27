// nitro-next packages/client-module-sdk/src/theme.ts, as the server bundles plugins' client modules with it.
/**
 * `import { Frame, Button } from '@nitrodevco/client-module-sdk/theme'`: the client's own theme
 * components, taken from the host when the module is loaded - never a bundled copy.
 */
import { host } from './host';
import type { ClientModuleTheme } from './theme-types';

export type * from './theme-types';

export const { Box, Button, Frame, Region, RichText, ThemeText, TemplateWindow }: ClientModuleTheme = host().theme;

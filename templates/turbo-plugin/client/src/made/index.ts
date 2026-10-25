/**
 * What `npm run make` added to the client module, activated with it (client/src/index.tsx).
 * Rewritten by tools/make.mjs: every file here with an `activate` is listed.
 */
import type { ClientModuleApi } from '@nitrodevco/client-module-sdk';

const made: ((api: ClientModuleApi) => void)[] = [];

export const activateMade = (api: ClientModuleApi) => made.forEach(activate => activate(api));

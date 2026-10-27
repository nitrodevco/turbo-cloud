// nitro-next packages/client-module-sdk/src/host.ts, as the server bundles plugins' client modules with it.
import type { ClientModuleLibraries } from './api';

declare global {
    var NitroClientModuleHost: ClientModuleLibraries | undefined;
}

/**
 * The client's shared libraries. The client sets them on `globalThis` before it imports the first
 * module, so a module reads them at its top level; anywhere else (a unit test, a page without the
 * client) there are none.
 */
export const host = (): ClientModuleLibraries => {
    const libraries = globalThis.NitroClientModuleHost;

    if (!libraries) throw new Error('No Nitro client here: a client module runs inside the client, which provides React and the theme (globalThis.NitroClientModuleHost).');

    return libraries;
};

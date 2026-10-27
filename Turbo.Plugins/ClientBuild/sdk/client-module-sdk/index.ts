// nitro-next packages/client-module-sdk/src/index.ts, as the server bundles plugins' client modules with it.
/**
 * `@nitrodevco/client-module-sdk`: what a client module is written against. The API's types, and
 *
 * - `defineModule({ activate, deactivate })`: the module's default export, typed;
 * - `useApi()`: the module's API from any component it draws;
 * - `assetUrl(path)`: a file beside the module in its plugin's `wwwroot/`;
 * - `defineChannel<T>(key)` / `useChannel(channel)`: data shared with other modules, typed.
 *
 * React comes from `react` and the theme from `@nitrodevco/client-module-sdk/theme`; the bundler
 * preset (`@nitrodevco/client-module-sdk/build`) makes both the client's own.
 */
import type { ClientModuleApi, ClientModuleDispose } from './api';
import { host } from './host';

export type * from './types';

export interface ClientModuleDefinition {
    /** Called once the client has the module: add windows, icons, patches, packets here. */
    activate(api: ClientModuleApi): void | Promise<void>;
    /**
     * Called when the module stops (the plugin unloads or reloads, or the client leaves). What was
     * added through the API is taken out by the client; undo here only what the module did besides.
     */
    deactivate?(): void | Promise<void>;
}

let current: ClientModuleApi | undefined;

/** The module's default export: `export default defineModule({ activate(api) { ... } })`. */
export const defineModule = (definition: ClientModuleDefinition): ClientModuleDefinition => ({
    activate(api) {
        current = api;

        return definition.activate(api);
    },
    async deactivate() {
        try {
            await definition.deactivate?.();
        } finally {
            current = undefined;
        }
    },
});

/** The API the module was activated with, for its components and helpers. */
export const useApi = (): ClientModuleApi => {
    if (!current) throw new Error('useApi() before the module was activated: call it from a component or after activate(api).');

    return current;
};

/** The address of a file beside the module in its plugin's `wwwroot/` (`assetUrl('icon.png')`). */
export const assetUrl = (path: string): string => new URL(path, useApi().baseUrl).href;

/** A channel's key with the type of what is published on it: what two modules agree on. */
export interface ClientModuleChannel<T> {
    readonly key: string;
    publish: (value: T) => void;
    subscribe: (handler: (value: T | undefined, from: string | undefined) => void) => ClientModuleDispose;
    get: () => T | undefined;
}

/**
 * `api.channels` for one key, typed: `const promo = defineChannel<Article[]>('news.promo')`, then
 * `promo.publish(articles)` in one module and `promo.subscribe(list => ...)` (or `useChannel(promo)`)
 * in another. Each call reaches the API of the module it runs in.
 */
export const defineChannel = <T>(key: string): ClientModuleChannel<T> => ({
    key,
    publish: value => useApi().channels.publish<T>(key, value),
    subscribe: handler => useApi().channels.subscribe<T>(key, handler),
    get: () => useApi().channels.get<T>(key),
});

/** A channel's value in a component, drawn again when it changes; `undefined` until one is published. */
export const useChannel = <T>(channel: ClientModuleChannel<T>): T | undefined => host().react.useSyncExternalStore(channel.subscribe, channel.get);

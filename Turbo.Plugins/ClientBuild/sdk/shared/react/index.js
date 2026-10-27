// The client's own react, as the client module SDK's bundler preset (clientModuleHostPlugin) reads it:
// a module never bundles a second copy.
const host = globalThis.NitroClientModuleHost;
if (!host) throw new Error('No Nitro client here: this client module runs inside the client.');
module.exports = host.react;

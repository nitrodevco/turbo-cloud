# Running behind Cloudflare (nginx on Ploi)

How to put the hotel's sites (the admin panel, the client, the game's WebSocket) behind
Cloudflare's proxy on a Ploi server, so that:

- nginx sees each **visitor's real address**, not Cloudflare's. Turbo's admin sign-in rate limit
  and logs depend on it;
- the sites **only answer Cloudflare**. Someone who finds the server's own address can't reach the
  sites directly and go around Cloudflare's protection.

The steps assume Ubuntu with Ploi's nginx layout. `example.com` stands for your domain.

## 1. In Cloudflare

1. **Proxy the records.** Each site's DNS record (`admin.example.com`, ...) should have the
   orange cloud on.
2. **SSL/TLS mode: Full (strict).** Every Ploi site has its own certificate (each site's SSL
   tab). Flexible would send traffic from Cloudflare to the server unencrypted.
3. **Don't cache the admin panel's pages.** If a Cache Rule caches everything on the domain,
   exclude `admin.example.com` from it. Its `/assets/` files may be cached; they're named after
   their contents.

Cloudflare's proxy carries HTTP, HTTPS and WebSockets. A raw TCP game port isn't proxied, so the
steps below don't cover it.

## 2. Cloudflare's address ranges, once for the server

Cloudflare publishes the ranges its proxies connect from: <https://www.cloudflare.com/ips/>.
They change rarely, but they do change. Check them now and then, and when they change, update
the two files in this section.

### Which connections came from Cloudflare

Create **`/etc/nginx/conf.d/cloudflare.conf`**. nginx reads `conf.d` inside its `http { }` block,
which is where a `geo` has to be. Check with `grep -n conf.d /etc/nginx/nginx.conf`; Ubuntu and
Ploi include it by default.

```nginx
# 1 when the connection itself came from Cloudflare. Reads the address that connected
# ($realip_remote_addr), before real_ip replaces it with the visitor's.
geo $realip_remote_addr $from_cloudflare {
    default 0;

    173.245.48.0/20 1;
    103.21.244.0/22 1;
    103.22.200.0/22 1;
    103.31.4.0/22 1;
    141.101.64.0/18 1;
    108.162.192.0/18 1;
    190.93.240.0/20 1;
    188.114.96.0/20 1;
    197.234.240.0/22 1;
    198.41.128.0/17 1;
    162.158.0.0/15 1;
    104.16.0.0/13 1;
    104.24.0.0/14 1;
    172.64.0.0/13 1;
    131.0.72.0/22 1;

    2400:cb00::/32 1;
    2606:4700::/32 1;
    2803:f800::/32 1;
    2405:b500::/32 1;
    2405:8100::/32 1;
    2a06:98c0::/29 1;
    2c0f:f248::/32 1;
}
```

### Only Cloudflare may connect

Create **`/etc/nginx/snippets/cloudflare-only.conf`**, the rule a site includes to refuse
everyone else:

```nginx
# Inside a server block: only Cloudflare may connect; anyone else is dropped without an answer.
if ($from_cloudflare = 0) {
    return 444;
}
```

`444` is nginx's "close the connection without replying". A scan of the server's address gets
nothing back.

> **Why not `allow` / `deny`?** The real-IP step below runs before nginx's access check. Once it
> has run, `$remote_addr` is the *visitor's* address, so `allow <Cloudflare ranges>; deny all;`
> refuses every real visitor. The `geo` above reads `$realip_remote_addr`, which keeps the
> address that actually connected.

## 3. Each site behind Cloudflare

Ploi includes every file in `/etc/nginx/ploi/<site>/server/` inside that site's `server { }`
block. Put the site's Cloudflare settings there rather than in the site's NGINX configuration
in Ploi: Ploi leaves the folder alone when it rewrites the site's config.

Create **`/etc/nginx/ploi/admin.example.com/server/cloudflare.conf`**:

```nginx
# The visitor's real address, from the header Cloudflare adds, believed only from Cloudflare.
real_ip_header CF-Connecting-IP;
set_real_ip_from 173.245.48.0/20;
set_real_ip_from 103.21.244.0/22;
set_real_ip_from 103.22.200.0/22;
set_real_ip_from 103.31.4.0/22;
set_real_ip_from 141.101.64.0/18;
set_real_ip_from 108.162.192.0/18;
set_real_ip_from 190.93.240.0/20;
set_real_ip_from 188.114.96.0/20;
set_real_ip_from 197.234.240.0/22;
set_real_ip_from 198.41.128.0/17;
set_real_ip_from 162.158.0.0/15;
set_real_ip_from 104.16.0.0/13;
set_real_ip_from 104.24.0.0/14;
set_real_ip_from 172.64.0.0/13;
set_real_ip_from 131.0.72.0/22;
set_real_ip_from 2400:cb00::/32;
set_real_ip_from 2606:4700::/32;
set_real_ip_from 2803:f800::/32;
set_real_ip_from 2405:b500::/32;
set_real_ip_from 2405:8100::/32;
set_real_ip_from 2a06:98c0::/29;
set_real_ip_from 2c0f:f248::/32;

# Nobody but Cloudflare.
include /etc/nginx/snippets/cloudflare-only.conf;
```

Do the same for each site that should only be reached through Cloudflare. Change the folder name
for each site; the file itself is the same. Leave the file out for any site that people should
reach directly.

If **every** site on the server is behind Cloudflare, the `real_ip_header` and
`set_real_ip_from` lines may go into `/etc/nginx/conf.d/cloudflare.conf` once instead, and each
site's file shrinks to the `include` line.

End every file you write here with a line break. nginx joins included files together, so a file
whose last line has no line break runs into the next file's first line.

## 4. Apply and check

```bash
sudo nginx -t && sudo systemctl reload nginx
```

`nginx -t` must report the configuration ok. If it fails, nginx keeps running the **old**
configuration, whatever the files now say. `sudo nginx -T` prints the files as they are on disk,
not what is running, so it can't tell you whether the reload worked.

Then, from **another machine** (from the server itself, a request doesn't come from Cloudflare
either, so it is dropped too):

```bash
# Through Cloudflare: answers as usual.
curl -sI https://admin.example.com/login | head -1

# Directly to the server, going around Cloudflare: dropped, "Empty reply from server".
curl -skI --resolve admin.example.com:443:<server address> https://admin.example.com/login
```

## Keeping it up

- **Cloudflare's ranges change:** update `/etc/nginx/conf.d/cloudflare.conf` and every
  `set_real_ip_from` list, then `nginx -t` and reload.
- **These files aren't Ploi's.** They survive deploys and Ploi's config edits, but Ploi's
  NGINX editor doesn't show them. Note where they are for whoever runs the server next.
- **A stronger version** is a firewall that accepts ports 80 and 443 only from Cloudflare's
  ranges. It refuses the connection before TLS even starts, but it covers every site on the
  server, so use it only if they are all behind Cloudflare.

## Troubleshooting

| Symptom | Cause and fix |
| --- | --- |
| Every visitor is refused | The block uses `allow`/`deny`, or `$remote_addr`, instead of the `geo` on `$realip_remote_addr` above. |
| Direct requests still answered | `nginx -t` failed, so the old config is still running; or the site's `server/` file is missing the `include`. |
| `nginx -t`: `duplicate "geo" ... $from_cloudflare` | The ranges are defined twice, for example once in `conf.d` and again in a site's `before/` folder. Keep the `conf.d` one. |
| Sign-in "Too many attempts" for every staff member at once | The site has no `set_real_ip_from` lines, or is missing Cloudflare's IPv6 ranges, so everyone shares Cloudflare's address. |
| A page works through the address but a link to it gives Cloudflare's 404 | Cloudflare cached a 404 from before the site was set up. Purge the cache (Caching > Configuration > Purge), and don't cache the panel's pages. |

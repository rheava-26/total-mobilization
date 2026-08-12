/* Total Mobilization — service worker.
   Makes the game an installable, offline-capable app. HTML is network-first
   (you always get the latest version while online, and a cached copy when
   offline); icons/manifest are cache-first. Cross-origin requests (the
   YouTube music player) are left untouched. Bump CACHE to force a refresh. */
const CACHE = 'tm-v1';
const ASSETS = ['./', 'index.html', 'title.html', 'manifest.webmanifest', 'icon-192.png', 'icon-512.png', 'apple-touch-icon.png'];

self.addEventListener('install', e => {
  e.waitUntil(caches.open(CACHE).then(c => c.addAll(ASSETS)).then(() => self.skipWaiting()));
});
self.addEventListener('activate', e => {
  e.waitUntil(caches.keys().then(ks => Promise.all(ks.filter(k => k !== CACHE).map(k => caches.delete(k)))).then(() => self.clients.claim()));
});
self.addEventListener('fetch', e => {
  const req = e.request;
  if (req.method !== 'GET') return;
  let url;
  try { url = new URL(req.url); } catch (_) { return; }
  if (url.origin !== location.origin) return;   // never intercept YouTube / other hosts
  if (req.mode === 'navigate' || req.destination === 'document') {
    e.respondWith(
      fetch(req).then(r => { const cp = r.clone(); caches.open(CACHE).then(c => c.put(req, cp)); return r; })
        .catch(() => caches.match(req).then(m => m || caches.match('title.html')))
    );
  } else {
    e.respondWith(
      caches.match(req).then(m => m || fetch(req).then(r => { const cp = r.clone(); caches.open(CACHE).then(c => c.put(req, cp)); return r; }))
    );
  }
});

/* global self, caches, fetch, URL */
const SHELL_CACHE = 'otklik-shell-v2';
const STATIC_CACHE = 'otklik-static-v1';
const OFFLINE_URL = '/offline.html';
const SHELL_FILES = [OFFLINE_URL, '/offline.css', '/manifest.webmanifest', '/icon-192.svg', '/icon-512.svg'];

self.addEventListener('install', (event) => {
  event.waitUntil(caches.open(SHELL_CACHE).then((cache) => cache.addAll(SHELL_FILES)));
  self.skipWaiting();
});

self.addEventListener('activate', (event) => {
  event.waitUntil(caches.keys().then((keys) => Promise.all(keys
    .filter((key) => ![SHELL_CACHE, STATIC_CACHE].includes(key))
    .map((key) => caches.delete(key)))).then(() => self.clients.claim()));
});

self.addEventListener('fetch', (event) => {
  const request = event.request;
  if (request.method !== 'GET') return;
  const url = new URL(request.url);
  if (url.origin !== self.location.origin) return;
  if (request.mode === 'navigate') {
    event.respondWith(fetch(request, { cache: 'no-store' }).catch(() => caches.match(OFFLINE_URL)));
    return;
  }
  if (url.pathname.startsWith('/api/') || url.pathname.startsWith('/hubs/') || url.pathname.startsWith('/staff')) {
    event.respondWith(fetch(request, { cache: 'no-store' }));
    return;
  }
  if (url.pathname.startsWith('/assets/')) {
    event.respondWith(caches.open(STATIC_CACHE).then(async (cache) => {
      const cached = await cache.match(request);
      if (cached) return cached;
      const response = await fetch(request);
      if (response.ok) await cache.put(request, response.clone());
      return response;
    }));
  }
});

self.addEventListener('push', (event) => {
  event.waitUntil(self.registration.showNotification('Отклик', {
    body: 'В обращении есть обновление',
    icon: '/icon-192.svg',
    badge: '/icon-192.svg',
    tag: 'otklik-appeal-update',
    data: { url: '/appeal/status' },
  }));
});

self.addEventListener('notificationclick', (event) => {
  event.notification.close();
  event.waitUntil(self.clients.matchAll({ type: 'window', includeUncontrolled: true }).then(async (clients) => {
    const existing = clients.find((client) => new URL(client.url).origin === self.location.origin);
    if (existing) {
      await existing.navigate('/appeal/status');
      return existing.focus();
    }
    return self.clients.openWindow('/appeal/status');
  }));
});

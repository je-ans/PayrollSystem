const CACHE_NAME = 'drmc-bms-cache-v5';
const STATIC_FOLDERS = [
    '/css/',
    '/js/lib/',
    '/lib/',
    '/plugins/',
    '/image/',
    '/plugins/',
    '/dist/',
];

const STATIC_ASSETS = [
    '/'
];

// Don't cache these patterns (they change frequently or are user-specific)
const EXCLUDE_PATTERNS = [
    '/api/',
    'GenerateReport',
    'browserLink'
];

// On install, cache static assets
self.addEventListener('install', event => {
    event.waitUntil(
        caches.open(CACHE_NAME).then(cache => {
            // Only cache the specific assets listed, not folders
            // Folders will be cached dynamically on first access
            return Promise.all(
                STATIC_ASSETS.map(url =>
                    fetch(url)
                        .then(response => {
                            if (!response.ok) {
                                //console.warn(`Failed to cache ${url}: ${response.status}`);
                                return;
                            }
                            return cache.put(url, response);
                        })
                        .catch(err => {
                            console.warn(`Failed to cache ${url}:`, err);
                        })
                )
            );
        }).then(() => {
            //console.log('Service Worker installed');
        })
    );
    self.skipWaiting();
});

// On activate, clean up old caches
self.addEventListener('activate', event => {
    //console.log('Service Worker activating...');
    event.waitUntil(
        caches.keys().then(cacheNames => {
            return Promise.all(
                cacheNames
                    .filter(name => name !== CACHE_NAME)
                    .map(name => {
                        //console.log('Deleting old cache:', name);
                        return caches.delete(name);
                    })
            );
        }).then(() => {
            //console.log('Service Worker activated');
        })
    );
    return self.clients.claim();
});

// Fetch handler with proper caching
self.addEventListener('fetch', event => {
    const req = event.request;
    const url = new URL(req.url);

    // Only handle GET requests
    if (req.method !== 'GET') return;

    // Skip excluded patterns (API calls, dynamic content)
    if (EXCLUDE_PATTERNS.some(pattern => url.pathname.includes(pattern) || url.href.includes(pattern))) {
        return; // Let it go to network
    }

    // Handle navigation requests (HTML pages)
    if (req.mode === 'navigate') {
        event.respondWith(
            fetch(req)
                .then(response => {
                    if (response.ok) {
                        const copy = response.clone();
                        caches.open(CACHE_NAME).then(cache => cache.put(req, copy));
                    }
                    return response;
                })
                .catch(async () => {
                    const cachedResponse = await caches.match(req);
                    if (cachedResponse) return cachedResponse;

                    const offlinePage = await caches.match('/offline.html');
                    if (offlinePage) return offlinePage;

                    return new Response(
                        '<!DOCTYPE html><html><head><title>Offline</title></head><body><h1>You\'re Offline</h1><p>No internet connection. Please check your connection and try again.</p></body></html>',
                        {
                            headers: {
                                'Content-Type': 'text/html',
                                'Cache-Control': 'no-cache'
                            }
                        }
                    );
                })
        );
        return;
    }

    // Handle manifest.json dynamically
    if (url.pathname === '/manifest.json') {
        event.respondWith(
            fetch(req)
                .then(response => {
                    if (response.ok) {
                        const copy = response.clone();
                        caches.open(CACHE_NAME).then(cache => cache.put(req, copy));
                    }
                    return response;
                })
                .catch(() => caches.match(req))
        );
        return;
    }

    // Handle offline.html dynamically
    if (url.pathname === '/offline.html') {
        event.respondWith(
            fetch(req)
                .then(response => {
                    if (response.ok) {
                        const copy = response.clone();
                        caches.open(CACHE_NAME).then(cache => cache.put(req, copy));
                    }
                    return response;
                })
                .catch(() => caches.match(req))
        );
        return;
    }

    // For static assets: cache-first strategy
    const pathname = url.pathname;

    // Check if URL is in a static folder
    const isInStaticFolder = STATIC_FOLDERS.some(folder => pathname.startsWith(folder));
    const isStaticAsset = isInStaticFolder || STATIC_ASSETS.some(asset => {
        if (asset === '/') return false;
        return pathname.includes(asset) || pathname.endsWith(asset);
    });

    if (isStaticAsset) {
        // Create a normalized cache key (ignore query strings)
        const cacheKey = new Request(url.origin + pathname);

        event.respondWith(
            caches.match(cacheKey).then(cachedResponse => {
                if (cachedResponse) {
                    return cachedResponse;
                }
                return fetch(req).then(response => {
                    if (response.ok && response.status === 200) {
                        const copy = response.clone();
                        caches.open(CACHE_NAME).then(cache => cache.put(cacheKey, copy));
                    }
                    return response;
                });
            })
        );
        return;
    }

    // For images, scripts, and styles: cache-first with dynamic caching
    if (req.destination === 'image' || req.destination === 'script' || req.destination === 'style' || req.destination === 'font') {
        // Create normalized cache key (ignore query strings)
        const pathname = url.pathname;
        const cacheKey = new Request(url.origin + pathname);

        event.respondWith(
            caches.match(cacheKey).then(cachedResponse => {
                if (cachedResponse) {
                    return cachedResponse;
                }
                return fetch(req).then(response => {
                    if (response.ok && response.status === 200) {
                        const copy = response.clone();
                        caches.open(CACHE_NAME).then(cache => {
                            cache.put(cacheKey, copy).catch(err => {
                            });
                        });
                    }
                    return response;
                }).catch(() => {
                    // Fallback for images
                    if (req.destination === 'image') {
                        return caches.match('/image/drmc_logo.png');
                    }
                    return new Response('Resource unavailable offline', {
                        status: 503,
                        statusText: 'Service Unavailable'
                    });
                });
            })
        );
        return;
    }

    // Default: network-first for everything else (API calls, etc.)
    event.respondWith(
        fetch(req).catch(() => {
            return caches.match(req).then(cached => {
                return cached || new Response('Offline', {
                    status: 503,
                    statusText: 'Service Unavailable'
                });
            });
        })
    );
});
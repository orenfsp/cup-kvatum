import { readFileSync } from 'node:fs';
import { resolve } from 'node:path';
import { describe, expect, it } from 'vitest';

describe('privacy-preserving PWA contract', () => {
  const publicFile = (name: string) => readFileSync(resolve('public', name), 'utf8');

  it('starts from a neutral URL and has install metadata', () => {
    const manifest = JSON.parse(publicFile('manifest.webmanifest'));
    expect(manifest.start_url).toBe('/appeal/status');
    expect(manifest.display).toBe('standalone');
    expect(manifest.icons).toHaveLength(2);
    expect(JSON.stringify(manifest)).not.toMatch(/track|ticket|appealId/i);
  });

  it('caches only the shell and static application assets', () => {
    const worker = publicFile('sw.js');
    const shellDeclaration = worker.match(/const SHELL_FILES = \[([^;]+)\];/)?.[1] ?? '';
    expect(worker).toContain("const OFFLINE_URL = '/offline.html'");
    expect(shellDeclaration).toContain('OFFLINE_URL');
    expect(shellDeclaration).not.toMatch(/api|hubs|staff|appeal\/status/i);
    expect(worker).toContain("fetch(request, { cache: 'no-store' })");
    expect(worker).toContain("url.pathname.startsWith('/api/')");
  });

  it('shows one neutral notification regardless of incoming payload', () => {
    const worker = publicFile('sw.js');
    const pushHandler = worker.slice(worker.indexOf("self.addEventListener('push'"), worker.indexOf("self.addEventListener('notificationclick'"));
    expect(pushHandler).toContain("'В обращении есть обновление'");
    expect(pushHandler).not.toMatch(/event\.data|track|category|priority|expert|message\.body/i);
  });

  it('offline page cannot contain previously viewed content', () => {
    const offline = publicFile('offline.html');
    expect(offline).toContain('Содержимое обращений не сохраняется в офлайн-кэше');
    expect(offline).not.toMatch(/localStorage|sessionStorage|indexedDB/i);
  });
});

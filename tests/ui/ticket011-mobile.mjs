import fs from 'node:fs';

const port = Number(process.env.OTKLIK_CDP_PORT ?? '9344');
const output = process.env.OTKLIK_QA_OUTPUT ?? 'C:/Users/Admin/AppData/Local/Temp/otklik-ticket011-qa';

const pages = await (await fetch(`http://127.0.0.1:${port}/json`)).json();
const page = pages.find((item) => item.type === 'page');
const socket = new WebSocket(page.webSocketDebuggerUrl);
await new Promise((resolve, reject) => { socket.onopen = resolve; socket.onerror = reject; });
let id = 0;
const pending = new Map();
socket.onmessage = (event) => {
  const message = JSON.parse(event.data);
  if (!message.id || !pending.has(message.id)) return;
  const [resolve, reject] = pending.get(message.id);
  pending.delete(message.id);
  if (message.error) reject(new Error(message.error.message)); else resolve(message.result);
};
const send = (method, params = {}) => new Promise((resolve, reject) => {
  const callId = ++id;
  pending.set(callId, [resolve, reject]);
  socket.send(JSON.stringify({ id: callId, method, params }));
});

await send('Page.enable');
await send('Emulation.setDeviceMetricsOverride', { width: 320, height: 1000, deviceScaleFactor: 1, mobile: true });
fs.mkdirSync(output, { recursive: true });
await send('Page.navigate', { url: 'http://localhost:3000/staff' });
await wait(500);
const login = await send('Runtime.evaluate', {
  expression: `(async () => {
    const csrf = await fetch('/api/staff/auth/csrf', { credentials: 'same-origin' }).then(r => r.json());
    return (await fetch('/api/staff/auth/login', {
      method: 'POST', credentials: 'same-origin',
      headers: { 'Content-Type': 'application/json', 'X-CSRF-TOKEN': csrf.token },
      body: JSON.stringify({ userName: 'administrator', password: 'AdminPanel!2026' })
    })).status;
  })()`,
  awaitPromise: true,
  returnByValue: true,
});
if (login.result.value !== 200) throw new Error(`Administrator login failed: ${login.result.value}`);
await send('Page.navigate', { url: 'http://localhost:3000/staff' });
await wait(700);
await send('Runtime.evaluate', {
  expression: `[...document.querySelectorAll('.staff-nav__item')]
    .find((button) => button.textContent.includes('Аналитика'))?.click()`,
});
await wait(1000);

const result = await send('Runtime.evaluate', {
  expression: `JSON.stringify({
    innerWidth,
    clientWidth: document.documentElement.clientWidth,
    scrollWidth: document.documentElement.scrollWidth,
    bodyFont: getComputedStyle(document.body).fontFamily,
    headingVisible: [...document.querySelectorAll('h1')].some((item) => item.textContent.trim() === 'Аналитика'),
    privacyTextVisible: document.body.textContent.includes('Тексты, чат, заметки, файлы, контакты и трек-номера не попадают'),
    summaryVisible: Boolean(document.querySelector('.analytics-summary')),
    exportButtons: [...document.querySelectorAll('.analytics-export md-outlined-button')].map((item) => item.textContent.trim()),
    navigationItems: [...document.querySelectorAll('.staff-nav__item')].map((item) => ({
      text: item.textContent.trim(),
      left: Math.round(item.getBoundingClientRect().left),
      right: Math.round(item.getBoundingClientRect().right),
      top: Math.round(item.getBoundingClientRect().top),
    })),
    hasGradient: [...document.querySelectorAll('*')].some((item) => getComputedStyle(item).backgroundImage.includes('gradient')),
    hasDecorativeListMarker: [...document.querySelectorAll('li')].some((item) => getComputedStyle(item).listStyleType !== 'none'),
  })`,
  returnByValue: true,
});
const shot = await send('Page.captureScreenshot', { format: 'png', fromSurface: true, captureBeyondViewport: false });
const screenshot = `${output}/admin-analytics-320.png`;
fs.writeFileSync(screenshot, Buffer.from(shot.data, 'base64'));
console.log(JSON.stringify({ metrics: JSON.parse(result.result.value), screenshot }, null, 2));
socket.close();

function wait(milliseconds) {
  return new Promise((resolve) => setTimeout(resolve, milliseconds));
}

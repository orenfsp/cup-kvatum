import fs from 'node:fs';

const port = Number(process.env.OTKLIK_CDP_PORT ?? '9342');
const output = process.env.OTKLIK_QA_OUTPUT ?? 'C:/Users/Admin/AppData/Local/Temp/otklik-ticket009-qa';
const trackNumber = process.env.OTKLIK_QA_TRACK ?? 'ОТК-NMJ2-QBQU';

async function connect() {
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
  return {
    socket,
    send(method, params = {}) {
      return new Promise((resolve, reject) => {
        const callId = ++id;
        pending.set(callId, [resolve, reject]);
        socket.send(JSON.stringify({ id: callId, method, params }));
      });
    },
  };
}

const client = await connect();
await client.send('Page.enable');
await client.send('Emulation.setDeviceMetricsOverride', {
  width: 320,
  height: 1000,
  deviceScaleFactor: 1,
  mobile: true,
});
fs.mkdirSync(output, { recursive: true });

await client.send('Page.navigate', { url: 'http://localhost:3000/appeal/status' });
await wait(700);
await client.send('Runtime.evaluate', { expression: `document.querySelector('input')?.focus()` });
await client.send('Input.dispatchKeyEvent', { type: 'keyDown', key: 'a', code: 'KeyA', modifiers: 2 });
await client.send('Input.dispatchKeyEvent', { type: 'keyUp', key: 'a', code: 'KeyA', modifiers: 2 });
await client.send('Input.insertText', { text: trackNumber });
await wait(100);
await client.send('Runtime.evaluate', {
  expression: `[...document.querySelectorAll('md-filled-button')]
    .find((button) => button.textContent.includes('Показать статус'))?.click()`,
});
await wait(700);
const publicMetrics = await metrics(client, '.crisis-help');
await screenshot(client, `${output}/public-crisis-320.png`);

const login = await client.send('Runtime.evaluate', {
  expression: `(async () => {
    const csrf = await fetch('/api/staff/auth/csrf', { credentials: 'same-origin' }).then(r => r.json());
    return (await fetch('/api/staff/auth/login', {
      method: 'POST', credentials: 'same-origin',
      headers: { 'Content-Type': 'application/json', 'X-CSRF-TOKEN': csrf.token },
      body: JSON.stringify({ userName: 'operator', password: 'Operator!2026' })
    })).status;
  })()`,
  awaitPromise: true,
  returnByValue: true,
});
if (login.result.value !== 200) throw new Error(`Operator login failed: ${login.result.value}`);
await client.send('Page.navigate', { url: 'http://localhost:3000/staff' });
await wait(700);
await client.send('Runtime.evaluate', {
  expression: `[...document.querySelectorAll('.staff-nav__item')]
    .find((button) => button.textContent.includes('Срочная помощь'))?.click()`,
});
await wait(700);
const operatorMetrics = await metrics(client, '.crisis-workspace');
await screenshot(client, `${output}/operator-crisis-320.png`);

console.log(JSON.stringify({ publicMetrics, operatorMetrics }, null, 2));
client.socket.close();

async function metrics(target, requiredSelector) {
  const result = await target.send('Runtime.evaluate', {
    expression: `JSON.stringify({
      innerWidth,
      clientWidth: document.documentElement.clientWidth,
      scrollWidth: document.documentElement.scrollWidth,
      bodyFont: getComputedStyle(document.body).fontFamily,
      requiredRegionVisible: Boolean(document.querySelector(${JSON.stringify(requiredSelector)})),
      hasGradient: [...document.querySelectorAll('*')].some((item) => getComputedStyle(item).backgroundImage.includes('gradient')),
      hasDecorativeListMarker: [...document.querySelectorAll('li')].some((item) => getComputedStyle(item).listStyleType !== 'none'),
    })`,
    returnByValue: true,
  });
  return JSON.parse(result.result.value);
}

async function screenshot(target, path) {
  const shot = await target.send('Page.captureScreenshot', {
    format: 'png',
    fromSurface: true,
    captureBeyondViewport: false,
  });
  fs.writeFileSync(path, Buffer.from(shot.data, 'base64'));
}

function wait(milliseconds) {
  return new Promise((resolve) => setTimeout(resolve, milliseconds));
}

import fs from 'node:fs';

const port = Number(process.env.OTKLIK_CDP_PORT ?? '9341');
const output = process.env.OTKLIK_QA_OUTPUT ?? 'C:/Users/Admin/AppData/Local/Temp/otklik-ticket008-qa';
const trackNumber = process.env.OTKLIK_QA_TRACK ?? 'ОТК-MMBU-SRUV';

async function connect() {
  const pages = await (await fetch(`http://127.0.0.1:${port}/json`)).json();
  const page = pages.find((item) => item.type === 'page');
  const socket = new WebSocket(page.webSocketDebuggerUrl);
  await new Promise((resolve, reject) => {
    socket.onopen = resolve;
    socket.onerror = reject;
  });

  let id = 0;
  const pending = new Map();
  socket.onmessage = (event) => {
    const message = JSON.parse(event.data);
    if (!message.id || !pending.has(message.id)) return;
    const [resolve, reject] = pending.get(message.id);
    pending.delete(message.id);
    if (message.error) reject(new Error(message.error.message));
    else resolve(message.result);
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
await client.send('Page.navigate', { url: 'http://localhost:3000/appeal/status' });
await new Promise((resolve) => setTimeout(resolve, 800));

await client.send('Runtime.evaluate', {
  expression: `document.querySelector('input')?.focus()`,
});
await client.send('Input.dispatchKeyEvent', {
  type: 'keyDown',
  key: 'a',
  code: 'KeyA',
  modifiers: 2,
});
await client.send('Input.dispatchKeyEvent', {
  type: 'keyUp',
  key: 'a',
  code: 'KeyA',
  modifiers: 2,
});
await client.send('Input.insertText', { text: trackNumber });
await new Promise((resolve) => setTimeout(resolve, 100));
await client.send('Runtime.evaluate', {
  expression: `[...document.querySelectorAll('md-filled-button')]
    .find((button) => button.textContent.includes('Показать статус'))
    ?.click()`,
});
await new Promise((resolve) => setTimeout(resolve, 1000));

const result = await client.send('Runtime.evaluate', {
  expression: `JSON.stringify({
    innerWidth,
    clientWidth: document.documentElement.clientWidth,
    scrollWidth: document.documentElement.scrollWidth,
    bodyFont: getComputedStyle(document.body).fontFamily,
    fieldValue: document.querySelector('input')?.value,
    pageText: document.querySelector('main')?.innerText.slice(0, 500),
    status: [...document.querySelectorAll('h2')].map((item) => item.textContent),
    recommendationVersions: [...document.querySelectorAll('.recommendation-item strong')].map((item) => item.textContent),
    hasGradient: [...document.querySelectorAll('*')].some((item) => getComputedStyle(item).backgroundImage.includes('gradient')),
    hasDecorativeListMarker: [...document.querySelectorAll('li')].some((item) => getComputedStyle(item).listStyleType !== 'none'),
  })`,
  returnByValue: true,
});

fs.mkdirSync(output, { recursive: true });
const shot = await client.send('Page.captureScreenshot', {
  format: 'png',
  fromSurface: true,
  captureBeyondViewport: false,
});
fs.writeFileSync(`${output}/public-outcome-320.png`, Buffer.from(shot.data, 'base64'));
console.log(result.result.value);
client.socket.close();

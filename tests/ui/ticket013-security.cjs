const fs = require('node:fs');
const path = require('node:path');
const { chromium } = require('playwright');

const output = process.env.OTKLIK_QA_OUTPUT || 'C:/Users/Admin/AppData/Local/Temp/otklik-ticket013-qa';
fs.mkdirSync(output, { recursive: true });

(async () => {
  const browser = await chromium.launch({ headless: true, channel: 'chrome' });
  const context = await browser.newContext({ viewport: { width: 320, height: 900 }, serviceWorkers: 'allow' });
  const page = await context.newPage();
  const errors = [];
  page.on('console', (message) => { if (message.type() === 'error') errors.push(message.text()); });
  page.on('pageerror', (error) => errors.push(error.message));
  page.on('requestfailed', (request) => errors.push(`${request.method()} ${request.url()}: ${request.failure()?.errorText}`));

  const response = await page.goto('http://localhost:3000/', { waitUntil: 'networkidle' });
  const landing = await page.evaluate(() => ({
    clientWidth: document.documentElement.clientWidth,
    scrollWidth: document.documentElement.scrollWidth,
    bodyFont: getComputedStyle(document.body).fontFamily,
    materialActions: document.querySelectorAll('md-filled-button, md-outlined-button, md-text-button').length,
    primaryNavigation: [...document.querySelectorAll('.primary-nav__link')].map((item) => item.textContent.trim()),
    gradients: [...document.querySelectorAll('*')].filter((item) => getComputedStyle(item).backgroundImage.includes('gradient')).length,
    visibleListMarkers: [...document.querySelectorAll('li')].filter((item) => getComputedStyle(item).listStyleType !== 'none').length,
    externalResources: performance.getEntriesByType('resource')
      .map((item) => new URL(item.name))
      .filter((url) => url.origin !== location.origin)
      .map((url) => url.href),
  }));
  await page.screenshot({ path: path.join(output, 'landing-320.png'), fullPage: true });

  await page.goto('http://localhost:3000/appeal/status', { waitUntil: 'networkidle' });
  const status = await page.evaluate(() => ({
    clientWidth: document.documentElement.clientWidth,
    scrollWidth: document.documentElement.scrollWidth,
    bodyFont: getComputedStyle(document.body).fontFamily,
    heading: document.querySelector('h1')?.textContent?.trim(),
    gradients: [...document.querySelectorAll('*')].filter((item) => getComputedStyle(item).backgroundImage.includes('gradient')).length,
    visibleListMarkers: [...document.querySelectorAll('li')].filter((item) => getComputedStyle(item).listStyleType !== 'none').length,
  }));
  await page.screenshot({ path: path.join(output, 'status-320.png'), fullPage: true });

  const result = {
    landing,
    status,
    headers: {
      csp: response.headers()['content-security-policy'],
      referrerPolicy: response.headers()['referrer-policy'],
      frameOptions: response.headers()['x-frame-options'],
    },
    errors,
    screenshots: [path.join(output, 'landing-320.png'), path.join(output, 'status-320.png')],
  };
  console.log(JSON.stringify(result, null, 2));

  if (landing.clientWidth !== landing.scrollWidth || status.clientWidth !== status.scrollWidth) process.exitCode = 1;
  if (!landing.bodyFont.toLowerCase().includes('onest') || !status.bodyFont.toLowerCase().includes('onest')) process.exitCode = 1;
  if (landing.primaryNavigation.join('|') !== 'Обратиться|Статус|Кабинет') process.exitCode = 1;
  if (landing.gradients || status.gradients || landing.visibleListMarkers || status.visibleListMarkers) process.exitCode = 1;
  if (landing.externalResources.length || errors.length || !result.headers.csp) process.exitCode = 1;
  await browser.close();
})().catch((error) => {
  console.error(error);
  process.exitCode = 1;
});

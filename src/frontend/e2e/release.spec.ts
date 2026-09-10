import { expect, test, type Page } from '@playwright/test';

const passwords = {
  operator: process.env.OTKLIK_OPERATOR_PASSWORD ?? 'Operator!2026',
  expert: process.env.OTKLIK_EXPERT_PASSWORD ?? 'ExpertHelp!2026',
  administrator: process.env.OTKLIK_ADMINISTRATOR_PASSWORD ?? 'AdminPanel!2026',
};

const onePixelPng = Buffer.from(
  'iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mP8/x8AAusB9Wl2nXQAAAAASUVORK5CYII=',
  'base64',
);

test('UX foundation: calm Material language, accessible names and responsive text zoom', async ({ page }) => {
  await page.setViewportSize({ width: 320, height: 800 });
  await page.goto('/');

  await expect(page.getByRole('heading', { name: 'Когда трудно, не обязательно оставаться с этим одному' })).toBeVisible();
  const navigation = page.getByRole('navigation', { name: 'Основная навигация' });
  await expect(navigation.getByRole('link', { name: 'Обратиться' })).toBeVisible();
  await expect(navigation.getByRole('link', { name: 'Моё обращение' })).toBeVisible();
  await expect(page.getByRole('link', { name: 'Вход для сотрудников' })).toBeVisible();

  const design = await inspectDesign(page);
  expect(design.font).toContain('onest');
  expect(design.gradients).toBe(0);
  expect(design.decorativeBullets).toBe(0);
  expect(design.statusPills).toBe(0);
  expect(design.tooltips).toBe(0);
  expect(design.unnamedActions).toBe(0);
  expect(design.contrast).toBeGreaterThanOrEqual(4.5);
  expect(design.focusContrast).toBeGreaterThanOrEqual(3);
  expect(design.scrollWidth).toBeLessThanOrEqual(design.viewportWidth);

  // 200% browser zoom on a 1280 px desktop exposes roughly a 640 CSS px layout viewport.
  await page.setViewportSize({ width: 640, height: 800 });
  await page.evaluate(() => { document.documentElement.style.fontSize = '200%'; });
  expect(await page.evaluate(() => document.documentElement.scrollWidth)).toBeLessThanOrEqual(
    await page.evaluate(() => document.documentElement.clientWidth),
  );

  await page.keyboard.press('Tab');
  await expect(page.locator(':focus')).not.toHaveCount(0);
});

test('C1: a schoolchild uses informal copy, attaches evidence and receives one track', async ({ page }, testInfo) => {
  test.skip(testInfo.project.name !== 'mobile-chromium', 'C1 runs in the mobile project.');
  await page.setViewportSize({ width: 390, height: 844 });
  await page.goto('/appeal/new');
  await page.locator('.choice-card').filter({ hasText: 'Школьник' }).click();
  await materialButton(page, 'Продолжить').click();
  await expect(page.getByRole('heading', { name: 'Как тебе удобнее рассказать?' })).toBeVisible();
  await page.locator('.choice-card').filter({ hasText: 'Рассказать своими словами' }).click();
  await materialButton(page, 'Продолжить').click();
  await expect(page.getByRole('heading', { name: 'Расскажи, что происходит' })).toBeVisible();
  await page.getByRole('textbox', { name: 'Что происходит' }).fill('Меня регулярно обзывают в школьном чате, и я хочу попросить помощи.');
  await page.locator('input[type="file"]').setInputFiles({ name: 'evidence.png', mimeType: 'image/png', buffer: onePixelPng });
  await materialButton(page, 'Отправить обращение').click();
  await expect(page.getByRole('heading', { name: 'Сохрани трек-номер' })).toBeVisible();
  await expect(page.getByLabel('Трек-номер обращения')).toHaveText(/^ОТК-[A-Z0-9]{4}-[A-Z0-9]{4}$/u);
  await expect(materialButton(page, 'Открыть обращение')).toBeVisible();
  await expectNoOverflow(page);
});

test('C2: an adult uses formal copy and can describe an uncertain category', async ({ page }, testInfo) => {
  test.skip(testInfo.project.name !== 'desktop-chromium', 'C2 runs in the desktop project.');
  await page.goto('/appeal/new');
  await page.locator('.choice-card').filter({ hasText: 'Родитель' }).click();
  await materialButton(page, 'Продолжить').click();
  await expect(page.getByRole('heading', { name: 'Как вам удобнее рассказать?' })).toBeVisible();
  await page.locator('.choice-card').filter({ hasText: 'Выбрать категорию' }).click();
  await materialButton(page, 'Продолжить').click();
  await page.getByRole('button', { name: 'Не знаю, как это назвать' }).click();
  await page.getByRole('textbox', { name: 'Что происходит' }).fill('Не могу точно назвать категорию, но ребёнку нужна спокойная помощь специалиста.');
  await materialButton(page, 'Отправить обращение').click();
  await expect(page.getByRole('heading', { name: 'Сохраните трек-номер' })).toBeVisible();
});

test('C3: operator gets a routed task flow without expert-only content', async ({ page }, testInfo) => {
  test.skip(testInfo.project.name !== 'desktop-chromium', 'C3 runs in the desktop project.');
  await page.setViewportSize({ width: 1024, height: 900 });
  const created = await createAppeal(page, 'Student', 'Проверка последовательного разбора нового обращения оператором.');
  await login(page, 'operator', passwords.operator);
  await page.goto(`/staff/operator/queue/${created.appealId}?stage=new&priority=standard`);
  await expect(page.getByRole('heading', { name: 'Провести разбор обращения' })).toBeFocused();
  await expect(page.locator('.operator-step--context')).toContainText('Проверка последовательного разбора');
  await expect(page.getByText('Категория и приоритет', { exact: true })).toBeVisible();
  await expect(page.getByText('Ответственный специалист', { exact: true })).toBeVisible();
  await expect(page.getByText(/Внутренняя заметка специалиста/u)).toHaveCount(0);
  await page.reload();
  await expect(page).toHaveURL(/stage=new&priority=standard/u);
  await expectNamedActions(page);
});

test('C4: expert work is split into clear routed workspaces', async ({ page }, testInfo) => {
  test.skip(testInfo.project.name !== 'desktop-chromium', 'C4 runs in the desktop project.');
  await login(page, 'expert', passwords.expert);
  await page.goto('/staff/expert/active');
  await expect(page.getByRole('heading', { name: 'В работе' })).toBeVisible();
  const firstCase = page.locator('.case-row').first();
  await expect(firstCase).toBeVisible();
  await firstCase.click();
  await expect(page).toHaveURL(/\/staff\/expert\/cases\/[^/]+\/overview/u);
  for (const name of ['Обзор', 'Диалог', 'Заметки', 'Ответ', 'Команда']) {
    await expect(page.getByRole('link', { name, exact: true })).toBeVisible();
  }
  await page.getByRole('link', { name: 'Заметки', exact: true }).click();
  await expect(page.getByRole('heading', { name: 'Заметки' })).toBeFocused();
  await expect(page.getByText('Видят только разрешённые участники обращения', { exact: false })).toBeVisible();
  await page.goBack();
  await expect(page).toHaveURL(/\/overview/u);
  await page.goForward();
  await expect(page).toHaveURL(/\/notes/u);
  await expectNamedActions(page);
});

test('C5: one secret opens a clear overview and complete cycle history', async ({ page }) => {
  await openTrack(page, 'ОТК-FNSH-89AB');
  await expect(page).toHaveURL(/\/appeal\/overview$/u);
  await expect(page.locator('.appeal-guidance h2')).toBeVisible();
  await expect(page).not.toHaveURL(/ОТК|track/u);
  await page.getByRole('link', { name: 'Вся история', exact: true }).click();
  await expect(page.getByRole('heading', { name: 'История обращения' })).toBeVisible();
  await expect(page.locator('.appeal-cycle summary strong').first()).toBeVisible();
  await expect(page.getByText('Написать снова')).toHaveCount(0);
  await page.reload();
  await expect(page).toHaveURL(/\/appeal\/history$/u);
  await expect(page.getByRole('heading', { name: 'История обращения' })).toBeVisible();
});

test('C6: immediate danger is honest about anonymous limits and keeps emergency contacts visible', async ({ page }, testInfo) => {
  await openTrack(page, 'ОТК-RUSH-DEFG');
  await expect(page.getByRole('heading', { name: 'Если опасность рядом' })).toBeVisible();
  await expect(page.locator('.crisis-help')).toContainText('Перейди туда');
  await expect(page.getByRole('link', { name: 'Позвонить 112' })).toHaveAttribute('href', 'tel:112');
  await expect(page.locator('.crisis-help')).toContainText('8-800-2000-122');
  await expect(page.locator('.crisis-help')).toContainText('124');
  await expect(page.locator('.crisis-help')).toContainText('не знает, кто ты');

  if (testInfo.project.name === 'desktop-chromium') {
    await login(page, 'operator', passwords.operator);
    await page.goto('/staff/operator/urgent');
    await expect(page.getByRole('heading', { name: 'Срочная помощь' })).toBeVisible();
    await expect(page.getByText('Контакт не оставлен', { exact: false }).first()).toBeVisible();
    await expect(page.getByText('Мне угрожают', { exact: false })).toHaveCount(0);
  }
});

test('C7: administrator uses safe list-detail routes and readable audit values', async ({ page }, testInfo) => {
  test.skip(testInfo.project.name !== 'desktop-chromium', 'C7 runs in the desktop project.');
  await page.setViewportSize({ width: 1440, height: 900 });
  await login(page, 'administrator', passwords.administrator);
  await page.goto('/staff/admin');
  await expect(page.getByRole('heading', { name: 'Задачи настройки и контроля' })).toBeFocused();
  await expect(page.getByText(/Тексты обращений, чат, заметки/u)).toBeVisible();
  await page.getByRole('link', { name: 'Категории', exact: true }).click();
  const category = page.locator('.admin-row--link').first();
  await expect(category).toBeVisible();
  await category.click();
  await expect(page).toHaveURL(/\/staff\/admin\/categories\/[^/]+/u);
  await expect(page.locator('[data-detail-heading]')).toBeFocused();
  await page.reload();
  await expect(page.locator('[data-detail-heading]')).toBeFocused();
  await page.goBack();
  await expect(page).toHaveURL(/\/staff\/admin\/categories$/u);
  await page.goForward();
  await expect(page).toHaveURL(/\/staff\/admin\/categories\/[^/]+/u);
  await page.getByRole('link', { name: 'Журнал', exact: true }).click();
  await page.locator('.admin-row--link').first().click();
  await expect(page.locator('pre')).toHaveCount(0);
  await expect(page.locator('body')).not.toContainText('AnalyticsExportCreated');
  await expectNamedActions(page);
});

test('C8: analytics explains its privacy boundary and downloads an anonymous CSV', async ({ page }, testInfo) => {
  test.skip(testInfo.project.name !== 'desktop-chromium', 'C8 runs in the desktop project.');
  await login(page, 'administrator', passwords.administrator);
  await page.goto('/staff/analytics');
  await expect(page.getByRole('heading', { name: 'Аналитика' })).toBeFocused();
  await expect(page.locator('.analytics-workspace')).toContainText('Тексты, чат, заметки, файлы, контакты и трек-номера не попадают');
  await page.getByLabel('Период').selectOption('365');
  const downloadPromise = page.waitForEvent('download');
  await materialButton(page, 'Скачать CSV').click();
  const download = await downloadPromise;
  expect(download.suggestedFilename()).toMatch(/\.csv$/u);
  await expect(page.getByRole('status')).toContainText('Выгрузка подготовлена');
});

test('keyboard-only navigation reaches the primary workspace of every role', async ({ page }, testInfo) => {
  test.skip(testInfo.project.name !== 'desktop-chromium', 'One desktop pass covers the four navigation models.');

  await page.goto('/');
  await activateWithKeyboard(page, page.getByRole('link', { name: 'Обратиться' }));
  await expect(page).toHaveURL(/\/appeal\/new$/u);

  for (const role of [
    { user: 'operator', password: passwords.operator, link: 'Срочная помощь', route: /\/staff\/operator\/urgent$/u },
    { user: 'expert', password: passwords.expert, link: 'В работе', route: /\/staff\/expert\/active$/u },
    { user: 'administrator', password: passwords.administrator, link: 'Категории', route: /\/staff\/admin\/categories$/u },
  ]) {
    await keyboardLogin(page, role.user, role.password);
    await activateWithKeyboard(page, page.getByRole('link', { name: role.link, exact: true }));
    await expect(page).toHaveURL(role.route);
    await expectNamedActions(page);
    await activateWithKeyboard(page, materialButton(page, 'Выйти'));
    await expect(page.getByRole('heading', { name: 'Вход для сотрудников' })).toBeVisible();
  }
});

async function createAppeal(page: Page, applicantType: 'Student' | 'Parent', narrative: string) {
  const response = await page.request.post('/api/public/appeals', {
    data: {
      clientRequestId: crypto.randomUUID(),
      applicantType,
      submissionPath: 'FreeText',
      categoryId: null,
      narrative,
      answers: {},
      crisisContact: null,
    },
  });
  expect(response.ok(), `${response.status()} ${await response.text()}`).toBeTruthy();
  return response.json() as Promise<{ appealId: string; trackNumber: string }>;
}

async function login(page: Page, userName: string, password: string) {
  await page.goto('/staff');
  if (await materialButton(page, 'Выйти').isVisible().catch(() => false)) {
    await materialButton(page, 'Выйти').click();
    await expect(page.getByRole('heading', { name: 'Вход для сотрудников' })).toBeVisible();
  }
  await page.getByLabel('Логин').fill(userName);
  await page.getByLabel('Пароль').fill(password);
  await materialButton(page, 'Войти').click();
  await expect(materialButton(page, 'Выйти')).toBeVisible();
}

async function openTrack(page: Page, trackNumber: string) {
  await page.goto('/appeal/access');
  await expect(page.getByText('Проверяем, сохранён ли доступ на этом устройстве…')).toBeHidden();
  const another = materialButton(page, 'Ввести другой трек-номер');
  if (await another.isVisible().catch(() => false)) await another.click();
  await page.getByLabel('Трек-номер').fill(trackNumber);
  await materialButton(page, 'Открыть обращение').click();
  await expect(page.locator('.status-result')).toBeVisible();
}

async function inspectDesign(page: Page) {
  return page.evaluate(() => {
    const luminance = (color: string) => {
      const channels = color.match(/[\d.]+/g)?.slice(0, 3).map(Number) ?? [];
      const linear = channels.map((channel) => {
        const normalized = channel / 255;
        return normalized <= 0.04045 ? normalized / 12.92 : ((normalized + 0.055) / 1.055) ** 2.4;
      });
      return linear.length === 3 ? 0.2126 * linear[0] + 0.7152 * linear[1] + 0.0722 * linear[2] : 0;
    };
    const heading = document.querySelector('h1')!;
    const foreground = luminance(getComputedStyle(heading).color);
    const background = luminance(getComputedStyle(document.body).backgroundColor);
    const focusProbe = document.querySelector<HTMLElement>('.primary-nav__link')!;
    focusProbe.focus();
    const focus = luminance(getComputedStyle(focusProbe).outlineColor);
    return {
      font: getComputedStyle(document.body).fontFamily.toLowerCase(),
      gradients: [...document.querySelectorAll('*')].filter((element) => getComputedStyle(element).backgroundImage.includes('gradient')).length,
      decorativeBullets: [...document.querySelectorAll('li')].filter((element) => getComputedStyle(element).listStyleType !== 'none').length,
      statusPills: document.querySelectorAll('[class*="pill"], [class*="badge"], [class*="chip"]').length,
      tooltips: document.querySelectorAll('[role="tooltip"], [title]').length,
      unnamedActions: [...document.querySelectorAll('a, button, input, select, textarea, md-filled-button, md-outlined-button')]
        .filter((element) => !(element.getAttribute('aria-label') || element.getAttribute('aria-labelledby') || element.textContent?.trim() || element.closest('label'))).length,
      contrast: (Math.max(foreground, background) + 0.05) / (Math.min(foreground, background) + 0.05),
      focusContrast: (Math.max(focus, background) + 0.05) / (Math.min(focus, background) + 0.05),
      viewportWidth: document.documentElement.clientWidth,
      scrollWidth: document.documentElement.scrollWidth,
    };
  });
}

async function expectNamedActions(page: Page) {
  expect(await page.evaluate(() => [...document.querySelectorAll('a, button, input, select, textarea, md-filled-button, md-outlined-button')]
    .filter((element) => !(element.getAttribute('aria-label') || element.getAttribute('aria-labelledby') || element.textContent?.trim() || element.closest('label'))).length)).toBe(0);
}

async function expectNoOverflow(page: Page) {
  expect(await page.evaluate(() => document.documentElement.scrollWidth)).toBeLessThanOrEqual(
    await page.evaluate(() => document.documentElement.clientWidth),
  );
}

async function keyboardLogin(page: Page, userName: string, password: string) {
  await page.goto('/staff');
  await page.getByLabel('Логин').focus();
  await page.keyboard.type(userName);
  await page.getByLabel('Пароль').focus();
  await page.keyboard.type(password);
  await activateWithKeyboard(page, materialButton(page, 'Войти'));
  await expect(materialButton(page, 'Выйти')).toBeVisible();
}

async function activateWithKeyboard(page: Page, target: ReturnType<Page['locator']>) {
  await target.focus();
  await expect(target).toBeFocused();
  await page.keyboard.press('Enter');
}

function materialButton(page: Page, text: string) {
  return page.locator('md-filled-button, md-outlined-button, md-text-button').filter({ hasText: text }).last();
}

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

test('Material foundation is calm, responsive and role-oriented', async ({ page }) => {
  await page.goto('/');
  await expect(page.getByRole('heading', { name: 'Когда трудно, не обязательно оставаться с этим одному' })).toBeVisible();
  await expect(page.getByRole('navigation', { name: 'Основная навигация' })).toContainText('Обратиться');
  await expect(page.getByRole('navigation', { name: 'Основная навигация' })).toContainText('Статус');
  await expect(page.getByRole('navigation', { name: 'Основная навигация' })).toContainText('Кабинет');

  const design = await page.evaluate(() => {
    const contrastRatio = (foreground: string, background: string) => {
      const luminance = (color: string) => {
        const channels = color.match(/[\d.]+/g)?.slice(0, 3).map(Number) ?? [];
        if (channels.length !== 3) return 0;
        const linear = channels.map((channel) => {
          const normalized = channel / 255;
          return normalized <= 0.04045 ? normalized / 12.92 : ((normalized + 0.055) / 1.055) ** 2.4;
        });
        return 0.2126 * linear[0] + 0.7152 * linear[1] + 0.0722 * linear[2];
      };
      const lighter = Math.max(luminance(foreground), luminance(background));
      const darker = Math.min(luminance(foreground), luminance(background));
      return (lighter + 0.05) / (darker + 0.05);
    };
    return {
      width: document.documentElement.clientWidth,
      scrollWidth: document.documentElement.scrollWidth,
      font: getComputedStyle(document.body).fontFamily,
      gradients: [...document.querySelectorAll('*')]
        .filter((element) => getComputedStyle(element).backgroundImage.includes('gradient')).length,
      listMarkers: [...document.querySelectorAll('li')]
        .filter((element) => getComputedStyle(element).listStyleType !== 'none').length,
      materialActions: document.querySelectorAll('md-filled-button, md-outlined-button, md-text-button').length,
      unnamedActions: [...document.querySelectorAll('a, button, md-filled-button, md-outlined-button, md-text-button')]
        .filter((element) => !(element.getAttribute('aria-label') || element.textContent?.trim())).length,
      textContrast: contrastRatio(
        getComputedStyle(document.querySelector('h1')!).color,
        getComputedStyle(document.body).backgroundColor,
      ),
    };
  });

  expect(design.width).toBe(design.scrollWidth);
  expect(design.font.toLowerCase()).toContain('onest');
  expect(design.gradients).toBe(0);
  expect(design.listMarkers).toBe(0);
  expect(design.materialActions).toBeGreaterThan(0);
  expect(design.unnamedActions).toBe(0);
  expect(design.textContrast).toBeGreaterThanOrEqual(4.5);

  await page.keyboard.press('Tab');
  expect(await page.evaluate(() => document.activeElement !== document.body)).toBeTruthy();
});

test('C1: a schoolchild can tell the story, attach evidence and keep the track number', async ({ page }, testInfo) => {
  test.skip(testInfo.project.name !== 'mobile-chromium', 'C1 is the focused mobile acceptance path.');

  await page.goto('/appeal/new');
  await page.locator('.choice-card').filter({ hasText: 'Школьник' }).click();
  await materialButton(page, 'Продолжить').click();
  await expect(page.getByRole('heading', { name: 'Как тебе удобнее рассказать?' })).toBeVisible();
  await page.locator('.choice-card').filter({ hasText: 'Рассказать своими словами' }).click();
  await materialButton(page, 'Продолжить').click();
  await expect(page.getByRole('heading', { name: 'Расскажи, что происходит' })).toBeVisible();
  await page.getByRole('textbox', { name: 'Что происходит' }).fill('Меня регулярно обзывают в школьном чате, и я хочу попросить помощи.');
  await page.getByPlaceholder('Можно пропустить').first().fill('В школьном чате');
  await page.locator('input[type="file"]').setInputFiles({
    name: 'screenshot.png',
    mimeType: 'image/png',
    buffer: onePixelPng,
  });
  await expect(page.getByText('screenshot.png', { exact: true })).toBeVisible();

  await materialButton(page, 'Отправить обращение').click();
  await expect(page.getByRole('heading', { name: 'Сохраните трек-номер' })).toBeVisible();
  const track = (await page.getByLabel('Трек-номер обращения').textContent())?.trim() ?? '';
  expect(track).toMatch(/^ОТК-[A-Z0-9]{4}-[A-Z0-9]{4}$/u);

  await page.evaluate(() => {
    Object.defineProperty(navigator, 'clipboard', {
      configurable: true,
      value: { writeText: async (value: string) => sessionStorage.setItem('copiedTrack', value) },
    });
  });
  await materialButton(page, 'Скопировать номер').click();
  await expect(page.getByRole('status')).toContainText('Номер скопирован');
  expect(await page.evaluate(() => sessionStorage.getItem('copiedTrack'))).toBe(track);

  const downloadPromise = page.waitForEvent('download');
  await materialButton(page, 'Сохранить в файл').click();
  const download = await downloadPromise;
  expect(download.suggestedFilename()).toBe('otklik-track.txt');
});

test('C2: an adult can choose a category or say they are unsure', async ({ page }, testInfo) => {
  test.skip(testInfo.project.name !== 'desktop-chromium', 'C2 is the focused desktop acceptance path.');

  await page.goto('/appeal/new');
  await page.locator('.choice-card').filter({ hasText: 'Родитель' }).click();
  await materialButton(page, 'Продолжить').click();
  await expect(page.getByRole('heading', { name: 'Как вам удобнее рассказать?' })).toBeVisible();
  await page.locator('.choice-card').filter({ hasText: 'Выбрать категорию' }).click();
  await materialButton(page, 'Продолжить').click();
  await page.getByRole('button', { name: 'Не знаю, как это назвать' }).click();
  await page.getByRole('textbox', { name: 'Что происходит' }).fill('Не могу точно определить категорию, но ребенку нужна спокойная помощь специалиста.');
  await materialButton(page, 'Отправить обращение').click();
  await expect(page.getByRole('heading', { name: 'Сохраните трек-номер' })).toBeVisible();
});

test('C3–C5: operator, expert and applicant complete a full return cycle through the UI', async ({ page }, testInfo) => {
  test.skip(testInfo.project.name !== 'desktop-chromium', 'The mutating release journey runs once.');
  test.setTimeout(180_000);

  const primary = await createAppeal(page, null,
    'Меня обзывают в школьном чате, сообщения повторяются каждый день и мне нужна помощь.');
  const rejected = await createAppeal(page, null,
    'Повторяющееся рекламное сообщение, созданное для проверки отклонения оператором.');

  await login(page, 'operator', passwords.operator);
  await page.goto(`/staff?appeal=${primary.appealId}`);
  await expect(page.getByRole('heading', { name: 'Первоначальное обращение' })).toBeVisible();
  await expect(page.locator('.suggestion-copy')).toContainText('Подсказка');
  const triageResponsePromise = page.waitForResponse((response) => response.url().endsWith('/triage'));
  await materialButton(page, 'Сохранить разбор').click();
  expect((await triageResponsePromise).ok()).toBeTruthy();
  await expect(page.locator('.appeal-detail__heading .eyebrow')).toContainText('Проверено оператором');
  const operatorDetailResponse = await page.request.get(`/api/staff/operator/queue/${primary.appealId}`);
  const operatorDetail = await operatorDetailResponse.json() as {
    routing: { experts: Array<{ id: string }> };
  };
  const primaryExpertIndex = operatorDetail.routing.experts.findIndex((expert) =>
    expert.id === '10000000-0000-0000-0000-000000000002');
  expect(primaryExpertIndex).toBeGreaterThanOrEqual(0);
  await page.locator('.expert-choice').nth(primaryExpertIndex).click();
  const capacityOverride = page.getByLabel('Назначить сверх лимита вручную');
  if (await capacityOverride.isVisible().catch(() => false)) {
    await capacityOverride.check();
    await page.locator('.capacity-override textarea').fill('Приемочный сценарий с контролируемым превышением demo-лимита.');
  }
  const assignResponsePromise = page.waitForResponse((response) => response.url().endsWith('/assign'));
  await materialButton(page, 'Назначить специалиста').click();
  expect((await assignResponsePromise).ok()).toBeTruthy();
  await expect(page.getByRole('heading', { name: 'Выберите обращение в очереди' })).toBeVisible();

  await page.goto(`/staff?appeal=${rejected.appealId}`);
  await expect(page.getByRole('heading', { name: 'Первоначальное обращение' })).toBeVisible();
  await page.getByRole('tab', { name: 'Отклонить' }).click();
  await page.locator('.operator-decision textarea').fill('Демонстрационная проверка причины спама.');
  await materialButton(page, 'Отклонить обращение').click();
  await expect(page.getByRole('status')).toContainText('Обращение отклонено');

  await logout(page);
  await login(page, 'expert', passwords.expert);
  await page.goto(`/staff?appeal=${primary.appealId}`);
  await expect(page.getByRole('heading', { name: 'Первоначальное обращение' })).toBeVisible();
  await materialButton(page, 'Взять в работу').click();
  await expect(materialButton(page, 'Задать вопрос')).toBeVisible();

  await page.locator('.expert-notes textarea').fill('Заявитель описал повторяющееся давление; уточняем безопасный следующий шаг.');
  await materialButton(page, 'Сохранить заметку').click();
  await expect(page.locator('.expert-note').last()).toContainText('уточняем безопасный следующий шаг');

  await page.locator('.workflow-request textarea').fill('Нужна консультация второго специалиста для совместной рекомендации.');
  await materialButton(page, 'Отправить оператору').click();
  await expect(page.locator('.workflow-history')).toContainText('Ожидает решения');

  await page.locator('.expert-chat textarea').fill('Когда это произошло в последний раз?');
  await materialButton(page, 'Задать вопрос').click();
  await expect(page.locator('.expert-chat')).toContainText('Ждем ответа заявителя');

  await logout(page);
  await login(page, 'operator', passwords.operator);
  await page.getByRole('button', { name: 'Запросы' }).click();
  await expect(page.getByRole('heading', { name: 'Запросы экспертов' })).toBeVisible();
  const requestsResponse = await page.request.get('/api/staff/operator/collaboration/requests');
  const requests = await requestsResponse.json() as { items: Array<{ appealId: string }> };
  const requestIndex = requests.items.findIndex((item) => item.appealId === primary.appealId);
  expect(requestIndex).toBeGreaterThanOrEqual(0);
  await page.locator('.queue-list .queue-row').nth(requestIndex).click();
  await page.locator('.collaboration-decision select').selectOption({ index: 1 });
  await materialButton(page, 'Подтвердить').click();
  await expect(page.locator('.collaboration-decision')).toContainText('Решение уже сохранено');

  await logout(page);
  await login(page, 'expert', passwords.expert);
  await openStatus(page, primary.trackNumber);
  await expect(page.locator('.public-chat')).toContainText('Когда это произошло');
  await page.getByLabel('Ваш ответ специалисту').fill('Вчера после уроков, переписка у меня сохранена.');
  await materialButton(page, 'Отправить ответ').click();
  await expect(page.getByRole('heading', { name: 'Специалист работает с обращением' })).toBeVisible();

  await page.goto(`/staff?appeal=${primary.appealId}`);
  const recommendation = page.locator('.expert-recommendations textarea');
  await recommendation.fill('Сохраните сообщения, обратитесь к взрослому, которому доверяете, и заранее договоритесь о безопасном способе связи.');
  await expect(materialButton(page, 'Опубликовать рекомендацию')).toBeEnabled();
  await materialButton(page, 'Опубликовать рекомендацию').click();
  await expect(page.locator('.expert-recommendation').last()).toContainText('Сохраните сообщения');

  await openStatus(page, primary.trackNumber);
  await expect(page.getByRole('heading', { name: 'Помог ли ответ?' })).toBeVisible();
  await materialButton(page, 'Это не помогло').click();
  await page.getByLabel(/Комментарий/u).last().fill('Нужна дополнительная помощь и более конкретный следующий шаг.');
  await materialButton(page, 'Вернуть оператору').click();
  await expect(page.getByRole('heading', { name: 'Обращение вернулось оператору' })).toBeVisible();

  await page.goto('/staff');
  await logout(page);
  await login(page, 'operator', passwords.operator);
  await page.getByRole('button', { name: 'Возвраты' }).click();
  await expect(page.getByRole('heading', { name: 'Возвраты и жалобы' })).toBeVisible();
  const lifecycleResponse = await page.request.get('/api/staff/operator/lifecycle');
  const lifecycle = await lifecycleResponse.json() as { returns: Array<{ id: string }> };
  const returnIndex = lifecycle.returns.findIndex((item) => item.id === primary.appealId);
  expect(returnIndex).toBeGreaterThanOrEqual(0);
  await page.locator('[aria-label="Возвращенные обращения"] .queue-row').nth(returnIndex).click();
  const returnDetailResponse = await page.request.get(`/api/staff/operator/lifecycle/returns/${primary.appealId}`);
  const returnDetail = await returnDetailResponse.json() as { candidates: Array<{ id: string }> };
  const primaryExpert = returnDetail.candidates.find((candidate) =>
    candidate.id === '10000000-0000-0000-0000-000000000002');
  expect(primaryExpert).toBeTruthy();
  await page.locator('.operator-actions select').selectOption(primaryExpert!.id);
  const reassignResponsePromise = page.waitForResponse((response) =>
    response.url().endsWith(`/lifecycle/returns/${primary.appealId}/reassign`));
  await materialButton(page, 'Назначить повторно').click();
  expect((await reassignResponsePromise).ok()).toBeTruthy();
  await expect(page.getByRole('heading', { name: 'Выберите возврат' })).toBeVisible();

  await logout(page);
  await login(page, 'expert', passwords.expert);
  await page.goto(`/staff?appeal=${primary.appealId}`);
  const acceptAgainResponsePromise = page.waitForResponse((response) => response.url().endsWith('/accept'));
  await materialButton(page, 'Взять в работу').click();
  expect((await acceptAgainResponsePromise).ok()).toBeTruthy();
  await page.locator('.expert-recommendations textarea').fill(
    'Попросите доверенного взрослого вместе зафиксировать сообщения и согласовать разговор со школой в безопасном формате.',
  );
  const recommendationAgainResponsePromise = page.waitForResponse((response) => response.url().endsWith('/recommendations'));
  await materialButton(page, 'Опубликовать рекомендацию').click();
  expect((await recommendationAgainResponsePromise).ok()).toBeTruthy();

  await openStatus(page, primary.trackNumber);
  await materialButton(page, 'Это помогло').click();
  await expect(page.getByRole('heading', { name: 'Обращение закрыто' })).toBeVisible();
  await page.getByLabel('Комментарий (необязательно)').fill('Теперь понятен следующий шаг.');
  await materialButton(page, 'Отправить оценку').click();
  await expect(page.getByText('Спасибо, оценка сохранена.')).toBeVisible();
});

test('C4–C6: seeded dialogue, return and crisis paths are visible without leaking notes', async ({ page }) => {
  await openStatus(page, 'ОТК-ASKD-5678');
  await expect(page.getByRole('heading', { name: 'Нужно уточнение' })).toBeVisible();
  await expect(page.locator('.public-chat')).toContainText('когда ситуация повторилась');
  await expect(page.getByLabel('Ваш ответ специалисту')).toBeVisible();
  await expect(page.locator('body')).not.toContainText('Внутренняя заметка специалиста');

  await openStatus(page, 'ОТК-RSPN-6789');
  await expect(page.getByRole('heading', { name: 'Подготовлена рекомендация' })).toBeVisible();
  await expect(page.getByRole('heading', { name: 'Помог ли ответ?' })).toBeVisible();

  await openStatus(page, 'ОТК-RUSH-DEFG');
  await expect(page.getByRole('heading', { name: 'Если опасность рядом' })).toBeVisible();
  await expect(page.locator('.crisis-help')).toContainText('8-800-2000-122');
  await expect(page.locator('.crisis-help')).toContainText('124');

  await login(page, 'operator', passwords.operator);
  await page.getByRole('button', { name: 'Срочная помощь' }).click();
  await expect(page.getByRole('heading', { name: 'Срочная помощь' })).toBeVisible();
  await expect(page.locator('.crisis-queue-list')).toContainText('Срочность еще не подтверждена');
  await expect(page.locator('.crisis-queue-list')).toContainText(/\d+ мин/u);
});

test('C7–C8: administrator changes safe configuration and downloads anonymous reports', async ({ page }, testInfo) => {
  test.skip(testInfo.project.name !== 'desktop-chromium', 'The mutating administration journey runs once.');
  test.setTimeout(90_000);

  await login(page, 'administrator', passwords.administrator);
  await expect(page.getByRole('heading', { name: 'Конфигурация маршрутизации' })).toBeVisible();
  await expect(page.locator('.admin-privacy-boundary')).toContainText('Тексты обращений, чат, заметки');

  const suffix = Date.now().toString(36);
  const categoryName = `Поддержка ${suffix}`;
  const categorySection = page.locator('.admin-section').filter({ has: page.getByRole('heading', { name: 'Категории', exact: true }) });
  await categorySection.getByLabel('Код').fill(`support-${suffix}`);
  await categorySection.getByLabel('Название').fill(categoryName);
  await materialButton(categorySection, 'Добавить категорию').click();
  await expect(page.getByRole('status')).toContainText('Категория добавлена');

  const groupName = `Группа ${suffix}`;
  const groupSection = page.locator('.admin-section').filter({ has: page.getByRole('heading', { name: 'Группы экспертов' }) });
  await groupSection.getByLabel('Код').fill(`group-${suffix}`);
  await groupSection.getByLabel('Название').fill(groupName);
  await groupSection.locator('input[type="checkbox"]').first().check();
  await materialButton(groupSection, 'Создать группу').click();
  await expect(page.getByRole('status')).toContainText('Группа экспертов создана');

  const ruleSection = page.locator('.admin-section').filter({ has: page.getByRole('heading', { name: 'Правила маршрутизации' }) });
  await ruleSection.getByLabel('Категория').selectOption({ label: categoryName });
  await ruleSection.getByLabel('Группа').selectOption({ label: groupName });
  await materialButton(ruleSection, 'Сохранить правило').click();
  await expect(page.getByRole('status')).toContainText('Правило маршрутизации создано');

  await page.getByRole('button', { name: 'Сотрудники' }).click();
  await expect(page.getByRole('heading', { name: 'Сотрудники и доступ' })).toBeVisible();
  await page.getByLabel('Логин').fill(`expert-${suffix}`);
  await page.getByLabel('Имя в кабинете').fill(`Демо эксперт ${suffix}`);
  await page.getByLabel('Временный пароль').fill('Temporary!2026');
  await materialButton(page, 'Создать сотрудника').click();
  await expect(page.getByRole('status')).toContainText('Учетная запись создана');

  await page.getByRole('button', { name: 'Зависшие' }).click();
  await expect(page.getByRole('heading', { name: 'Зависшие обращения' })).toBeVisible();
  await page.locator('.admin-stuck-list .admin-row').first().click();
  await page.getByLabel('Причина изменения').fill('Демонстрационное разблокирование зависшего обращения.');
  await materialButton(page, 'Сохранить изменение').click();

  await page.getByRole('button', { name: 'Журнал' }).click();
  await expect(page.getByRole('heading', { name: 'Журнал действий' })).toBeVisible();
  await expect(page.locator('.admin-summary')).not.toContainText('0 событий');

  await page.getByRole('button', { name: 'Аналитика' }).click();
  await expect(page.getByRole('heading', { name: 'Аналитика' })).toBeVisible();
  await expect(page.locator('.analytics-workspace')).toContainText('Тексты, чат, заметки, файлы');
  await page.getByLabel('Период').selectOption('365');

  const csvDownloadPromise = page.waitForResponse((response) =>
    response.request().method() === 'POST' && response.url().includes('/api/staff/analytics/exports/'));
  await materialButton(page, 'Скачать CSV').click();
  const csvDownload = await csvDownloadPromise;
  expect(csvDownload.ok(), `${csvDownload.status()} ${await csvDownload.text()}`).toBeTruthy();
  expect(csvDownload.headers()['content-disposition']).toContain('.csv');
  await expect(page.getByRole('status')).toContainText('Выгрузка подготовлена');

  const xlsxDownloadPromise = page.waitForResponse((response) =>
    response.request().method() === 'POST' && response.url().includes('/api/staff/analytics/exports/'));
  await materialButton(page, 'Скачать XLSX').click();
  const xlsxDownload = await xlsxDownloadPromise;
  expect(xlsxDownload.ok(), `${xlsxDownload.status()} ${await xlsxDownload.text()}`).toBeTruthy();
  expect(xlsxDownload.headers()['content-disposition']).toContain('.xlsx');
});

async function createAppeal(page: Page, categoryId: string | null, narrative: string) {
  const response = await page.request.post('/api/public/appeals', {
    data: {
      clientRequestId: crypto.randomUUID(),
      applicantType: 'Student',
      submissionPath: categoryId ? 'Category' : 'FreeText',
      categoryId,
      narrative,
      answers: { place: 'В школе', frequency: 'Каждый день' },
      crisisContact: null,
    },
  });
  expect(response.ok(), await response.text()).toBeTruthy();
  return response.json() as Promise<{ appealId: string; trackNumber: string }>;
}

async function login(page: Page, userName: string, password: string) {
  await page.goto('/staff');
  if (await materialButton(page, 'Выйти').isVisible().catch(() => false)) await logout(page);
  await expect(page.getByRole('heading', { name: 'Вход для сотрудников' })).toBeVisible();
  await page.getByLabel('Логин').fill(userName);
  await page.getByLabel('Пароль').fill(password);
  await materialButton(page, 'Войти').click();
  await expect(materialButton(page, 'Выйти')).toBeVisible();
}

async function logout(page: Page) {
  await page.goto('/staff');
  await materialButton(page, 'Выйти').click();
  await expect(page.getByRole('heading', { name: 'Вход для сотрудников' })).toBeVisible();
}

async function openStatus(page: Page, trackNumber: string) {
  await page.goto('/appeal/status');
  await expect(page.getByText('Проверяем, сохранен ли безопасный доступ на этом устройстве…')).toBeHidden();
  const openAnother = materialButton(page, 'Открыть другое обращение');
  if (await openAnother.isVisible().catch(() => false)) {
    await openAnother.click();
  }
  const input = page.getByRole('textbox', { name: 'Трек-номер' });
  await expect(input).toBeVisible();
  await input.fill(trackNumber);
  await materialButton(page, 'Показать статус').click();
  await expect(page.locator('.status-result')).toBeVisible();
}

function materialButton(scope: Page | ReturnType<Page['locator']>, text: string) {
  return scope.locator('md-filled-button, md-outlined-button, md-text-button').filter({ hasText: text }).last();
}

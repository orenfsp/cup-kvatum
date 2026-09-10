import { expect, test, type APIResponse, type Page } from '@playwright/test';

const administratorPassword = process.env.OTKLIK_ADMINISTRATOR_PASSWORD ?? 'AdminPanel!2026';

test('administrator follows configuration, access, stuck and audit workflows without private content', async ({ page }, testInfo) => {
  test.skip(testInfo.project.name !== 'desktop-chromium', 'The mutating administration journey runs once on desktop.');
  test.setTimeout(240_000);
  const suffix = crypto.randomUUID().slice(0, 8);
  const categoryName = `Поддержка UX ${suffix}`;
  const groupName = `Команда UX ${suffix}`;
  const userName = `expert.ux.${suffix}`;
  const temporaryPassword = 'TemporaryHelp!2026';
  const privateNarrative = `Приватный текст ${suffix}: мне угрожают убить прямо сейчас, я прошу срочной помощи.`;

  await login(page);
  await page.goto('/staff/admin');
  await expect(page.getByRole('heading', { name: 'Задачи настройки и контроля' })).toBeFocused();
  await expect(page.getByRole('link', { name: 'Открыть аналитику' })).toBeVisible();
  await expect(page.getByText(/Тексты обращений, чат, заметки/)).toBeVisible();

  const configuration = await getJson<AdminConfiguration>(page, '/api/staff/administrator/configuration');
  const expert = configuration.experts.find((item) => item.isActive && item.isAvailable);
  expect(expert).toBeTruthy();

  await page.goto('/staff/admin/categories/new');
  await expect(page.getByRole('heading', { name: 'Создайте категорию' })).toBeFocused();
  await page.getByLabel('Название для заявителя').fill(categoryName);
  await page.getByLabel('Порядок в форме').fill('91');
  await materialButton(page, 'Создать категорию').click();
  await expect(page.getByRole('status')).toContainText('Категория создана');
  const categoryId = pathId(page.url());
  await page.getByRole('link', { name: 'Создать группу экспертов' }).click();
  await expect.poll(() => new URL(page.url()).pathname + new URL(page.url()).search).toBe(`/staff/admin/expert-groups/new?categoryId=${categoryId}`);

  await page.getByLabel('Название группы').fill(groupName);
  await page.getByLabel('Активных обращений на эксперта').fill('4');
  await page.locator('.admin-members label').filter({ hasText: expert!.displayName }).getByRole('checkbox').check();
  await materialButton(page, 'Создать группу').click();
  await expect(page.getByRole('status')).toContainText('Группа создана');
  const groupId = pathId(page.url());
  await page.getByRole('link', { name: 'Создать правило маршрутизации' }).click();
  await expect(page.getByLabel('Категория')).toHaveValue(categoryId);
  await expect(page.getByLabel('Профильная группа')).toHaveValue(groupId);
  await materialButton(page, 'Сохранить правило').click();
  await expect(page.getByRole('status')).toContainText('Правило создано');
  await page.getByRole('link', { name: `Категория: ${categoryName}` }).click();
  await expect(page.getByRole('heading', { name: categoryName })).toBeFocused();
  await page.getByRole('link', { name: new RegExp(groupName, 'u') }).click();
  await expect(page.getByRole('heading', { name: categoryName })).toBeFocused();
  await page.getByRole('link', { name: `Группа: ${groupName}`, exact: true }).click();
  await expect(page.getByRole('heading', { name: groupName })).toBeFocused();

  await page.goto('/staff/admin/users/new');
  await page.getByLabel('Логин').fill(userName);
  await page.getByLabel('Имя в кабинете').fill(`Эксперт UX ${suffix}`);
  await page.getByLabel('Временный пароль').fill(temporaryPassword);
  await materialButton(page, 'Создать сотрудника').click();
  await expect(page.getByRole('heading', { name: 'Передайте временный пароль сотруднику' })).toBeFocused();
  await expect(page.getByText(temporaryPassword, { exact: true })).toBeVisible();
  await expect(page.getByRole('button', { name: 'Скопировать пароль' })).toBeVisible();
  await page.getByRole('link', { name: 'Готово, вернуться к списку' }).click();
  await page.getByRole('link', { name: new RegExp(`Эксперт UX ${suffix}`, 'u') }).click();
  await materialButton(page, 'Изменить').click();
  await page.getByLabel('Имя в кабинете').fill(`Эксперт UX обновлён ${suffix}`);
  await materialButton(page, 'Сохранить изменения').click();
  await expect(page.getByRole('status')).toContainText('Учётная запись обновлена');
  await materialButton(page, 'Заблокировать доступ').click();
  const accessDialog = page.getByRole('dialog', { name: 'Заблокировать доступ?' });
  await expect(accessDialog).toContainText('все активные сессии будут отозваны');
  await accessDialog.getByLabel('Причина блокировки').fill('Завершение демонстрационной учетной записи.');
  await materialButton(page, 'Заблокировать и отозвать сессии').click();
  await expect(page.getByRole('status')).toContainText('активные сессии отозваны');

  const appealResponse = await page.request.post('/api/public/appeals', { data: { clientRequestId: crypto.randomUUID(), applicantType: 'Student', submissionPath: 'FreeText', categoryId: null, narrative: privateNarrative, answers: {}, crisisContact: null } });
  await expectOk(appealResponse);
  const { appealId } = await appealResponse.json() as { appealId: string };
  await expect.poll(async () => {
    const result = await getJson<{ items: Array<{ id: string }> }>(page, '/api/staff/administrator/stuck');
    return result.items.some((item) => item.id === appealId);
  }, { timeout: 30_000 }).toBeTruthy();

  await page.goto(`/staff/admin/stuck/${appealId}`);
  await expect(page.getByRole('heading', { name: /Категория|Не знаю|Помощь/u })).toBeFocused();
  await expect(page.getByText(privateNarrative, { exact: false })).toHaveCount(0);
  const interventionForm = page.locator('.admin-intervention form');
  const priorityBefore = await interventionForm.getByLabel('Приоритет').inputValue();
  await interventionForm.getByLabel('Приоритет').selectOption(priorityBefore === 'Low' ? 'Standard' : 'Low');
  await interventionForm.getByLabel('Причина изменения').fill('Возврат обращения в рабочую очередь после проверки сигнала.');
  await materialButton(page, 'Проверить изменение').click();
  await expect(page.getByRole('heading', { name: 'Проверьте изменение' })).toBeVisible();
  await expect(page.getByRole('heading', { name: 'Было' })).toBeVisible();
  await expect(page.getByRole('heading', { name: 'Станет' })).toBeVisible();
  await materialButton(page, 'Подтвердить и сохранить').click();
  await expect(page.getByRole('heading', { name: 'Обращение возвращено в рабочий процесс' })).toBeFocused();
  await page.getByRole('link', { name: 'Открыть событие в журнале' }).click();
  await expect(page.getByRole('heading', { name: 'Зависшее обращение разблокировано' })).toBeFocused();
  await expect(page.getByText('Возврат обращения в рабочую очередь', { exact: false })).toBeVisible();
  await expect(page.locator('pre')).toHaveCount(0);
  await expect(page.getByText(privateNarrative, { exact: false })).toHaveCount(0);

  await page.getByRole('link', { name: 'Аналитика' }).click();
  await expect(page.getByRole('heading', { name: 'Аналитика' })).toBeFocused();
  const downloadPromise = page.waitForEvent('download');
  await materialButton(page, 'Скачать CSV').click();
  const download = await downloadPromise;
  expect(download.suggestedFilename()).toMatch(/\.csv$/u);
});

test('administrator can start a new group or rule after viewing an existing record', async ({ page }, testInfo) => {
  test.skip(testInfo.project.name !== 'desktop-chromium', 'The route-transition regression runs once on desktop.');
  const pageErrors: string[] = [];
  page.on('pageerror', (error) => pageErrors.push(error.message));

  await login(page);
  const configuration = await getJson<AdminConfiguration>(page, '/api/staff/administrator/configuration');
  expect(configuration.groups.length).toBeGreaterThan(0);
  expect(configuration.rules.length).toBeGreaterThan(0);

  await page.goto(`/staff/admin/expert-groups/${configuration.groups[0].id}`);
  await page.getByRole('link', { name: 'Создать группу' }).click();
  await expect(page.getByRole('heading', { name: 'Создайте профильную группу' })).toBeVisible();

  await page.goto(`/staff/admin/routing-rules/${configuration.rules[0].id}`);
  await page.getByRole('link', { name: 'Создать правило' }).click();
  await expect(page.getByRole('heading', { name: 'Свяжите категорию и группу' })).toBeVisible();
  expect(pageErrors).toEqual([]);
});

test('mobile administrator list and detail are separate at 320 px and keep URL filters', async ({ page }, testInfo) => {
  test.skip(testInfo.project.name !== 'mobile-chromium', 'The mobile transition is verified in the mobile project.');
  await page.setViewportSize({ width: 320, height: 800 });
  await login(page);
  await page.goto('/staff/admin/users?role=Expert&state=active');
  await expect(page.getByLabel('Роль')).toHaveValue('Expert');
  await expect(page.locator('.admin-filter-row label').filter({ hasText: 'Доступ' }).locator('select')).toHaveValue('active');
  await page.locator('.admin-row--link').first().click();
  await expect(page).toHaveURL(/\/staff\/admin\/users\/[^?]+\?role=Expert&state=active$/u);
  await expect(page.getByRole('link', { name: '← К списку сотрудников' })).toBeVisible();
  await expect(page.getByRole('region', { name: 'Сотрудники и доступ' })).toBeHidden();
  expect(await page.evaluate(() => document.documentElement.scrollWidth)).toBe(await page.evaluate(() => document.documentElement.clientWidth));
  await page.getByRole('link', { name: '← К списку сотрудников' }).click();
  await expect(page).toHaveURL(/\/staff\/admin\/users\?role=Expert&state=active$/u);
});

type AdminConfiguration = {
  experts: Array<{ id: string; displayName: string; isActive: boolean; isAvailable: boolean }>;
  groups: Array<{ id: string }>;
  rules: Array<{ id: string }>;
};
function pathId(url: string) { return new URL(url).pathname.split('/').filter(Boolean).at(-1)!; }
async function getJson<T>(page: Page, route: string) { const response = await page.request.get(route); await expectOk(response); return response.json() as Promise<T>; }
async function expectOk(response: APIResponse) { expect(response.ok(), `${response.status()} ${await response.text()}`).toBeTruthy(); }
function materialButton(page: Page, text: string) { return page.locator('md-filled-button, md-outlined-button, md-text-button').filter({ hasText: text }).last(); }
async function login(page: Page) {
  await page.goto('/staff');
  if (await materialButton(page, 'Выйти').isVisible().catch(() => false)) {
    const csrfResponse = await page.request.get('/api/staff/auth/csrf'); await expectOk(csrfResponse); const { token } = await csrfResponse.json() as { token: string };
    await expectOk(await page.request.post('/api/staff/auth/logout', { headers: { 'X-CSRF-TOKEN': token } })); await page.goto('/staff');
  }
  await page.getByLabel('Логин').fill('administrator'); await page.getByLabel('Пароль').fill(administratorPassword); await materialButton(page, 'Войти').click(); await expect(materialButton(page, 'Выйти')).toBeVisible();
}

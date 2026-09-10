import { expect, test, type APIResponse, type Page } from '@playwright/test';

const operatorPassword = process.env.OTKLIK_OPERATOR_PASSWORD ?? 'Operator!2026';
const expertPassword = process.env.OTKLIK_EXPERT_PASSWORD ?? 'ExpertHelp!2026';
const primaryExpertId = '10000000-0000-0000-0000-000000000002';
const mediatorExpertId = '10000000-0000-0000-0000-000000000004';
const conflictCategoryId = '20000000-0000-0000-0000-000000000002';

test('responsible expert moves through focused workspaces and publishes from preview', async ({ page }, testInfo) => {
  test.skip(testInfo.project.name !== 'desktop-chromium', 'The mutating expert journey runs once on desktop.');
  test.setTimeout(60_000);
  const created = await prepareAssignedAppeal(page, primaryExpertId,
    'Нужна помощь с повторяющимся конфликтом и безопасным разговором в школе.');
  await login(page, 'expert', expertPassword);

  await page.goto('/staff/expert/inbox?priority=Standard');
  await page.locator(`[data-appeal-id="${created.appealId}"]`).click();
  await expect(page).toHaveURL(new RegExp(`/staff/expert/cases/${created.appealId}/overview\\?.*priority=Standard`, 'u'));
  await expect(page.getByRole('heading', { name: 'Обзор обращения' })).toBeFocused();
  await expect.poll(() => page.evaluate(() => window.scrollY)).toBe(0);
  await expect(page.getByText('Ответственный · Медиация конфликтов', { exact: true })).toBeVisible();
  await expect(page.getByText('Форма с категорией, цикл 1', { exact: true })).toBeVisible();
  await expect(page.locator('.expert-case-detail').getByText('Взять обращение в работу', { exact: true })).toBeVisible();
  await expect(materialButton(page, 'Взять в работу')).toHaveCount(1);
  await materialButton(page, 'Взять в работу').click();
  await expect(page).toHaveURL(new RegExp(`/staff/expert/cases/${created.appealId}/overview\\?.*from=active`, 'u'));
  await expect(page.getByRole('link', { name: /В работе/u }).locator('[data-navigation-count]')).toBeVisible();
  await expect(page.locator('.expert-case-detail').getByText('Ответить заявителю или подготовить итог', { exact: true })).toBeVisible();

  await page.getByRole('link', { name: 'Заметки', exact: true }).click();
  await expect(page.getByRole('heading', { name: 'Внутренние заметки' })).toBeFocused();
  await expect(page.getByText(
    'Видят только разрешённые участники обращения. Не видят заявитель, оператор и администратор.',
    { exact: true },
  )).toBeVisible();
  const note = page.getByLabel('Новая внутренняя заметка');
  await note.fill('Нужно уточнить безопасный формат разговора с участниками конфликта.');
  page.once('dialog', async (dialog) => {
    expect(dialog.message()).toContain('не сохранены');
    await dialog.dismiss();
  });
  await page.getByRole('link', { name: 'Диалог', exact: true }).click();
  await expect(page).toHaveURL(new RegExp(`/${created.appealId}/notes`, 'u'));
  await materialButton(page, 'Сохранить заметку').click();
  await expect(page.locator('.expert-note').last()).toContainText('безопасный формат разговора');

  await page.getByRole('link', { name: 'Диалог', exact: true }).click();
  await expect(page.getByRole('heading', { name: 'Диалог' })).toBeFocused();
  await expect(page.getByText('Заявитель увидит это сообщение.', { exact: false })).toBeVisible();
  await expect(page.getByText('Ваше имя не показывается', { exact: false })).toBeVisible();
  const activeBeforeQuestion = await navigationCount(page, 'В работе');
  const waitingBeforeQuestion = await navigationCount(page, 'Ждут заявителя');
  await page.getByLabel('Сообщение заявителю').fill('Подскажите, рядом есть взрослый, которому вы доверяете?');
  await materialButton(page, 'Отправить сообщение').click();
  await expect(page).toHaveURL(new RegExp(`/${created.appealId}/dialog\\?.*from=waiting`, 'u'));
  await expect.poll(() => navigationCount(page, 'В работе')).toBe(activeBeforeQuestion - 1);
  await expect.poll(() => navigationCount(page, 'Ждут заявителя')).toBe(waitingBeforeQuestion + 1);
  await expect(page.getByText('Ждём ответа заявителя.', { exact: false })).toBeVisible();
  await expect(page.getByLabel('Сообщение заявителю')).toHaveCount(0);

  await publicPost(page, '/api/public/appeals/messages', {
    trackNumber: created.trackNumber,
    clientMessageId: crypto.randomUUID(),
    body: 'Да, рядом со мной сейчас классный руководитель.',
  });
  await expect(page).toHaveURL(new RegExp(`/${created.appealId}/dialog\\?.*from=active`, 'u'));
  await expect.poll(() => navigationCount(page, 'В работе')).toBe(activeBeforeQuestion);
  await expect.poll(() => navigationCount(page, 'Ждут заявителя')).toBe(waitingBeforeQuestion);
  await expect(page.locator('.action-receipt')).toContainText('Заявитель ответил');
  await expect(page.getByLabel('Сообщение заявителю')).toBeVisible();
  await expect(page.locator('.expert-messages')).toContainText('классный руководитель');

  await page.getByRole('link', { name: 'Ответ', exact: true }).click();
  await expect(page.getByRole('heading', { name: 'Ответ' })).toBeFocused();
  const answer = page.getByLabel('Новый ответ');
  const answerText = 'Обсудите ситуацию с доверенным взрослым и вместе согласуйте безопасный разговор со школой.';
  await answer.fill(answerText);
  await materialButton(page, 'Проверить перед публикацией').click();
  await expect(page.getByText('Так увидит заявитель', { exact: true })).toBeVisible();
  await expect(page.locator('.answer-preview')).toContainText(answerText);
  await materialButton(page, 'Вернуться к редактированию').click();
  await expect(answer).toHaveValue(answerText);
  await materialButton(page, 'Проверить перед публикацией').click();
  await materialButton(page, 'Опубликовать ответ').click();
  await expect(page.getByText('Ответ опубликован и сохранён в истории.', { exact: false })).toBeVisible();
  await expect(page.locator('.expert-recommendation').last()).toContainText('доверенным взрослым');

  await page.getByRole('link', { name: 'Завершённые', exact: true }).click();
  await expect(page).toHaveURL(/\/staff\/expert\/completed$/u);
  await expect(page.getByLabel('Статус')).toHaveValue('Completed');
  await expect(page.locator(`[data-appeal-id="${created.appealId}"]`)).toBeVisible();
  const current = await getJson<{ version: number }>(page, `/api/staff/expert/appeals/${created.appealId}`);
  await publicPost(page, '/api/public/appeals/outcomes/helped', {
    trackNumber: created.trackNumber,
    clientActionId: crypto.randomUUID(),
    expectedVersion: current.version,
  });
  await page.locator(`[data-appeal-id="${created.appealId}"]`).click();
  await expect(page.locator('.expert-case-facts').getByText('Завершено', { exact: true })).toBeVisible();
  await page.getByRole('link', { name: 'Заметки', exact: true }).click();
  await expect(page.getByLabel('Новая внутренняя заметка')).toHaveCount(0);
  await expect(page.getByText('Заметки доступны только для просмотра.', { exact: false })).toBeVisible();
});

test('mediation and coexecution share one shell while responsibility stays explicit', async ({ page }, testInfo) => {
  test.skip(testInfo.project.name !== 'desktop-chromium', 'The collaboration journey runs once on desktop.');
  test.setTimeout(180_000);
  const created = await prepareAssignedAppeal(page, mediatorExpertId,
    'Для затянувшегося конфликта нужна совместная работа медиатора и второго специалиста.');
  await login(page, 'expert.mediator', expertPassword);
  await page.goto(`/staff/expert/cases/${created.appealId}/overview?from=inbox`);
  await materialButton(page, 'Взять в работу').click();
  await page.getByRole('link', { name: 'Команда', exact: true }).click();
  await expect(page.getByText('Ответственный · Медиация конфликтов', { exact: true })).toBeVisible();
  const reason = 'Нужен второй специалист, чтобы проверить безопасный план совместной встречи.';
  await page.getByLabel('Эту причину увидит оператор').fill(reason);
  await materialButton(page, 'Отправить запрос').click();
  await expect(page.locator('.workflow-history')).toContainText('Ожидает решения');

  const expertDetail = await getJson<ExpertDetail>(page, `/api/staff/expert/appeals/${created.appealId}`);
  const workflow = expertDetail.workflowRequests.find((request) => request.type === 'CoExecutor');
  expect(workflow).toBeTruthy();
  await login(page, 'operator', operatorPassword);
  const operatorRequest = await getJson<{ version: number; reason: string }>(
    page,
    `/api/staff/operator/collaboration/requests/${workflow!.id}`,
  );
  expect(operatorRequest.reason).toBe(reason);
  await staffPost(page, `/api/staff/operator/collaboration/requests/${workflow!.id}/approve`, {
    expertId: primaryExpertId,
    keepPreviousAsCoExecutor: false,
    decisionReason: 'Подключаем профильного специалиста к совместной работе.',
    expectedVersion: operatorRequest.version,
  });

  await login(page, 'expert', expertPassword);
  await page.goto(`/staff/expert/cases/${created.appealId}/overview?from=active`);
  await expect(page.getByText('Соисполнитель · Медиация конфликтов', { exact: true })).toBeVisible();
  await page.getByRole('link', { name: 'Ответ', exact: true }).click();
  await expect(page.getByText('Опубликовать итог может только ответственный специалист.', { exact: false })).toBeVisible();
  await expect(page.getByLabel('Новый ответ')).toHaveCount(0);
  await page.getByRole('link', { name: 'Заметки', exact: true }).click();
  await expect(page.getByLabel('Новая внутренняя заметка')).toBeDisabled();
  await materialButton(page, 'Начать редактирование').click();
  await expect(page.getByLabel('Новая внутренняя заметка')).toBeEnabled();
  await page.getByLabel('Новая внутренняя заметка').fill('Предложение соисполнителя сохранено только для рабочей команды.');
  await materialButton(page, 'Сохранить заметку').click();
  await expect(page.locator('.expert-note').last()).toContainText('Предложение соисполнителя');
  await page.getByRole('link', { name: 'Команда', exact: true }).click();
  await expect(page.getByRole('heading', { name: 'Команда' })).toBeFocused();
  await expect(page.locator('.participant-list')).toContainText('Соисполнитель');
  await expect(page.locator('.workflow-history')).toContainText(reason);
});

test('mobile expert detail is a separate navigable screen without horizontal overflow', async ({ page }, testInfo) => {
  test.skip(testInfo.project.name !== 'mobile-chromium', 'The mobile transition is verified in the mobile project.');
  await page.setViewportSize({ width: 320, height: 800 });
  const created = await prepareAssignedAppeal(page, primaryExpertId,
    'Мобильная проверка пяти рабочих разделов карточки эксперта.');
  await login(page, 'expert', expertPassword);
  await page.goto(`/staff/expert/cases/${created.appealId}/overview?from=inbox&priority=Standard`);
  await expect(page.getByRole('heading', { name: 'Новые назначения' })).toBeHidden();
  await expect(page.getByRole('button', { name: 'Вернуться в «Новые назначения»' })).toBeVisible();
  await page.getByRole('link', { name: 'Диалог', exact: true }).click();
  await expect(page).toHaveURL(new RegExp(`/${created.appealId}/dialog\\?from=inbox&priority=Standard`, 'u'));
  await expect(page.getByRole('heading', { name: 'Диалог' })).toBeFocused();
  expect(await page.evaluate(() => document.documentElement.scrollWidth)).toBe(
    await page.evaluate(() => document.documentElement.clientWidth),
  );
  await page.getByRole('button', { name: 'Вернуться в «Новые назначения»' }).click();
  await expect(page).toHaveURL(/\/staff\/expert\/inbox\?priority=Standard$/u);
  await expect(page.getByRole('heading', { name: 'Новые назначения' })).toBeVisible();
});

type CreatedAppeal = { appealId: string; trackNumber: string };
type OperatorDetail = {
  version: number;
  categories: Array<{ id: string }>;
  routing: { experts: Array<{ id: string }> };
};
type ExpertDetail = {
  workflowRequests: Array<{ id: string; type: string }>;
};

async function prepareAssignedAppeal(page: Page, expertId: string, narrative: string) {
  const response = await page.request.post('/api/public/appeals', {
    data: {
      clientRequestId: crypto.randomUUID(),
      applicantType: 'Student',
      submissionPath: 'Category',
      categoryId: conflictCategoryId,
      narrative,
      answers: { place: 'В школе', frequency: 'Регулярно' },
      crisisContact: null,
    },
  });
  await expectOk(response);
  const created = await response.json() as CreatedAppeal;
  await login(page, 'operator', operatorPassword);
  let detail = await getJson<OperatorDetail>(page, `/api/staff/operator/queue/${created.appealId}`);
  await staffPost(page, `/api/staff/operator/queue/${created.appealId}/triage`, {
    categoryId: conflictCategoryId,
    priority: 'Standard',
    expectedVersion: detail.version,
  });
  detail = await getJson<OperatorDetail>(page, `/api/staff/operator/queue/${created.appealId}`);
  expect(detail.routing.experts.some((expert) => expert.id === expertId)).toBeTruthy();
  await staffPost(page, `/api/staff/operator/queue/${created.appealId}/assign`, {
    expertId,
    expectedVersion: detail.version,
    allowOverCapacity: true,
    overrideReason: 'Контролируемое назначение для проверки интерфейса эксперта.',
  });
  return created;
}

async function getJson<T>(page: Page, route: string) {
  const response = await page.request.get(route);
  await expectOk(response);
  return response.json() as Promise<T>;
}

async function staffPost(page: Page, route: string, data: object) {
  const csrfResponse = await page.request.get('/api/staff/auth/csrf');
  await expectOk(csrfResponse);
  const { token } = await csrfResponse.json() as { token: string };
  const response = await page.request.post(route, { data, headers: { 'X-CSRF-TOKEN': token } });
  await expectOk(response);
  return response;
}

async function publicPost(page: Page, route: string, data: object) {
  const response = await page.request.post(route, { data });
  await expectOk(response);
  return response;
}

async function login(page: Page, userName: string, password: string) {
  await page.goto('/staff');
  const logoutButton = materialButton(page, 'Выйти');
  const loginHeading = page.getByRole('heading', { name: 'Вход для сотрудников' });
  await expect(logoutButton.or(loginHeading)).toBeVisible();
  if (await logoutButton.isVisible()) {
    await logoutButton.click();
    await expect(loginHeading).toBeVisible();
  }
  await page.getByLabel('Логин').fill(userName);
  await page.getByLabel('Пароль').fill(password);
  await materialButton(page, 'Войти').click();
  await expect(materialButton(page, 'Выйти')).toBeVisible();
}

async function expectOk(response: APIResponse) {
  expect(response.ok(), `${response.status()} ${await response.text()}`).toBeTruthy();
}

function materialButton(scope: Page, text: string) {
  return scope.locator('md-filled-button, md-outlined-button, md-text-button').filter({ hasText: text }).last();
}

async function navigationCount(page: Page, label: string) {
  const value = await page.getByRole('link', { name: label, exact: true })
    .locator('[data-navigation-count]')
    .textContent();
  return Number.parseInt(value ?? '', 10);
}

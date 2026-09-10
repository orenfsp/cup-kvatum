import { expect, test, type APIResponse, type Page } from '@playwright/test';

const operatorPassword = process.env.OTKLIK_OPERATOR_PASSWORD ?? 'Operator!2026';
const expertPassword = process.env.OTKLIK_EXPERT_PASSWORD ?? 'ExpertHelp!2026';
const primaryExpertId = '10000000-0000-0000-0000-000000000002';

test('operator queue keeps filters, scroll and a sequential triage while explaining conflicts', async ({ page }, testInfo) => {
  test.skip(testInfo.project.name !== 'desktop-chromium', 'The mutating operator journey runs once on desktop.');
  test.setTimeout(180_000);

  const assigned = await createAppeal(page, 'Меня регулярно обзывают в школьном чате, и мне нужна помощь специалиста.');
  const rejected = await createAppeal(page, 'Демонстрационная рекламная запись для проверки отклонения как спама.');
  const conflicted = await createAppeal(page, 'На меня давят одноклассники, оператору нужно проверить сохранение выбора при конфликте.');
  const guarded = await createAppeal(page, 'Проверка защиты несохранённого решения оператора при переходе в другой раздел.');
  await login(page, 'operator', operatorPassword);

  await page.goto('/staff/operator/queue');
  await page.getByLabel('Задача').selectOption('new');
  await page.getByLabel('Приоритет', { exact: true }).selectOption('standard');
  await page.getByText('Дополнительные фильтры', { exact: true }).click();
  await page.getByLabel('Порядок').selectOption('oldest');
  await page.getByLabel('Найти по номеру обращения').fill(caseNumber(assigned.appealId));
  await expect.poll(() => new URL(page.url()).searchParams.get('stage')).toBe('new');
  await expect.poll(() => new URL(page.url()).searchParams.get('priority')).toBe('standard');
  await expect.poll(() => new URL(page.url()).searchParams.get('sort')).toBe('oldest');
  await expect.poll(() => new URL(page.url()).searchParams.get('q')).toBe(caseNumber(assigned.appealId));

  const assignedRow = page.locator(`[data-appeal-id="${assigned.appealId}"]`);
  await assignedRow.click();
  await expect(page).toHaveURL(new RegExp(`/staff/operator/queue/${assigned.appealId}\\?`, 'u'));
  await expect(page.getByRole('heading', { name: 'Провести разбор обращения' })).toBeFocused();
  await expect(page.locator('.operator-step--context')).toContainText('Меня регулярно обзывают');
  await expect(page.locator('.operator-step--active h3').first()).toHaveText('Категория и приоритет');

  await page.goBack();
  await expect(page).toHaveURL(/\/staff\/operator\/queue\?/u);
  await expect(page.getByLabel('Найти по номеру обращения')).toHaveValue(caseNumber(assigned.appealId));
  await page.goForward();

  await materialButton(page, 'Сохранить разбор').click();
  await expect(page.locator('.operator-step--active h3').first()).toHaveText('Ответственный специалист');
  const contextHeadingBox = await page.locator('.operator-step--context h3').boundingBox();
  const appealNarrativeBox = await page.locator('.operator-step--context .appeal-original > p').boundingBox();
  expect(contextHeadingBox).not.toBeNull();
  expect(appealNarrativeBox).not.toBeNull();
  expect(appealNarrativeBox!.y - (contextHeadingBox!.y + contextHeadingBox!.height)).toBeLessThan(120);

  const assignedDetail = await getJson<OperatorDetail>(page, `/api/staff/operator/queue/${assigned.appealId}`);
  const expert = assignedDetail.routing.experts.find((candidate) => candidate.id === primaryExpertId)
    ?? assignedDetail.routing.experts[0];
  expect(expert).toBeTruthy();
  await page.locator('.expert-choice').filter({ hasText: expert.displayName }).click();
  const capacityOverride = page.getByLabel('Назначить сверх лимита вручную');
  if (await capacityOverride.isVisible().catch(() => false)) {
    await capacityOverride.check();
    await page.getByLabel('Причина исключения').fill('123456789');
    await expect(materialButton(page, 'Назначить специалиста')).toHaveAttribute('disabled');
    await page.getByLabel('Причина исключения').fill('Контролируемое назначение в приёмочном сценарии оператора.');
  }
  await materialButton(page, 'Назначить специалиста').click();
  await expect(page.locator('.action-receipt')).toContainText('распределено специалисту');
  await expect(page).not.toHaveURL(new RegExp(assigned.appealId, 'u'));

  await page.goto(`/staff/operator/queue/${rejected.appealId}`);
  await page.getByText('Другие решения', { exact: true }).click();
  await materialButton(page, 'Отклонить обращение').click();
  await page.getByLabel('Внутренняя причина').fill('Демонстрационная запись является спамом.');
  await materialButton(page, 'Подтвердить отклонение').click();
  await expect(page.locator('.action-receipt')).toContainText('отклонено');
  await expect(page).not.toHaveURL(new RegExp(rejected.appealId, 'u'));

  await page.goto(`/staff/operator/queue/${conflicted.appealId}`);
  await page.getByLabel('Приоритет обращения').selectOption('Low');
  const staleDetail = await getJson<OperatorDetail>(page, `/api/staff/operator/queue/${conflicted.appealId}`);
  const categoryId = staleDetail.suggestion?.categoryId ?? staleDetail.categories[0].id;
  const currentLeaseId = await page.evaluate(() => window.sessionStorage.getItem('otklik:operator-work-lease'));
  expect(currentLeaseId).toBeTruthy();
  await staffPost(page, `/api/staff/operator/queue/${conflicted.appealId}/triage`, {
    categoryId,
    priority: 'Standard',
    expectedVersion: staleDetail.version,
    leaseId: currentLeaseId,
  });
  const conflictResponse = page.waitForResponse((response) => response.url().endsWith('/triage') && response.status() === 409);
  await materialButton(page, 'Сохранить разбор').click();
  await conflictResponse;
  await expect(page.getByRole('heading', { name: 'Обращение обновил другой сотрудник' })).toBeVisible();
  await materialButton(page, 'Открыть актуальную версию').click();
  await expect(page.getByLabel('Приоритет обращения')).toHaveValue('Low');
  await materialButton(page, 'Сохранить разбор').click();
  await expect(page.locator('.operator-step--active h3').first()).toHaveText('Ответственный специалист');

  await page.goto(`/staff/operator/queue/${guarded.appealId}`);
  await page.getByLabel('Приоритет обращения').selectOption('Low');
  page.once('dialog', async (dialog) => {
    expect(dialog.message()).toContain('не сохранены');
    await dialog.dismiss();
  });
  await page.getByRole('link', { name: 'Срочная помощь' }).click();
  await expect(page).toHaveURL(new RegExp(guarded.appealId, 'u'));
  page.once('dialog', async (dialog) => dialog.accept());
  await page.getByRole('link', { name: 'Срочная помощь' }).click();
  await expect(page).toHaveURL(/\/staff\/operator\/urgent$/u);
});

test('a claimed appeal is unavailable in another operator window', async ({ page, browser }, testInfo) => {
  test.skip(testInfo.project.name !== 'desktop-chromium', 'The concurrent operator journey runs once on desktop.');
  const created = await createAppeal(page, 'Проверка атомарного закрепления обращения за одним рабочим окном.');
  await login(page, 'operator', operatorPassword);
  await page.goto(`/staff/operator/queue/${created.appealId}`);
  await expect(page.getByRole('heading', { name: 'Провести разбор обращения' })).toBeVisible();

  const secondContext = await browser.newContext({
    baseURL: process.env.OTKLIK_E2E_BASE_URL ?? 'http://localhost:3000/',
  });
  const secondPage = await secondContext.newPage();
  try {
    await login(secondPage, 'operator', operatorPassword);
    await secondPage.goto('/staff/operator/queue');
    await secondPage.getByLabel('Найти по номеру обращения').fill(caseNumber(created.appealId));
    const busyRow = secondPage.locator(`[data-appeal-id="${created.appealId}"]`);
    await expect(busyRow).toBeDisabled();
    await expect(busyRow).toContainText('Сейчас разбирает другой оператор');

    await secondPage.goto(`/staff/operator/queue/${created.appealId}`);
    await expect(secondPage.getByRole('heading', { name: 'Обращение уже разбирает другой оператор' })).toBeVisible();
  } finally {
    const leaseId = await page.evaluate(() => window.sessionStorage.getItem('otklik:operator-work-lease'));
    if (leaseId) {
      await staffPost(page, `/api/staff/operator/work/${created.appealId}/release`, { leaseId });
    }
    await secondContext.close();
  }
});

test('urgent queue shows the risk evidence and supports both review decisions', async ({ page }, testInfo) => {
  test.skip(testInfo.project.name !== 'desktop-chromium', 'The mutating crisis journey runs once on desktop.');
  const crisis = await createAppeal(page, 'Мне угрожают убить прямо сейчас, я боюсь возвращаться один и прошу помощи.');
  await login(page, 'operator', operatorPassword);

  await page.goto(`/staff/operator/urgent/${crisis.appealId}`);
  await expect(page.getByRole('heading', { name: 'Возможная угроза жизни' })).toBeFocused();
  await expect(page.getByText('Контакт не оставлен. Не обещайте физическую помощь', { exact: false })).toBeVisible();
  await expect(page.getByRole('link', { name: '112' })).toHaveAttribute('href', 'tel:112');
  await expect(page.getByRole('link', { name: '8 800 2000 122' })).toBeVisible();
  await expect(page.getByRole('link', { name: '124' })).toBeVisible();
  await expect(page.getByRole('heading', { name: 'Сведения для проверки' })).toBeVisible();
  await expect(page.getByText('Мне угрожают убить прямо сейчас', { exact: false })).toBeVisible();

  await materialButton(page, 'Подтвердить срочность').click();
  await expect(page.getByRole('heading', { name: 'Срочность подтверждена' })).toBeVisible();
  await expect(page.locator('.action-receipt')).toContainText('Срочность подтверждена');
  await materialButton(page, 'Перейти к разбору и назначению').click();
  await expect(page).toHaveURL(new RegExp(`/staff/operator/queue/${crisis.appealId}$`, 'u'));

  const falsePositive = await createAppeal(page, 'В школьной постановке была фраза «меня убьют», но это цитата из роли, непосредственной угрозы нет.');
  await page.goto(`/staff/operator/urgent/${falsePositive.appealId}`);
  await materialButton(page, 'Сигнал не подтверждается').click();
  await page.getByLabel('Почему срочность не подтверждается').fill('Это цитата из постановки, непосредственной угрозы сейчас нет.');
  await materialButton(page, 'Подтвердить решение').click();
  await expect(page.locator('.action-receipt')).toContainText('срочность не подтверждена');
  await expect(page).not.toHaveURL(new RegExp(falsePositive.appealId, 'u'));
});

test('expert requests and both return decisions are separate operator tasks', async ({ page }, testInfo) => {
  test.skip(testInfo.project.name !== 'desktop-chromium', 'The mutating collaboration and return journey runs once on desktop.');
  test.setTimeout(240_000);

  const collaboration = await prepareAssignedAppeal(page, 'Нужна помощь нескольких специалистов по затянувшемуся школьному конфликту.');
  await login(page, 'expert', expertPassword);
  await acceptAssignedAppeal(page, collaboration.appealId);
  const requestResponse = await staffPost(page, `/api/staff/expert/appeals/${collaboration.appealId}/workflow-requests`, {
    clientRequestId: crypto.randomUUID(),
    type: 'CoExecutor',
    reason: 'Нужен медиатор как соисполнитель для подготовки безопасного разговора.',
  });
  const request = await requestResponse.json() as { id: string };

  await login(page, 'operator', operatorPassword);
  await page.goto(`/staff/operator/requests/${request.id}`);
  await expect(page.getByRole('heading', { name: 'Добавить соисполнителя' })).toBeFocused();
  await expect(page.getByText('Нужен медиатор как соисполнитель', { exact: false })).toBeVisible();
  await expect(page.getByText('Ответственный не изменится', { exact: false })).toBeVisible();
  await expect(page.getByText('затянувшемуся школьному конфликту', { exact: false })).toHaveCount(0);
  await page.locator('.collaboration-decision select').selectOption({ index: 1 });
  await materialButton(page, 'Подтвердить').click();
  await expect(page.locator('.action-receipt')).toContainText('Запрос подтверждён');
  await expect(page).not.toHaveURL(new RegExp(request.id, 'u'));

  const returned = await prepareRecommendationReadyAppeal(page, 'Мне нужен более конкретный план после повторяющегося давления в школе.');
  const complaintResponse = await page.request.post('/api/public/appeals/complaints', { data: { trackNumber: returned.trackNumber, clientComplaintId: crypto.randomUUID(), body: 'Специалист ответил слишком общо, и мне было непонятно, что делать дальше.' } });
  await expectOk(complaintResponse);
  const complaint = await complaintResponse.json() as { id: string };
  await login(page, 'operator', operatorPassword);
  await page.goto(`/staff/operator/complaints/${complaint.id}`);
  await expect(page.getByRole('heading', { name: 'Проверьте сообщение заявителя' })).toBeFocused();
  await expect(page.getByText('Это не удаляет обращение', { exact: false })).toBeVisible();
  await materialButton(page, 'Обработать жалобу').click();
  await materialButton(page, 'Подтвердить обработку').click();
  await expect(page.locator('.action-receipt')).toContainText('убрана из входящих');
  await expect(page).not.toHaveURL(new RegExp(complaint.id, 'u'));

  await returnRecommendation(page, returned.trackNumber, returned.version, 'Первый ответ оказался слишком общим.');
  await login(page, 'operator', operatorPassword);
  await page.goto(`/staff/operator/returns/${returned.appealId}`);
  await expect(page.getByRole('heading', { name: 'Подберите следующий шаг' })).toBeFocused();
  await expect(page.getByText('Возврат 1 из 2', { exact: true })).toBeVisible();
  await expect(page.getByRole('heading', { name: 'Предыдущие назначения' })).toBeVisible();
  await page.getByLabel('Ответственный специалист').selectOption(primaryExpertId);
  await materialButton(page, 'Назначить повторно').click();
  await expect(page.locator('.action-receipt')).toContainText('повторно назначено');

  await login(page, 'expert', expertPassword);
  const secondVersion = await acceptAndRecommend(page, returned.appealId, 'Новая версия ответа с конкретными шагами безопасного обращения к взрослому.');
  await returnRecommendation(page, returned.trackNumber, secondVersion, 'Нужна итоговая помощь после второй попытки.');
  await login(page, 'operator', operatorPassword);
  await page.goto(`/staff/operator/returns/${returned.appealId}`);
  await expect(page.getByRole('heading', { name: 'Нужно итоговое решение' })).toBeFocused();
  await expect(page.getByText('Возврат 2 из 2', { exact: true })).toBeVisible();
  await expect(materialButton(page, 'Назначить повторно')).toHaveCount(0);
  await page.getByLabel('Бережное объяснение заявителю').fill('Мы завершили повторную проверку и сохранили всю историю. При непосредственной угрозе позвоните 112.');
  await materialButton(page, 'Отправить объяснение и завершить').click();
  await expect(page.getByRole('status')).toContainText('Итоговое объяснение отправлено');
  await expect(page).not.toHaveURL(new RegExp(returned.appealId, 'u'));
});

test('mobile operator list and detail are separate screens without horizontal scrolling', async ({ page }, testInfo) => {
  test.skip(testInfo.project.name !== 'mobile-chromium', 'The mobile transition is verified in the mobile project.');
  await page.setViewportSize({ width: 320, height: 800 });
  const created = await createAppeal(page, 'Мобильная проверка списка и карточки обращения без потери фильтров.');
  await login(page, 'operator', operatorPassword);
  await page.goto('/staff/operator/queue?stage=new&priority=standard');
  await page.getByLabel('Найти по номеру обращения').fill(caseNumber(created.appealId));
  await page.locator(`[data-appeal-id="${created.appealId}"]`).click();
  await expect(page).toHaveURL(new RegExp(`${created.appealId}\\?stage=new&priority=standard&q=`, 'u'));
  await expect(page.getByRole('button', { name: 'Вернуться к очереди' })).toBeVisible();
  await expect(page.getByRole('heading', { name: 'Разбор обращений' })).toBeHidden();
  expect(await page.evaluate(() => document.documentElement.scrollWidth)).toBe(
    await page.evaluate(() => document.documentElement.clientWidth),
  );
  await page.getByRole('button', { name: 'Вернуться к очереди' }).click();
  await expect(page).toHaveURL(/\/staff\/operator\/queue\?stage=new&priority=standard&q=/u);
  await expect(page.getByRole('heading', { name: 'Разбор обращений' })).toBeVisible();
});

type CreatedAppeal = { appealId: string; trackNumber: string };
const caseNumber = (appealId: string) => `ОБР-${appealId.replaceAll('-', '').slice(0, 8).toUpperCase()}`;
type OperatorDetail = {
  version: number;
  suggestion: { categoryId: string } | null;
  categories: Array<{ id: string }>;
  routing: { experts: Array<{ id: string; displayName: string }> };
};

async function createAppeal(page: Page, narrative: string) {
  const response = await page.request.post('/api/public/appeals', {
    data: {
      clientRequestId: crypto.randomUUID(),
      applicantType: 'Student',
      submissionPath: 'FreeText',
      categoryId: null,
      narrative,
      answers: { place: 'В школе', frequency: 'Регулярно' },
      crisisContact: null,
    },
  });
  await expectOk(response);
  return response.json() as Promise<CreatedAppeal>;
}

async function prepareAssignedAppeal(page: Page, narrative: string) {
  const created = await createAppeal(page, narrative);
  await login(page, 'operator', operatorPassword);
  let detail = await getJson<OperatorDetail>(page, `/api/staff/operator/queue/${created.appealId}`);
  const categoryId = detail.suggestion?.categoryId ?? detail.categories[0].id;
  await staffPost(page, `/api/staff/operator/queue/${created.appealId}/triage`, {
    categoryId,
    priority: 'Standard',
    expectedVersion: detail.version,
  });
  detail = await getJson<OperatorDetail>(page, `/api/staff/operator/queue/${created.appealId}`);
  expect(detail.routing.experts.some((expert) => expert.id === primaryExpertId)).toBeTruthy();
  await staffPost(page, `/api/staff/operator/queue/${created.appealId}/assign`, {
    expertId: primaryExpertId,
    expectedVersion: detail.version,
    allowOverCapacity: true,
    overrideReason: 'Контролируемое назначение для проверки рабочего интерфейса.',
  });
  return created;
}

async function prepareRecommendationReadyAppeal(page: Page, narrative: string) {
  const created = await prepareAssignedAppeal(page, narrative);
  await login(page, 'expert', expertPassword);
  const version = await acceptAndRecommend(page, created.appealId, 'Первый ответ специалиста с безопасными и понятными действиями для заявителя.');
  return { ...created, version };
}

async function acceptAssignedAppeal(page: Page, appealId: string) {
  const detail = await getJson<{ version: number }>(page, `/api/staff/expert/appeals/${appealId}`);
  await staffPost(page, `/api/staff/expert/appeals/${appealId}/accept`, { expectedVersion: detail.version });
}

async function acceptAndRecommend(page: Page, appealId: string, body: string) {
  await acceptAssignedAppeal(page, appealId);
  const detail = await getJson<{ version: number }>(page, `/api/staff/expert/appeals/${appealId}`);
  const response = await staffPost(page, `/api/staff/expert/appeals/${appealId}/recommendations`, {
    clientRecommendationId: crypto.randomUUID(),
    body,
    expectedVersion: detail.version,
    leaseId: null,
  });
  const result = await response.json() as { appeal: { version: number } };
  return result.appeal.version;
}

async function returnRecommendation(page: Page, trackNumber: string, expectedVersion: number, details: string) {
  const response = await page.request.post('/api/public/appeals/outcomes/returned', {
    data: {
      trackNumber,
      clientActionId: crypto.randomUUID(),
      expectedVersion,
      reason: 'NeedMoreHelp',
      details,
    },
  });
  await expectOk(response);
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

async function login(page: Page, userName: string, password: string) {
  await page.goto('/staff');
  if (await materialButton(page, 'Выйти').isVisible().catch(() => false)) {
    const csrfResponse = await page.request.get('/api/staff/auth/csrf');
    await expectOk(csrfResponse);
    const { token } = await csrfResponse.json() as { token: string };
    const logoutResponse = await page.request.post('/api/staff/auth/logout', {
      headers: { 'X-CSRF-TOKEN': token },
    });
    await expectOk(logoutResponse);
    await page.goto('/staff');
  }
  await expect(page.getByRole('heading', { name: 'Вход для сотрудников' })).toBeVisible();
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

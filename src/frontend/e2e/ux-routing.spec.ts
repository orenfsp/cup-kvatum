import { expect, test, type Page } from '@playwright/test';

const passwords = {
  operator: process.env.OTKLIK_OPERATOR_PASSWORD ?? 'Operator!2026',
  expert: process.env.OTKLIK_EXPERT_PASSWORD ?? 'ExpertHelp!2026',
  administrator: process.env.OTKLIK_ADMINISTRATOR_PASSWORD ?? 'AdminPanel!2026',
};

test('public routes use the applicant language and keep the legacy status URL compatible', async ({ page }) => {
  await page.goto('/appeal/status');
  await expect(page).toHaveURL(/\/appeal\/access$/u);
  const navigation = page.getByRole('navigation', { name: 'Основная навигация' });
  await expect(navigation.getByRole('link', { name: 'Обратиться' })).toBeVisible();
  await expect(navigation.getByRole('link', { name: 'Моё обращение' })).toHaveAttribute('aria-current', 'page');
  await expect(navigation.getByRole('link', { name: 'Вход для сотрудников' })).toHaveCount(0);
  await expect(page.getByRole('link', { name: 'Вход для сотрудников' })).toBeVisible();
});

test('operator sections support URL navigation, Back, Forward and refresh', async ({ page }) => {
  await login(page, 'operator', passwords.operator);
  await expect(page).toHaveURL(/\/staff\/operator\/queue$/u);
  await expect(page.getByRole('heading', { name: 'Очередь обращений' })).toBeVisible();

  await openRoleLink(page, 'Срочная помощь');
  await expect(page).toHaveURL(/\/staff\/operator\/urgent$/u);
  await expect(page.getByRole('heading', { name: 'Срочная помощь' })).toBeVisible();
  await page.goBack();
  await expect(page).toHaveURL(/\/staff\/operator\/queue$/u);
  await page.goForward();
  await page.reload();
  await expect(page).toHaveURL(/\/staff\/operator\/urgent$/u);
  await expect(page.getByRole('heading', { name: 'Срочная помощь' })).toBeVisible();
});

test('expert direct routes survive refresh', async ({ page }) => {
  await login(page, 'expert', passwords.expert);
  await page.goto('/staff/expert/waiting');
  await expect(page.getByRole('heading', { name: 'Ждут заявителя' })).toBeVisible();
  await page.reload();
  await expect(page).toHaveURL(/\/staff\/expert\/waiting$/u);
});

test('administrator routes reject a foreign role URL without leaking an object', async ({ page }) => {
  await login(page, 'administrator', passwords.administrator);
  await page.goto('/staff/admin/users');
  await expect(page.getByRole('heading', { name: 'Сотрудники и доступ' })).toBeVisible();
  await page.goto('/staff/operator/queue');
  await expect(page).toHaveURL(/\/staff\/admin$/u);
  await expect(page.getByRole('status')).toContainText('недоступен для вашей роли');
});

test('mobile staff navigation opens through an explicit Sections action', async ({ page }, testInfo) => {
  test.skip(testInfo.project.name !== 'mobile-chromium', 'Mobile navigation is verified in the mobile project.');
  await login(page, 'operator', passwords.operator);
  const trigger = page.getByRole('button', { name: 'Разделы', exact: true });
  await expect(trigger).toBeVisible();
  await trigger.click();
  await expect(trigger).toHaveAttribute('aria-expanded', 'true');
  await expect(page.getByRole('link', { name: 'Возвраты' })).toBeVisible();
  await page.getByRole('link', { name: 'Возвраты' }).click();
  await expect(page).toHaveURL(/\/staff\/operator\/returns$/u);
  await expect(trigger).toHaveAttribute('aria-expanded', 'false');
  expect(await page.evaluate(() => document.documentElement.scrollWidth)).toBe(
    await page.evaluate(() => document.documentElement.clientWidth),
  );
});

async function login(page: Page, userName: string, password: string) {
  await page.goto('/staff');
  const logoutButton = materialButton(page, 'Выйти');
  if (await logoutButton.isVisible().catch(() => false)) await logout(page);
  await page.getByLabel('Логин').fill(userName);
  await page.getByLabel('Пароль').fill(password);
  await materialButton(page, 'Войти').click();
  await expect(materialButton(page, 'Выйти')).toBeVisible();
}

async function logout(page: Page) {
  await materialButton(page, 'Выйти').click();
  await expect(page.getByRole('heading', { name: 'Вход для сотрудников' })).toBeVisible();
}

async function openRoleLink(page: Page, name: string) {
  const trigger = page.getByRole('button', { name: 'Разделы', exact: true });
  if (await trigger.isVisible().catch(() => false)) await trigger.click();
  await page.getByRole('link', { name, exact: true }).click();
}

function materialButton(page: Page, text: string) {
  return page.locator('md-filled-button, md-outlined-button, md-text-button').filter({ hasText: text }).last();
}

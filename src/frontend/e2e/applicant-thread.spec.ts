import { expect, test } from '@playwright/test';

test.describe('Applicant thread workspace', () => {
  test('one track opens a clear overview and routed history without a URL secret', async ({ page }) => {
    await page.goto('/appeal/access');
    await page.getByLabel('Трек-номер').fill('ОТК-FNSH-89AB');
    await page.getByRole('button', { name: 'Открыть обращение' }).click();

    await expect(page).toHaveURL(/\/appeal\/overview$/);
    await expect(page.getByRole('navigation', { name: 'Разделы обращения' })).toBeVisible();
    await expect(page.locator('.appeal-guidance h2')).toBeVisible();
    await expect(page.getByRole('heading', { name: 'Что уже есть в обращении' })).toBeVisible();
    await expect(page).not.toHaveURL(/ОТК|track/i);

    await page.getByRole('link', { name: 'Вся история', exact: true }).click();
    await expect(page).toHaveURL(/\/appeal\/history$/);
    await expect(page.getByRole('heading', { name: 'История обращения' })).toBeVisible();
    await expect(page.locator('.appeal-cycle summary strong').first()).toBeVisible();
    await expect(page.getByText('Написать снова')).toHaveCount(0);

    await page.reload();
    await expect(page).toHaveURL(/\/appeal\/history$/);
    await expect(page.getByRole('heading', { name: 'История обращения' })).toBeVisible();
  });

  test('public workspace has no horizontal overflow at 320 px', async ({ page }) => {
    await page.setViewportSize({ width: 320, height: 720 });
    await page.goto('/appeal/access');
    await page.getByLabel('Трек-номер').fill('ОТК-FNSH-89AB');
    await page.getByRole('button', { name: 'Открыть обращение' }).click();
    await page.getByRole('link', { name: 'Вся история', exact: true }).click();

    const sizes = await page.evaluate(() => ({
      viewport: window.innerWidth,
      document: document.documentElement.scrollWidth,
    }));
    expect(sizes.document).toBeLessThanOrEqual(sizes.viewport);
  });

  test('a child sees one required action and understands where every part is stored', async ({ page }) => {
    await page.setViewportSize({ width: 390, height: 844 });
    await page.goto('/appeal/access');
    await page.getByLabel('Трек-номер').fill('ОТК-RSPN-6789');
    await page.getByRole('button', { name: 'Открыть обращение' }).click();

    await expect(page).toHaveURL(/\/appeal\/overview$/);
    await expect(page.getByText('Тебе нужно сделать один шаг', { exact: true })).toBeVisible();
    await expect(page.getByRole('heading', { name: 'Ответ специалиста готов' })).toBeVisible();
    await expect(page.getByText('Тебе не нужно запоминать статусы.', { exact: false })).toBeVisible();
    await expect(page.getByRole('heading', { name: 'Что уже есть в твоём обращении' })).toBeVisible();
    await expect(page.getByText('Переписка', { exact: true }).last()).toBeVisible();
    await expect(page.getByText('Файлы', { exact: true })).toBeVisible();

    await page.getByRole('button', { name: 'Прочитать ответ специалиста' }).click();
    await expect(page).toHaveURL(/\/appeal\/answer$/);
    await expect(page.getByRole('heading', { name: 'Помог ли тебе этот ответ?' })).toBeVisible();

    const sizes = await page.evaluate(() => ({
      viewport: window.innerWidth,
      document: document.documentElement.scrollWidth,
    }));
    expect(sizes.document).toBeLessThanOrEqual(sizes.viewport);
  });
});

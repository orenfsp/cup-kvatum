import { expect, test } from '@playwright/test';

test.describe('Applicant thread workspace', () => {
  test('one track opens routed history and survives reload without a URL secret', async ({ page }) => {
    await page.goto('/appeal/access');
    await page.getByLabel('Трек-номер').fill('ОТК-FNSH-89AB');
    await page.getByRole('button', { name: 'Открыть обращение' }).click();

    await expect(page).toHaveURL(/\/appeal\/dialog$/);
    await expect(page.getByRole('navigation', { name: 'Разделы обращения' })).toBeVisible();
    await expect(page).not.toHaveURL(/ОТК|track/i);

    await page.getByRole('link', { name: 'История' }).click();
    await expect(page).toHaveURL(/\/appeal\/history$/);
    await expect(page.getByRole('heading', { name: 'История обращения' })).toBeVisible();
    await expect(page.getByText('Цикл 1', { exact: true })).toBeVisible();
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
    await page.getByRole('link', { name: 'История' }).click();

    const sizes = await page.evaluate(() => ({
      viewport: window.innerWidth,
      document: document.documentElement.scrollWidth,
    }));
    expect(sizes.document).toBeLessThanOrEqual(sizes.viewport);
  });
});

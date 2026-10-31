import { expect, test } from '@playwright/test';
import { uniqueName } from './support';

test('should create a board through the dialog and open it', async ({ page }) => {
  const name = uniqueName('E2E board');

  await page.goto('/boards');
  await expect(page.getByRole('heading', { level: 1, name: 'Boards' })).toBeVisible();

  await page.getByRole('button', { name: 'New board' }).click();
  const dialog = page.getByRole('dialog');
  await dialog.getByLabel('Name').fill(name);
  await dialog.getByLabel('Description').fill('Created by Playwright');
  await dialog.getByRole('button', { name: 'Save' }).click();
  await expect(dialog).toBeHidden();

  const link = page.getByRole('link', { name });
  await expect(link).toBeVisible();
  await link.click();

  await expect(page).toHaveURL(/\/boards\/\d+$/);
  await expect(page.getByRole('heading', { level: 1, name })).toBeVisible();
});

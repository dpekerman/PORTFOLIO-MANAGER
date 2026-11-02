import type { Page } from '@playwright/test';
import { expectNoHorizontalPageOverflow, settle } from './support/overflow';
import { expect, test } from './support/test';

const dialog = (page: Page) => page.locator('mat-dialog-container');

/** Opens Edit Position for the first stock (table row on desktop/tablet, card on handsets). */
async function openEdit(page: Page) {
  await page.goto('/portfolio');
  await settle(page);
  const edit = page.locator('button:has(mat-icon:text-is("edit")):visible').first();
  await expect(edit).toBeVisible();
  await edit.click();
  await expect(dialog(page).getByRole('heading').first()).toBeVisible();
}

async function chooseRole(page: Page, role: string) {
  await dialog(page).getByLabel('Holding Role').click();
  await page.getByRole('option', { name: role, exact: true }).click();
}

const saveButton = (page: Page) => dialog(page).getByRole('button', { name: /Save Changes/ });

test.describe('PM-014 Edit Position — Investment Role', () => {
  test('a rejected Role change keeps the dialog open, shows the backend reason and sends one request', async ({ page, errors }) => {
    const puts: unknown[] = [];
    await page.route('**/api/portfolio/*', async (route) => {
      if (route.request().method() !== 'PUT') return route.fallback();
      puts.push(route.request().postDataJSON());
      return route.fulfill({
        status: 409,
        contentType: 'application/json',
        body: JSON.stringify({ error: 'Other open lots of HUN.TO in this trade episode have different roles.' }),
      });
    });
    await openEdit(page);
    await chooseRole(page, 'Options');
    await saveButton(page).click();

    await expect(dialog(page)).toContainText('Other open lots of HUN.TO in this trade episode have different roles.');
    await expect(dialog(page).getByLabel('Holding Role')).toContainText('Options');
    await expect(saveButton(page)).toBeEnabled();
    expect(puts).toHaveLength(1);
    expect((puts[0] as { holdingRole: string }).holdingRole).toBe('Options');
    await expectNoHorizontalPageOverflow(page);
    expect(errors).toEqual([]);
  });

  test('a valid Role change saves once, closes the dialog and reports success', async ({ page, errors }) => {
    const puts: Array<Record<string, unknown>> = [];
    await page.route('**/api/portfolio/*', async (route) => {
      if (route.request().method() !== 'PUT') return route.fallback();
      const body = route.request().postDataJSON() as Record<string, unknown>;
      puts.push(body);
      const id = Number(new URL(route.request().url()).pathname.split('/').pop());
      return route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify({ updated: { id, symbol: 'X.TO', ...body }, newOpenItem: null }),
      });
    });
    await openEdit(page);
    await expect(dialog(page)).toContainText('does not change quantity, cost, transactions or broker orders');
    await chooseRole(page, 'Options');
    await saveButton(page).click();

    await expect(dialog(page)).toHaveCount(0);
    await expect(page.locator('.mat-mdc-snack-bar-container')).toContainText('Position updated');
    expect(puts).toHaveLength(1);
    expect(puts[0]['holdingRole']).toBe('Options');
    expect(errors).toEqual([]);
  });
});

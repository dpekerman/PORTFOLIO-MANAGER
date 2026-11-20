import { test, expect } from './support/test';

test.describe('persistent row selection', () => {
  test('the row stays highlighted after its dialog is closed', async ({ page, errors }) => {
    await page.goto('/signal-tracker');
    const rows = page.locator('tr.signal-row:visible');
    await expect(rows.first()).toBeVisible();

    const lmt = rows.filter({ hasText: 'LMT.TO' });
    await expect(rows.locator('xpath=self::*[contains(@class,"row-selected")]')).toHaveCount(0);

    await lmt.click();
    await expect(page.getByRole('dialog')).toBeVisible();
    await page.keyboard.press('Escape');
    await expect(page.getByRole('dialog')).toBeHidden();

    await expect(lmt).toHaveClass(/row-selected/);
    await expect(page.locator('tr.row-selected:visible')).toHaveCount(1);

    // Selecting another row moves the highlight.
    const other = rows.filter({ hasNotText: 'LMT.TO' }).first();
    await other.click();
    await page.keyboard.press('Escape');
    await expect(other).toHaveClass(/row-selected/);
    await expect(lmt).not.toHaveClass(/row-selected/);
    expect(errors, 'runtime errors').toEqual([]);
  });
});

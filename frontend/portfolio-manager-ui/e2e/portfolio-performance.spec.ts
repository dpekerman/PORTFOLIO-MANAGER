import { expect, test } from './support/test';
import { expectNoHorizontalPageOverflow, settle } from './support/overflow';

/** PM-022: Value change, net cash in/out and investment gain/loss render separately on every device (dashboard only). */
for (const route of ['/dashboard']) {
  test.describe(`cash-flow adjusted performance on ${route}`, () => {
    test('shows separate value change, net flow and gain; N/A percent with a flow', async ({ page, errors }) => {
      await page.goto(route);
      const toggle = page.getByRole('button', { name: 'Toggle cash adjusted performance' });
      await toggle.scrollIntoViewIfNeeded();
      if ((await toggle.getAttribute('aria-expanded')) !== 'true') {
        await toggle.click();
      }
      const block = page.getByTestId('portfolio-performance');
      await expect(block).toBeVisible();
      await settle(page);

      const day = block.getByTestId('pp-Day');
      await expect(day).toContainText('Value change');
      await expect(day).toContainText('Net cash in/out');
      await expect(day.getByTestId('pp-gain')).toContainText('1,842');
      await expect(day.getByTestId('pp-pct')).toHaveText('N/A');

      await expect(block.getByTestId('pp-Week').getByTestId('pp-pct')).toContainText('1.34%');
      await expect(block.getByTestId('pp-Month').getByTestId('pp-state')).toHaveText('Unavailable');
      await expect(block.getByTestId('pp-YTD').getByTestId('pp-state')).toContainText('Updating');

      await expectNoHorizontalPageOverflow(page);
      expect(errors).toEqual([]);
    });
  });
}

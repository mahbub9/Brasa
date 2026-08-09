import type { Page } from '@playwright/test';

/**
 * Picks a free table in the rendered UI, opens it, and returns its label.
 * Retries against a different table if the server rejects it with a 409
 * (`floor.table_not_free`) — the same race `openOrderOnAnyFreeTable` in
 * api.ts handles for API-driven specs. The UI picks the table optimistically
 * (the confirm panel opens client-side with no server round trip), so the
 * conflict only surfaces once "confirm-open-table" is clicked: the app stays
 * on the table picker and shows an error banner instead of navigating to the
 * order screen. See TableConfiguration.cs for the xmin concurrency token
 * that makes this a real, occasionally-observable race under parallel
 * workers, not a hypothetical one.
 */
export async function openAnyFreeTable(page: Page, coverCount: number, attempts = 5): Promise<string> {
  for (let attempt = 1; attempt <= attempts; attempt += 1) {
    const freeTable = page.locator('.floor-table-Free button').first();
    const tableLabel = await freeTable.locator('.floor-table-label').textContent();
    await freeTable.click();

    await page.getByTestId('table-confirm').getByRole('spinbutton').fill(String(coverCount));
    await page.getByTestId('confirm-open-table').click();

    const orderHeading = page.getByRole('heading', { name: tableLabel ?? '' });
    const errorBanner = page.getByRole('alert');

    const outcome = await Promise.race([
      orderHeading.waitFor({ timeout: 5_000 }).then(() => 'opened' as const),
      errorBanner.waitFor({ timeout: 5_000 }).then(() => 'conflict' as const),
    ]).catch(() => 'timeout' as const);

    if (outcome === 'opened') {
      return tableLabel ?? '';
    }

    if (attempt === attempts) {
      throw new Error(`Could not open a table after ${attempts} attempts (last outcome: ${outcome}, table: "${tableLabel}").`);
    }

    // Conflict: dismiss the error banner (if that's what happened — a
    // timeout falls through here too and just retries) and try again
    // against whatever the refreshed floor state now shows as free.
    await errorBanner.getByRole('button').click().catch(() => {});
  }

  throw new Error('unreachable');
}

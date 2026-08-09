import type { Page } from '@playwright/test';

/**
 * Picks a free table in the rendered UI, opens it, and returns its raw
 * label (the exact `Table.Label` value, e.g. "Mesa 6" — what `data-testid`
 * attributes are built from). Retries against a different table if the
 * server rejects it with a 409 (`floor.table_not_free`) — the same race
 * `openOrderOnAnyFreeTable` in api.ts handles for API-driven specs. The UI
 * picks the table optimistically (the confirm panel opens client-side with
 * no server round trip), so the conflict only surfaces once
 * "confirm-open-table" is clicked: the app stays on the table picker and
 * shows an error banner instead of navigating to the order screen. See
 * TableConfiguration.cs for the xmin concurrency token that makes this a
 * real, occasionally-observable race under parallel workers, not a
 * hypothetical one.
 *
 * The raw label and the *displayed* label can differ (src/lib/tableLabel.ts
 * renders "Mesa 6" as "Table 6" in English) — the internal wait below
 * matches on whatever text is actually on screen, but the return value is
 * always the raw label, since every caller uses it to build a
 * `data-testid` (`table-${label}`), not to assert rendered copy. A caller
 * that needs the displayed text should read it fresh from the DOM instead.
 */
export async function openAnyFreeTable(page: Page, coverCount: number, attempts = 5): Promise<string> {
  for (let attempt = 1; attempt <= attempts; attempt += 1) {
    const freeTable = page.locator('.floor-table-Free button').first();
    const testId = await freeTable.getAttribute('data-testid');
    const rawLabel = testId?.replace(/^table-/, '') ?? '';
    const displayedLabel = await freeTable.locator('.floor-table-label').textContent();
    await freeTable.click();

    await page.getByTestId('table-confirm').getByRole('spinbutton').fill(String(coverCount));
    await page.getByTestId('confirm-open-table').click();

    const orderHeading = page.getByRole('heading', { name: displayedLabel ?? '' });
    const errorBanner = page.getByRole('alert');

    const outcome = await Promise.race([
      orderHeading.waitFor({ timeout: 5_000 }).then(() => 'opened' as const),
      errorBanner.waitFor({ timeout: 5_000 }).then(() => 'conflict' as const),
    ]).catch(() => 'timeout' as const);

    if (outcome === 'opened') {
      return rawLabel;
    }

    if (attempt === attempts) {
      throw new Error(`Could not open a table after ${attempts} attempts (last outcome: ${outcome}, table: "${rawLabel}").`);
    }

    // Conflict: dismiss the error banner (if that's what happened — a
    // timeout falls through here too and just retries) and try again
    // against whatever the refreshed floor state now shows as free.
    await errorBanner.getByRole('button').click().catch(() => {});
  }

  throw new Error('unreachable');
}

/**
 * Opens the transfer-table dialog (ORD-12) and picks the first free table it
 * offers, retrying against a different one if the server rejects it with a
 * 409 (`floor.table_not_free`) — the same race `openAnyFreeTable` handles on
 * initial seating, now possible on the transfer side too since another
 * worker can occupy the picker's target between its fetch and this click.
 * Assumes the transfer-table dialog is already open. Returns the raw label
 * (from `data-testid`), same reasoning as `openAnyFreeTable` above — its
 * button text can be a translated display form, but every caller uses the
 * return value to build a `data-testid`.
 */
export async function transferToAnyFreeTable(page: Page, attempts = 5): Promise<string> {
  const dialog = page.getByRole('dialog', { name: 'Transferir para' });
  const errorBanner = page.getByRole('alert');

  for (let attempt = 1; attempt <= attempts; attempt += 1) {
    const targetButton = dialog.locator('.transfer-picker-tables button').first();
    const testId = await targetButton.getAttribute('data-testid');
    const rawLabel = testId?.replace(/^transfer-target-/, '') ?? '';
    const displayedLabel = await targetButton.textContent();
    await targetButton.click();

    const outcome = await Promise.race([
      dialog.waitFor({ state: 'hidden', timeout: 5_000 }).then(() => 'transferred' as const),
      errorBanner.waitFor({ timeout: 5_000 }).then(() => 'conflict' as const),
    ]).catch(() => 'timeout' as const);

    if (outcome === 'transferred') {
      return rawLabel;
    }

    if (attempt === attempts) {
      throw new Error(
        `Could not transfer to a free table after ${attempts} attempts (last outcome: ${outcome}, table: "${displayedLabel}").`,
      );
    }

    await errorBanner.getByRole('button').click().catch(() => {});
  }

  throw new Error('unreachable');
}

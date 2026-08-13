import { expect, test } from '@playwright/test';
import {
  addLine,
  closeOrderAndClearTable,
  deleteMenuItem,
  fireLines,
  fireLinesResponse,
  getMenu,
  importMenuItemsResponse,
  openOrderOnAnyFreeTable,
  updateMenuItemCourse,
  voidLine,
} from './support/api';
import { openAnyFreeTable } from './support/ui';

// ORD-07/08/09 — course firing. Course.cs's own doc comment named this task
// before it existed ("this is the tag it will read from once it is"), so
// CAT-14's Course assignment (Starter/Main/Dessert/Drink) was the seam this
// slice was always meant to unblock. No real kitchen exists yet
// (KIT-01…09/AGT are unbuilt) — firing only flips OrderLine.IsFired/
// FiredAtUtc, never touches money, and needs no manager authorisation
// (unlike void/discounts).
//
// Uses freshly-imported test items with their course set via CAT-14's own
// endpoint, isolated from the shared seeded menu other specs read
// concurrently (2 workers) — same reasoning as menu-price.spec.ts. Seeded
// items carry no course today, so a UI test against them could only ever
// exercise "fire all," never a specific course button.

async function createTestItem(
  request: import('@playwright/test').APIRequestContext,
  name: string,
  course: string | null,
): Promise<string> {
  const menu = await getMenu(request);
  const categoryName = menu[0].name;
  const csv = `CategoryName,Name,Price,VatRate\n${categoryName},${name},2.00,0.13\n`;

  const response = await importMenuItemsResponse(request, csv);
  expect((await response.json()).created).toBe(1);

  const item = (await getMenu(request)).flatMap((c) => c.items).find((i) => i.name === name);
  if (!item) throw new Error('Freshly-imported test item did not appear on the menu.');

  if (course) {
    await updateMenuItemCourse(request, item.id, course);
  }
  return item.id;
}

test.describe('course firing (ORD-07/08)', () => {
  test('firing a course sends only its own unfired lines; firing all sends the rest', async ({ request }) => {
    const starterId = await createTestItem(request, `Fire Starter Test ${Date.now()}`, 'Starter');
    const mainId = await createTestItem(request, `Fire Main Test ${Date.now()}`, 'Main');
    const { order, table } = await openOrderOnAnyFreeTable(request, 2);

    const withStarter = await addLine(request, order.id, starterId, 1);
    const starterLine = withStarter.lines[0];
    expect(starterLine.course).toBe('Starter');
    expect(starterLine.isFired).toBe(false);
    expect(starterLine.firedAtUtc).toBeNull();

    const withMain = await addLine(request, order.id, mainId, 1);
    const mainLine = withMain.lines.find((l) => l.id !== starterLine.id)!;
    expect(mainLine.course).toBe('Main');

    const afterFireStarter = await fireLines(request, order.id, 'Starter');
    expect(afterFireStarter.lines.find((l) => l.id === starterLine.id)!.isFired).toBe(true);
    expect(afterFireStarter.lines.find((l) => l.id === starterLine.id)!.firedAtUtc).not.toBeNull();
    expect(afterFireStarter.lines.find((l) => l.id === mainLine.id)!.isFired).toBe(false);

    // Firing an already-fired course again is a no-op, not an error.
    const refired = await fireLines(request, order.id, 'Starter');
    expect(refired.lines.find((l) => l.id === starterLine.id)!.isFired).toBe(true);
    expect(refired.lines.find((l) => l.id === mainLine.id)!.isFired).toBe(false);

    const afterFireAll = await fireLines(request, order.id, null);
    expect(afterFireAll.lines.find((l) => l.id === mainLine.id)!.isFired).toBe(true);

    await closeOrderAndClearTable(request, order.id, table.id);
    await deleteMenuItem(request, starterId);
    await deleteMenuItem(request, mainId);
  });

  test('a voided line is never fired', async ({ request }) => {
    const itemId = await createTestItem(request, `Fire Voided Test ${Date.now()}`, 'Starter');
    const { order, table } = await openOrderOnAnyFreeTable(request, 2);
    const withLine = await addLine(request, order.id, itemId, 1);
    const lineId = withLine.lines[0].id;

    // A second, never-voided line so the order can still be closed afterward.
    const withSecondLine = await addLine(request, order.id, itemId, 1);
    const secondLineId = withSecondLine.lines.find((l) => l.id !== lineId)!.id;

    await voidLine(request, order.id, lineId, 'Rung up by mistake');

    const afterFireAll = await fireLines(request, order.id, null);
    expect(afterFireAll.lines.find((l) => l.id === lineId)!.isFired).toBe(false);
    expect(afterFireAll.lines.find((l) => l.id === secondLineId)!.isFired).toBe(true);

    await closeOrderAndClearTable(request, order.id, table.id);
    await deleteMenuItem(request, itemId);
  });

  test('a line with no course assigned only fires with "fire all", never a named course', async ({ request }) => {
    const itemId = await createTestItem(request, `Fire No Course Test ${Date.now()}`, null);
    const { order, table } = await openOrderOnAnyFreeTable(request, 2);
    const withLine = await addLine(request, order.id, itemId, 1);
    const lineId = withLine.lines[0].id;
    expect(withLine.lines[0].course).toBeNull();

    const afterFireStarter = await fireLines(request, order.id, 'Starter');
    expect(afterFireStarter.lines.find((l) => l.id === lineId)!.isFired).toBe(false);

    const afterFireAll = await fireLines(request, order.id, null);
    expect(afterFireAll.lines.find((l) => l.id === lineId)!.isFired).toBe(true);

    await closeOrderAndClearTable(request, order.id, table.id);
    await deleteMenuItem(request, itemId);
  });

  test('rejects an unrecognised course, a closed order, and an unknown order', async ({ request }) => {
    const itemId = await createTestItem(request, `Fire Reject Test ${Date.now()}`, null);
    const { order, table } = await openOrderOnAnyFreeTable(request, 2);
    await addLine(request, order.id, itemId, 1);

    const wrongCourse = await fireLinesResponse(request, order.id, 'NotACourse');
    expect(wrongCourse.status()).toBe(400);
    expect((await wrongCourse.json()).code).toBe('order.invalid_course');

    const unknownOrder = await fireLinesResponse(request, crypto.randomUUID(), null);
    expect(unknownOrder.status()).toBe(404);
    expect((await unknownOrder.json()).code).toBe('order.not_found');

    await closeOrderAndClearTable(request, order.id, table.id);

    const onClosedOrder = await fireLinesResponse(request, order.id, null);
    expect(onClosedOrder.status()).toBe(409);
    expect((await onClosedOrder.json()).code).toBe('order.not_open');

    await deleteMenuItem(request, itemId);
  });

  test('the pos UI fires a course, showing a "Sent" badge on the fired line', async ({ page, request }) => {
    const itemName = `Fire UI Test ${Date.now()}`;
    const itemId = await createTestItem(request, itemName, 'Starter');

    await page.goto('/');
    const tableLabel = await openAnyFreeTable(page, 2);

    await page.getByRole('button', { name: itemName }).click();
    await expect(page.getByText('Ainda sem itens')).toBeHidden();

    await expect(page.getByTestId('fire-controls')).toBeVisible();
    await page.getByTestId('fire-course-Starter').click();

    await expect(page.getByTestId(/^line-fired-/)).toBeVisible();
    // Nothing left unfired — the whole fire-controls bar disappears.
    await expect(page.getByTestId('fire-controls')).toBeHidden();

    await page.getByTestId('close-order-button').click();
    await expect(page.getByRole('heading', { name: 'Recibo emitido' })).toBeVisible();
    await page.getByRole('button', { name: 'Abrir outra mesa' }).click();
    await page.getByTestId(`table-${tableLabel}`).click();

    await deleteMenuItem(request, itemId);
  });
});

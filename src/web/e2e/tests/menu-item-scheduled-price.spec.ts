import { expect, test } from '@playwright/test';
import type { APIRequestContext } from '@playwright/test';
import {
  addLine,
  closeOrderAndClearTable,
  deleteMenuItem,
  getMenu,
  importMenuItemsResponse,
  openOrderOnAnyFreeTable,
  testClockHeader,
  updateMenuItemScheduledPrice,
  updateMenuItemScheduledPriceResponse,
} from './support/api';

// CAT-16 — a price change queued to take effect at a future instant.
// Deliberately not driven by a background job (nothing in this codebase
// runs one yet, Hangfire is OPS-10): MenuItem.EffectivePrice resolves it
// lazily on every read, the same way CAT-11's schedule resolves its
// recurring window. GET /menu's Price field and AddLine's snapshot must
// both already reflect a due change with zero manual intervention — that
// is the actual thing worth proving, not just that the endpoint exists.
// Schedules a full minute out (comfortably future, no race with request
// latency), confirms it is not yet due against the real clock, then uses
// QA-04's X-Brasa-Test-Clock header to fast-forward the API's own clock
// past that instant for the "is it due now" requests only — no real wait,
// deterministic every run (this spec used to `test.slow()` and actually
// wait out a ~2s window before QA-04 existed).
//
// Uses a freshly-imported item, isolated from the shared seeded menu other
// specs read concurrently — same reasoning as menu-item-takeaway-price.spec.ts.

async function createTestItem(request: APIRequestContext, price: string): Promise<{ id: string; name: string }> {
  const menu = await getMenu(request);
  const categoryName = menu[0].name;
  const name = `Scheduled Price Test ${Date.now()}`;
  const csv = `CategoryName,Name,Price,VatRate\n${categoryName},${name},${price},0.13\n`;

  const response = await importMenuItemsResponse(request, csv);
  expect((await response.json()).created).toBe(1);

  const item = (await getMenu(request)).flatMap((c) => c.items).find((i) => i.name === name);
  if (!item) throw new Error('Freshly-imported test item did not appear on the menu.');
  return { id: item.id, name };
}

function findItem(categories: Awaited<ReturnType<typeof getMenu>>, name: string) {
  const item = categories.flatMap((c) => c.items).find((i) => i.name === name);
  if (!item) throw new Error(`"${name}" not found on GET /menu.`);
  return item;
}

test.describe('menu item scheduled price (CAT-16)', () => {
  test('a due change is reflected in GET /menu and charged by AddLine, with no manual step', async ({ request }) => {
    const item = await createTestItem(request, '5.00');

    try {
      const effectiveFromUtc = new Date(Date.now() + 60_000).toISOString();
      const scheduled = await updateMenuItemScheduledPrice(request, item.id, 6.0, effectiveFromUtc);
      expect(scheduled.scheduledPrice).toEqual({
        newPrice: { amount: 6.0, currency: 'EUR' },
        effectiveFromUtc: scheduled.scheduledPrice!.effectiveFromUtc,
        isActive: false,
      });
      // Not due yet -- Price is still the original, unaffected.
      expect(scheduled.price.amount).toBeCloseTo(5.0, 2);

      const beforeDue = findItem(await getMenu(request), item.name);
      expect(beforeDue.price.amount).toBeCloseTo(5.0, 2);
      expect(beforeDue.scheduledPrice?.isActive).toBe(false);

      // QA-04: fast-forward the API's own clock to just past the scheduled
      // instant, for these requests only -- no real wait.
      const afterEffective = testClockHeader(new Date(Date.parse(effectiveFromUtc) + 1000).toISOString());
      const afterDue = findItem(await getMenu(request, afterEffective), item.name);
      expect(afterDue.price.amount).toBeCloseTo(6.0, 2);
      expect(afterDue.scheduledPrice?.isActive).toBe(true);

      // The actual charge, not just the display -- AddLine must snapshot
      // the same effective price GET /menu just showed, never the stale
      // raw one. Same fast-forwarded clock, since AddLine resolves
      // EffectivePrice against whatever instant this request itself sees.
      const { order, table } = await openOrderOnAnyFreeTable(request, 2);
      try {
        const withLine = await addLine(request, order.id, item.id, 1, [], afterEffective);
        expect(withLine.lines[0].unitPrice.amount).toBeCloseTo(6.0, 2);
      } finally {
        await closeOrderAndClearTable(request, order.id, table.id);
      }

      const cleared = await updateMenuItemScheduledPrice(request, item.id, null, null);
      expect(cleared.scheduledPrice).toBeNull();
      expect(cleared.price.amount).toBeCloseTo(5.0, 2);
    } finally {
      await deleteMenuItem(request, item.id);
    }
  });

  test('rejects a partial request, a non-future date, an invalid date, a negative price, and an unknown item', async ({
    request,
  }) => {
    const item = await createTestItem(request, '3.00');

    try {
      const partial = await updateMenuItemScheduledPriceResponse(request, item.id, 4.0, null);
      expect(partial.status()).toBe(400);
      expect((await partial.json()).code).toBe('catalog.incomplete_scheduled_price');

      const pastDate = new Date(Date.now() - 60_000).toISOString();
      const notFuture = await updateMenuItemScheduledPriceResponse(request, item.id, 4.0, pastDate);
      expect(notFuture.status()).toBe(400);
      expect((await notFuture.json()).code).toBe('catalog.scheduled_price_not_future');

      const futureDate = new Date(Date.now() + 60_000).toISOString();
      const badDate = await updateMenuItemScheduledPriceResponse(request, item.id, 4.0, 'not-a-date');
      expect(badDate.status()).toBe(400);
      expect((await badDate.json()).code).toBe('catalog.invalid_scheduled_price_date');

      const negative = await updateMenuItemScheduledPriceResponse(request, item.id, -1, futureDate);
      expect(negative.status()).toBe(400);
      expect((await negative.json()).code).toBe('catalog.invalid_price');

      const unknownItem = await updateMenuItemScheduledPriceResponse(request, crypto.randomUUID(), 4.0, futureDate);
      expect(unknownItem.status()).toBe(404);
      expect((await unknownItem.json()).code).toBe('catalog.item_not_found');
    } finally {
      await deleteMenuItem(request, item.id);
    }
  });
});

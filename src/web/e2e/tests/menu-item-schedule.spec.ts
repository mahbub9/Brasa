import { expect, test } from '@playwright/test';
import { findMenuItem, getMenu, getMenuAll, updateMenuItemSchedule, updateMenuItemScheduleResponse } from './support/api';

// CAT-11 — a prato do dia's recurring day/time availability window.
// GetMenuAsync (pos-facing) filters a scheduled item out entirely when it's
// outside its own window; GetMenuAllAsync (admin) never filters on it, the
// same "management view sees everything" shape CAT-01/13 already established.
//
// "Pastel de Nata" is deliberately used here and nowhere else in this suite
// (checked before writing this file — see the "Sobremesas" collision trap
// in docs/ai/README.md) because this spec, unlike CAT-14/15's course/station
// tags, actually removes the item from GET /menu for part of its run; any
// other spec relying on it staying visible would race with that. Both day
// names are computed from the real Europe/Lisbon date so the test is
// self-scheduling and never goes stale.

const WEEK_DAYS = ['Sunday', 'Monday', 'Tuesday', 'Wednesday', 'Thursday', 'Friday', 'Saturday'];

function lisbonDayName(): string {
  return new Intl.DateTimeFormat('en-US', { timeZone: 'Europe/Lisbon', weekday: 'long' }).format(new Date());
}

test.describe('menu item schedule (CAT-11)', () => {
  test('sets, persists and clears a recurring day/time window, filtering GET /menu but never GET /menu/all', async ({
    request,
  }) => {
    const today = lisbonDayName();
    const notToday = WEEK_DAYS.find((d) => d !== today)!;

    const item = findMenuItem(await getMenu(request), 'Pastel de Nata');

    try {
      // A window covering today, all day: still on the guest-facing menu.
      const withToday = await updateMenuItemSchedule(request, item.id, [today], '00:00', '23:59');
      expect(withToday.schedule).toEqual({ daysOfWeek: [today], startTime: '00:00', endTime: '23:59' });

      const menuWithToday = await getMenu(request);
      expect(menuWithToday.flatMap((c) => c.items).some((i) => i.id === item.id)).toBe(true);

      // A window that excludes today entirely: gone from GET /menu, but
      // GET /menu/all — the management view — still shows it, schedule and
      // all, exactly the CAT-01/13 "admin must see what it filters" shape.
      await updateMenuItemSchedule(request, item.id, [notToday], '00:00', '23:59');

      const menuWithoutToday = await getMenu(request);
      expect(menuWithoutToday.flatMap((c) => c.items).some((i) => i.id === item.id)).toBe(false);

      const allCategories = await getMenuAll(request);
      const stillInAdmin = allCategories.flatMap((c) => c.items).find((i) => i.id === item.id);
      expect(stillInAdmin).toBeDefined();
      expect(stillInAdmin!.schedule).toEqual({ daysOfWeek: [notToday], startTime: '00:00', endTime: '23:59' });

      // Cleared: unconditionally back on GET /menu, whatever day it is.
      const cleared = await updateMenuItemSchedule(request, item.id, null, null, null);
      expect(cleared.schedule).toBeNull();

      const menuCleared = await getMenu(request);
      expect(menuCleared.flatMap((c) => c.items).some((i) => i.id === item.id)).toBe(true);
    } finally {
      // Never leave this item scheduled out — the dev database isn't reset
      // between runs (QA-02), and a leftover exclusion would silently break
      // whichever spec runs next, the same way an un-closed order does.
      await updateMenuItemSchedule(request, item.id, null, null, null);
    }
  });

  test('rejects an unrecognised day, an invalid time, a backwards window, a partial update and an unknown item', async ({
    request,
  }) => {
    const item = findMenuItem(await getMenu(request), 'Pastel de Nata');

    try {
      const badDay = await updateMenuItemScheduleResponse(request, item.id, ['Lundi'], '10:00', '15:00');
      expect(badDay.status()).toBe(400);
      expect((await badDay.json()).code).toBe('catalog.invalid_day_of_week');

      const badTime = await updateMenuItemScheduleResponse(request, item.id, ['Monday'], '10h00', '15:00');
      expect(badTime.status()).toBe(400);
      expect((await badTime.json()).code).toBe('catalog.invalid_time');

      const backwards = await updateMenuItemScheduleResponse(request, item.id, ['Monday'], '15:00', '10:00');
      expect(backwards.status()).toBe(400);
      expect((await backwards.json()).code).toBe('catalog.invalid_schedule');

      const partial = await updateMenuItemScheduleResponse(request, item.id, ['Monday'], null, null);
      expect(partial.status()).toBe(400);
      expect((await partial.json()).code).toBe('catalog.incomplete_schedule');

      const unknownItem = await updateMenuItemScheduleResponse(request, crypto.randomUUID(), ['Monday'], '10:00', '15:00');
      expect(unknownItem.status()).toBe(404);
      expect((await unknownItem.json()).code).toBe('catalog.item_not_found');
    } finally {
      await updateMenuItemSchedule(request, item.id, null, null, null);
    }
  });
});

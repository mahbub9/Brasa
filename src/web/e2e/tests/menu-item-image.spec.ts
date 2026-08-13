import { expect, test } from '@playwright/test';
import {
  deleteMenuItem,
  getMenu,
  importMenuItemsResponse,
  removeMenuItemImage,
  uploadMenuItemImage,
  uploadMenuItemImageResponse,
} from './support/api';

const origin = process.env.BRASA_API_BASE_URL ?? 'http://localhost:5216';
const adminBaseUrl = process.env.BRASA_ADMIN_BASE_URL ?? 'http://localhost:5174';

// CAT-02: menu item photos. MenuItemImageStorage writes to local disk under
// {ContentRoot}/uploads/menu-items -- an honest placeholder until a real
// tenant needs S3/Blob storage, see docs/architecture/decisions -- but the
// endpoint contract (multipart upload, GUID-named files served back under
// /uploads/menu-items via UseStaticFiles) is real. The point of this spec is
// proving the URL the API returns is actually fetchable, not just present in
// the DTO: a bug in the UseStaticFiles wiring would pass any test that only
// checked the database column.
//
// Uses a freshly-imported item, isolated from the shared seeded menu other
// specs read concurrently (2 workers) -- same reasoning as
// menu-price.spec.ts.

// A minimal valid 1x1 transparent PNG.
const TINY_PNG = Buffer.from(
  'iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mNk+A8AAQUBAScY42YAAAAASUVORK5CYII=',
  'base64',
);

async function createTestItem(request: import('@playwright/test').APIRequestContext, name: string): Promise<string> {
  const menu = await getMenu(request);
  const categoryName = menu[0].name;
  const csv = `CategoryName,Name,Price,VatRate\n${categoryName},${name},2.00,0.13\n`;

  const response = await importMenuItemsResponse(request, csv);
  expect((await response.json()).created).toBe(1);

  const item = (await getMenu(request)).flatMap((c) => c.items).find((i) => i.name === name);
  if (!item) throw new Error('Freshly-imported test item did not appear on the menu.');
  return item.id;
}

test.describe('menu item image upload (CAT-02)', () => {
  test('uploads, serves and removes a photo via the API', async ({ request }) => {
    const itemId = await createTestItem(request, `Image Test ${Date.now()}`);

    const uploaded = await uploadMenuItemImage(request, itemId, 'test.png', 'image/png', TINY_PNG);
    expect(uploaded.imageUrl).toMatch(/^\/uploads\/menu-items\/.+\.png$/);

    const fetched = await request.get(`${origin}${uploaded.imageUrl}`);
    expect(fetched.status()).toBe(200);
    expect(fetched.headers()['content-type']).toBe('image/png');

    const menuAfterUpload = await getMenu(request);
    const itemAfterUpload = menuAfterUpload.flatMap((c) => c.items).find((i) => i.id === itemId);
    expect(itemAfterUpload?.imageUrl).toBe(uploaded.imageUrl);

    const removed = await removeMenuItemImage(request, itemId);
    expect(removed.imageUrl).toBeNull();

    const goneResponse = await request.get(`${origin}${uploaded.imageUrl}`);
    expect(goneResponse.status()).toBe(404);

    await deleteMenuItem(request, itemId);
  });

  test('replacing a photo deletes the old file', async ({ request }) => {
    const itemId = await createTestItem(request, `Image Replace Test ${Date.now()}`);

    const first = await uploadMenuItemImage(request, itemId, 'first.png', 'image/png', TINY_PNG);
    const firstUrl = first.imageUrl;
    expect(firstUrl).not.toBeNull();

    const second = await uploadMenuItemImage(request, itemId, 'second.png', 'image/png', TINY_PNG);
    expect(second.imageUrl).not.toBe(firstUrl);

    const oldFileResponse = await request.get(`${origin}${firstUrl}`);
    expect(oldFileResponse.status()).toBe(404);

    const newFileResponse = await request.get(`${origin}${second.imageUrl}`);
    expect(newFileResponse.status()).toBe(200);

    await deleteMenuItem(request, itemId);
  });

  test('rejects an empty file, an oversized file, a disallowed content type, and an unknown item', async ({
    request,
  }) => {
    const itemId = await createTestItem(request, `Image Reject Test ${Date.now()}`);

    const empty = await uploadMenuItemImageResponse(request, itemId, 'empty.png', 'image/png', Buffer.alloc(0));
    expect(empty.status()).toBe(400);
    expect((await empty.json()).code).toBe('catalog.image_required');

    const oversized = Buffer.alloc(5 * 1024 * 1024 + 1);
    const tooLarge = await uploadMenuItemImageResponse(request, itemId, 'huge.png', 'image/png', oversized);
    expect(tooLarge.status()).toBe(400);
    expect((await tooLarge.json()).code).toBe('catalog.image_too_large');

    const wrongType = await uploadMenuItemImageResponse(request, itemId, 'test.gif', 'image/gif', TINY_PNG);
    expect(wrongType.status()).toBe(400);
    expect((await wrongType.json()).code).toBe('catalog.invalid_image_type');

    const unknown = await uploadMenuItemImageResponse(request, crypto.randomUUID(), 'test.png', 'image/png', TINY_PNG);
    expect(unknown.status()).toBe(404);
    expect((await unknown.json()).code).toBe('catalog.item_not_found');

    await deleteMenuItem(request, itemId);
  });

  test('removing an image that was never set is a no-op, not an error', async ({ request }) => {
    const itemId = await createTestItem(request, `Image Noop Test ${Date.now()}`);

    const removed = await removeMenuItemImage(request, itemId);
    expect(removed.imageUrl).toBeNull();

    await deleteMenuItem(request, itemId);
  });

  test('uploads and removes a photo from the admin UI', async ({ page, request }) => {
    const itemName = `Admin Image UI Test ${Date.now()}`;
    const csv = `CategoryName,Name,Price,VatRate\nEntradas,${itemName},3.00,0.13\n`;
    const importResponse = await importMenuItemsResponse(request, csv);
    expect((await importResponse.json()).created).toBe(1);

    await page.goto(adminBaseUrl);
    await page.getByTestId('nav-menu').click();
    await page.waitForSelector('.menu-manager');

    const row = page.getByTestId(`item-${itemName}`);
    await row.scrollIntoViewIfNeeded();

    await page.getByTestId(`upload-image-${itemName}`).setInputFiles({
      name: 'test.png',
      mimeType: 'image/png',
      buffer: TINY_PNG,
    });

    await expect(page.getByTestId(`item-image-${itemName}`)).toBeVisible();

    await page.getByTestId(`remove-image-${itemName}`).click();
    // The native <input type="file"> stays display:none by design -- the
    // styled label wrapping it is the actual click target (App.css) -- so
    // reverting to the upload control means the thumbnail is gone and the
    // (hidden but present) input is back, not that the input itself is visible.
    await expect(page.getByTestId(`item-image-${itemName}`)).toHaveCount(0);
    await expect(page.getByTestId(`upload-image-${itemName}`)).toBeAttached();

    await page.getByTestId(`delete-item-${itemName}`).click();
    await page.getByTestId(`delete-confirm-${itemName}`).click();
    await expect(row).toHaveCount(0);
  });
});

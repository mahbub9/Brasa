import { expect, test } from '@playwright/test';
import {
  createOrganization,
  createOrganizationResponse,
  createSite,
  createSiteResponse,
  createTerminal,
  createTerminalResponse,
  getOrganizations,
  getSites,
  getSitesResponse,
  getTerminals,
  getTerminalsResponse,
} from './support/api';

// IDN-01 — a narrow first slice of the Identity epic: Organization / Site /
// Terminal as real, referenceable rows, with stable ids CAT-05 (price lists
// per site) and FLR-06 (waiter section assignment) can key by once built.
// Create and list only, no auth/pairing (IDN-06/07 stay untouched) — see
// that endpoint file's own remarks. DevIdentitySeeder seeds one full chain
// ("Brasa Demo, Lda" → "Restaurante Central" → "Caixa 1") on startup, the
// same role DevFloorSeeder plays for the floor plan; these specs both read
// that seeded chain and create their own throwaway rows, since there's no
// delete endpoint yet to clean up after — harmless, nothing here asserts an
// exact count, only that a specific known row is present.

test.describe('organization / site / terminal (IDN-01)', () => {
  test('creates and lists organizations, alongside the seeded demo one', async ({ request }) => {
    const name = `Test Org ${Date.now()}`;
    const created = await createOrganization(request, name);
    expect(created.name).toBe(name);

    const organizations = await getOrganizations(request);
    expect(organizations.some((o) => o.id === created.id && o.name === name)).toBe(true);
    expect(organizations.some((o) => o.name === 'Brasa Demo, Lda')).toBe(true);
  });

  test('rejects an empty organization name', async ({ request }) => {
    const response = await createOrganizationResponse(request, '   ');
    expect(response.status()).toBe(400);
    expect((await response.json()).code).toBe('identity.invalid_organization_name');
  });

  test('creates and lists sites under an organization, with the region round-tripping', async ({ request }) => {
    const organization = await createOrganization(request, `Test Org ${Date.now()}`);
    const siteName = `Test Site ${Date.now()}`;
    const created = await createSite(request, organization.id, siteName, 'Azores');
    expect(created.organizationId).toBe(organization.id);
    expect(created.region).toBe('Azores');

    const sites = await getSites(request, organization.id);
    expect(sites).toEqual([{ id: created.id, organizationId: organization.id, name: siteName, region: 'Azores' }]);
  });

  test('rejects an empty site name, an unrecognised region, and an unknown organization', async ({ request }) => {
    const organization = await createOrganization(request, `Test Org ${Date.now()}`);

    const emptyName = await createSiteResponse(request, organization.id, '  ', 'Continental');
    expect(emptyName.status()).toBe(400);
    expect((await emptyName.json()).code).toBe('identity.invalid_site_name');

    const badRegion = await createSiteResponse(request, organization.id, 'Somewhere', 'Narnia');
    expect(badRegion.status()).toBe(400);
    expect((await badRegion.json()).code).toBe('identity.invalid_region');

    const unknownOrg = await createSiteResponse(request, crypto.randomUUID(), 'Somewhere', 'Continental');
    expect(unknownOrg.status()).toBe(404);
    expect((await unknownOrg.json()).code).toBe('identity.organization_not_found');

    const unknownOrgList = await getSitesResponse(request, crypto.randomUUID());
    expect(unknownOrgList.status()).toBe(404);
    expect((await unknownOrgList.json()).code).toBe('identity.organization_not_found');
  });

  test('creates and lists terminals at a site', async ({ request }) => {
    const organization = await createOrganization(request, `Test Org ${Date.now()}`);
    const site = await createSite(request, organization.id, `Test Site ${Date.now()}`, 'Madeira');
    const label = `Terminal ${Date.now()}`;

    const created = await createTerminal(request, site.id, label);
    expect(created.siteId).toBe(site.id);
    expect(created.label).toBe(label);

    const terminals = await getTerminals(request, site.id);
    expect(terminals).toEqual([{ id: created.id, siteId: site.id, label }]);
  });

  test('rejects an empty terminal label and an unknown site', async ({ request }) => {
    const organization = await createOrganization(request, `Test Org ${Date.now()}`);
    const site = await createSite(request, organization.id, `Test Site ${Date.now()}`, 'Continental');

    const emptyLabel = await createTerminalResponse(request, site.id, '   ');
    expect(emptyLabel.status()).toBe(400);
    expect((await emptyLabel.json()).code).toBe('identity.invalid_terminal_label');

    const unknownSite = await createTerminalResponse(request, crypto.randomUUID(), 'Caixa 2');
    expect(unknownSite.status()).toBe(404);
    expect((await unknownSite.json()).code).toBe('identity.site_not_found');

    const unknownSiteList = await getTerminalsResponse(request, crypto.randomUUID());
    expect(unknownSiteList.status()).toBe(404);
    expect((await unknownSiteList.json()).code).toBe('identity.site_not_found');
  });

  test('the seeded demo chain resolves end to end: organization -> site -> terminal', async ({ request }) => {
    const organizations = await getOrganizations(request);
    const demo = organizations.find((o) => o.name === 'Brasa Demo, Lda');
    expect(demo).toBeDefined();

    const sites = await getSites(request, demo!.id);
    const central = sites.find((s) => s.name === 'Restaurante Central');
    expect(central).toBeDefined();
    expect(central!.region).toBe('Continental');

    const terminals = await getTerminals(request, central!.id);
    expect(terminals.some((t) => t.label === 'Caixa 1')).toBe(true);
  });
});

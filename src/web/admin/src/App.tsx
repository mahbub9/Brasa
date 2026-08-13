import { LanguageToggle } from '@brasa/ui/components/LanguageToggle';
import { useCallback, useEffect, useState } from 'react';
import { useTranslation } from 'react-i18next';
import { api, ApiError } from './api/client';
import type { AdminMenuCategoryDto, RoomDto } from './api/types';
import { FloorManager } from './components/FloorManager';
import { MenuManager } from './components/MenuManager';
import { StaffManager } from './components/StaffManager';
import i18n from './i18n/i18n';
import './App.css';

// Every nav entry is live now — "staff" was the last placeholder. Kept as
// its own list (not inlined into the JSX below) since a future section
// will likely need the "labelled but not live yet" branch again.
const NAV_KEYS = ['overview', 'menu', 'floor', 'staff'] as const;
type Section = (typeof NAV_KEYS)[number];

/**
 * The back-office shell (WEB-09), now with four real editors: WEB-10's menu
 * slice, FLR-03's first slice — table CRUD (add/edit/delete), not yet the
 * drag-and-drop canvas that task's own title names — and a staff screen
 * (IDN-08/09, WEB-11's own staff half). No site-selector exists anywhere in
 * either client yet, so "staff" assumes the first organization's first
 * site — the same single-site shortcut every other screen here already
 * takes, just made explicit for this one since `GET /sites/{id}/staff`
 * actually needs a site id to call at all.
 */
export default function App() {
  const { t } = useTranslation();
  const [section, setSection] = useState<Section>('overview');
  const [categories, setCategories] = useState<AdminMenuCategoryDto[] | null>(null);
  const [rooms, setRooms] = useState<RoomDto[] | null>(null);
  const [siteId, setSiteId] = useState<string | null>(null);
  const [error, setError] = useState<string | null>(null);

  const loadCatalog = useCallback(() => {
    api.getMenu().then(setCategories).catch((err: unknown) => {
      setError(err instanceof ApiError ? err.message : i18n.t('error.generic'));
    });
  }, []);

  const loadFloor = useCallback(() => {
    api.getFloor().then(setRooms).catch((err: unknown) => {
      setError(err instanceof ApiError ? err.message : i18n.t('error.generic'));
    });
  }, []);

  useEffect(() => {
    loadCatalog();
    loadFloor();

    api
      .getOrganizations()
      .then((organizations) => {
        const first = organizations[0];
        if (!first) {
          return null;
        }
        return api.getSites(first.id);
      })
      .then((sites) => setSiteId(sites?.[0]?.id ?? null))
      .catch((err: unknown) => {
        setError(err instanceof ApiError ? err.message : i18n.t('error.generic'));
      });
  }, [loadCatalog, loadFloor]);

  return (
    <div className="admin">
      <header className="admin-header">
        <span className="admin-brand">Brasa</span>
        <span className="admin-tagline">{t('app.tagline')}</span>
        <LanguageToggle />
      </header>

      <div className="admin-layout">
        <nav className="admin-nav" aria-label={t('nav.overview')}>
          {NAV_KEYS.map((key) => (
            <button
              key={key}
              type="button"
              className={key === section ? 'admin-nav-item active' : 'admin-nav-item'}
              data-testid={`nav-${key}`}
              onClick={() => setSection(key)}
            >
              {t(`nav.${key}`)}
            </button>
          ))}
        </nav>

        <main className="admin-main">
          <h1>{t(`nav.${section}`)}</h1>

          {error && (
            <p className="admin-error" role="alert" data-testid="overview-error">
              {error}
            </p>
          )}

          {!error && (!categories || !rooms) && <p className="admin-loading">{t('app.loading')}</p>}

          {categories && rooms && section === 'overview' && <Overview categories={categories} rooms={rooms} />}
          {categories && section === 'menu' && (
            <MenuManager categories={categories} onReload={loadCatalog} onErrorChange={setError} />
          )}
          {rooms && section === 'floor' && (
            <FloorManager rooms={rooms} onReload={loadFloor} onErrorChange={setError} />
          )}
          {section === 'staff' && <StaffManager siteId={siteId} onErrorChange={setError} />}
        </main>
      </div>
    </div>
  );
}

interface OverviewProps {
  categories: AdminMenuCategoryDto[];
  rooms: RoomDto[];
}

function Overview({ categories, rooms }: OverviewProps) {
  const { t } = useTranslation();
  const hiddenCategoryCount = categories.filter((category) => !category.isVisible).length;
  const itemCount = categories.reduce((sum, category) => sum + category.items.length, 0);
  const unavailableCount = categories.reduce(
    (sum, category) => sum + category.items.filter((item) => !item.isAvailable).length,
    0,
  );
  const tables = rooms.flatMap((room) => room.tables);
  const occupiedCount = tables.filter((table) => table.state === 'Occupied' || table.state === 'BillRequested').length;

  return (
    <div className="overview-cards" data-testid="overview-cards">
      <article className="overview-card">
        <span className="overview-card-value" data-testid="overview-categories">
          {categories.length}
        </span>
        <span className="overview-card-label">
          {t('overview.categories')}
          {hiddenCategoryCount > 0 && ` · ${t('overview.categoriesHidden', { count: hiddenCategoryCount })}`}
        </span>
      </article>

      <article className="overview-card">
        <span className="overview-card-value" data-testid="overview-items">
          {itemCount}
        </span>
        <span className="overview-card-label">
          {t('overview.items')}
          {unavailableCount > 0 && ` · ${t('overview.itemsUnavailable', { count: unavailableCount })}`}
        </span>
      </article>

      <article className="overview-card">
        <span className="overview-card-value" data-testid="overview-rooms">
          {rooms.length}
        </span>
        <span className="overview-card-label">{t('overview.rooms')}</span>
      </article>

      <article className="overview-card">
        <span className="overview-card-value" data-testid="overview-tables">
          {tables.length}
        </span>
        <span className="overview-card-label">
          {t('overview.tables')}
          {tables.length > 0 && ` · ${t('overview.tablesOccupied', { count: occupiedCount })}`}
        </span>
      </article>
    </div>
  );
}

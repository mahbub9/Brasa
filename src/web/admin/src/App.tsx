import { useCallback, useEffect, useState } from 'react';
import { useTranslation } from 'react-i18next';
import { api, ApiError } from './api/client';
import type { AdminMenuCategoryDto, RoomDto } from './api/types';
import { LanguageToggle } from './components/LanguageToggle';
import { MenuManager } from './components/MenuManager';
import i18n from './i18n/i18n';
import './App.css';

const LIVE_SECTIONS = ['overview', 'menu'] as const;
const NAV_KEYS = ['overview', 'menu', 'floor', 'staff'] as const;
type Section = (typeof LIVE_SECTIONS)[number];

function isLiveSection(key: string): key is Section {
  return (LIVE_SECTIONS as readonly string[]).includes(key);
}

/**
 * The back-office shell (WEB-09), now with its first real editor (WEB-10's
 * menu slice — floor-plan editing is separate, not built here). "Overview"
 * and "Menu" are both live; Floor plan/Staff stay labelled placeholders
 * until their own tasks land — see docs/product/backlog.md (WEB-10/11).
 */
export default function App() {
  const { t } = useTranslation();
  const [section, setSection] = useState<Section>('overview');
  const [categories, setCategories] = useState<AdminMenuCategoryDto[] | null>(null);
  const [rooms, setRooms] = useState<RoomDto[] | null>(null);
  const [error, setError] = useState<string | null>(null);

  const loadCatalog = useCallback(() => {
    api.getMenu().then(setCategories).catch((err: unknown) => {
      setError(err instanceof ApiError ? err.message : i18n.t('error.generic'));
    });
  }, []);

  useEffect(() => {
    loadCatalog();
    api.getFloor().then(setRooms).catch((err: unknown) => {
      setError(err instanceof ApiError ? err.message : i18n.t('error.generic'));
    });
  }, [loadCatalog]);

  return (
    <div className="admin">
      <header className="admin-header">
        <span className="admin-brand">Brasa</span>
        <span className="admin-tagline">{t('app.tagline')}</span>
        <LanguageToggle />
      </header>

      <div className="admin-layout">
        <nav className="admin-nav" aria-label={t('nav.overview')}>
          {NAV_KEYS.map((key) =>
            isLiveSection(key) ? (
              <button
                key={key}
                type="button"
                className={key === section ? 'admin-nav-item active' : 'admin-nav-item'}
                data-testid={`nav-${key}`}
                onClick={() => setSection(key)}
              >
                {t(`nav.${key}`)}
              </button>
            ) : (
              <span key={key} className="admin-nav-item disabled" data-testid={`nav-${key}`}>
                {t(`nav.${key}`)}
                <span className="admin-nav-soon">{t('nav.comingSoon')}</span>
              </span>
            ),
          )}
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

import { useEffect, useState } from 'react';
import { useTranslation } from 'react-i18next';
import { api, ApiError } from './api/client';
import type { MenuCategoryDto, RoomDto } from './api/types';
import { LanguageToggle } from './components/LanguageToggle';
import i18n from './i18n/i18n';
import './App.css';

const NAV_KEYS = ['overview', 'menu', 'floor', 'staff'] as const;

/**
 * The back-office shell (WEB-09) — scaffolding for the editors WEB-10/11
 * will add, not an editor itself. The only screen that's live today,
 * "Overview", is read-only: it proves the shell is really wired to the
 * API (not a static mock) by rendering real counts from GET /menu and
 * GET /floor. Every other nav entry is a labelled placeholder until its
 * own task lands — see docs/product/backlog.md (WEB-10/11).
 */
export default function App() {
  const { t } = useTranslation();
  const [categories, setCategories] = useState<MenuCategoryDto[] | null>(null);
  const [rooms, setRooms] = useState<RoomDto[] | null>(null);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    Promise.all([api.getMenu(), api.getFloor()])
      .then(([menu, floor]) => {
        setCategories(menu);
        setRooms(floor);
      })
      .catch((err: unknown) => {
        setError(err instanceof ApiError ? err.message : i18n.t('error.generic'));
      });
  }, []);

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
            <span
              key={key}
              className={key === 'overview' ? 'admin-nav-item active' : 'admin-nav-item disabled'}
              data-testid={`nav-${key}`}
            >
              {t(`nav.${key}`)}
              {key !== 'overview' && <span className="admin-nav-soon">{t('nav.comingSoon')}</span>}
            </span>
          ))}
        </nav>

        <main className="admin-main">
          <h1>{t('nav.overview')}</h1>

          {error && (
            <p className="admin-error" role="alert" data-testid="overview-error">
              {error}
            </p>
          )}

          {!error && (!categories || !rooms) && <p className="admin-loading">{t('app.loading')}</p>}

          {categories && rooms && <Overview categories={categories} rooms={rooms} />}
        </main>
      </div>
    </div>
  );
}

interface OverviewProps {
  categories: MenuCategoryDto[];
  rooms: RoomDto[];
}

function Overview({ categories, rooms }: OverviewProps) {
  const { t } = useTranslation();
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
        <span className="overview-card-label">{t('overview.categories')}</span>
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

import { useEffect, useState } from 'react';
import { api, ApiError } from './api/client';
import type { MenuCategoryDto, RoomDto } from './api/types';
import './App.css';

interface NavSection {
  key: string;
  label: string;
}

const NAV_SECTIONS: NavSection[] = [
  { key: 'overview', label: 'Visão geral' },
  { key: 'menu', label: 'Menu' },
  { key: 'floor', label: 'Sala' },
  { key: 'staff', label: 'Equipa' },
];

/**
 * The back-office shell (WEB-09) — scaffolding for the editors WEB-10/11
 * will add, not an editor itself. The only screen that's live today,
 * "Visão geral", is read-only: it proves the shell is really wired to the
 * API (not a static mock) by rendering real counts from GET /menu and
 * GET /floor. Every other nav entry is a labelled placeholder until its
 * own task lands — see docs/product/backlog.md (WEB-10/11).
 */
export default function App() {
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
        setError(err instanceof ApiError ? err.message : 'Something went wrong.');
      });
  }, []);

  return (
    <div className="admin">
      <header className="admin-header">
        <span className="admin-brand">Brasa</span>
        <span className="admin-tagline">Back office</span>
      </header>

      <div className="admin-layout">
        <nav className="admin-nav" aria-label="Secções">
          {NAV_SECTIONS.map((section) => (
            <span
              key={section.key}
              className={section.key === 'overview' ? 'admin-nav-item active' : 'admin-nav-item disabled'}
              data-testid={`nav-${section.key}`}
            >
              {section.label}
              {section.key !== 'overview' && <span className="admin-nav-soon">Brevemente</span>}
            </span>
          ))}
        </nav>

        <main className="admin-main">
          <h1>Visão geral</h1>

          {error && (
            <p className="admin-error" role="alert" data-testid="overview-error">
              {error}
            </p>
          )}

          {!error && (!categories || !rooms) && <p className="admin-loading">A carregar…</p>}

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
        <span className="overview-card-label">Categorias de menu</span>
      </article>

      <article className="overview-card">
        <span className="overview-card-value" data-testid="overview-items">
          {itemCount}
        </span>
        <span className="overview-card-label">
          Itens no menu
          {unavailableCount > 0 && ` · ${unavailableCount} indisponíveis`}
        </span>
      </article>

      <article className="overview-card">
        <span className="overview-card-value" data-testid="overview-rooms">
          {rooms.length}
        </span>
        <span className="overview-card-label">Salas</span>
      </article>

      <article className="overview-card">
        <span className="overview-card-value" data-testid="overview-tables">
          {tables.length}
        </span>
        <span className="overview-card-label">
          Mesas
          {tables.length > 0 && ` · ${occupiedCount} ocupadas`}
        </span>
      </article>
    </div>
  );
}

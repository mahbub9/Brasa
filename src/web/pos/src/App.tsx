import { useEffect, useState } from 'react';
import { useTranslation } from 'react-i18next';
import { api, ApiError } from './api/client';
import type { CloseOrderResponse, MenuCategoryDto, MoneyDto, OrderDto, RoomDto } from './api/types';
import { ErrorBanner } from './components/ErrorBanner';
import i18n from './i18n/i18n';
import { LanguageToggle } from './components/LanguageToggle';
import { MenuGrid } from './components/MenuGrid';
import { OrderSummary } from './components/OrderSummary';
import { Receipt } from './components/Receipt';
import { TablePicker } from './components/TablePicker';
import './App.css';

/**
 * The walking-skeleton POS: pick a real table off the floor plan, ring up
 * items from the live menu, preview a split, close and see the fiscal
 * document. One screen, one tenant (DevTenantMiddleware), no auth — see
 * docs/product/status.md.
 */
export default function App() {
  const { t } = useTranslation();
  const [floor, setFloor] = useState<RoomDto[] | null>(null);
  const [menu, setMenu] = useState<MenuCategoryDto[] | null>(null);
  const [order, setOrder] = useState<OrderDto | null>(null);
  const [closeResult, setCloseResult] = useState<CloseOrderResponse | null>(null);
  const [splitParts, setSplitParts] = useState(2);
  const [splitAmounts, setSplitAmounts] = useState<MoneyDto[] | null>(null);
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    api.getMenu().then(setMenu).catch((err) => setError(describeError(err)));
    loadFloor();
  }, []);

  function loadFloor() {
    api.getFloor().then(setFloor).catch((err) => setError(describeError(err)));
  }

  async function handleOpenTable(tableId: string, coverCount: number) {
    setBusy(true);
    setError(null);
    try {
      setOrder(await api.openOrder({ tableId, coverCount }));
    } catch (err) {
      setError(describeError(err));
      loadFloor(); // someone else may have just occupied it — refresh state
    } finally {
      setBusy(false);
    }
  }

  async function handleClearTable(tableId: string) {
    setBusy(true);
    setError(null);
    try {
      await api.clearTable(tableId);
      loadFloor();
    } catch (err) {
      setError(describeError(err));
    } finally {
      setBusy(false);
    }
  }

  async function handleAddItem(menuItemId: string) {
    if (!order) return;
    setBusy(true);
    setError(null);
    try {
      setOrder(await api.addLine(order.id, { menuItemId, quantity: 1 }));
      setSplitAmounts(null);
    } catch (err) {
      setError(describeError(err));
    } finally {
      setBusy(false);
    }
  }

  async function handlePreviewSplit() {
    if (!order) return;
    setBusy(true);
    setError(null);
    try {
      setSplitAmounts(await api.previewSplit(order.id, splitParts));
    } catch (err) {
      setError(describeError(err));
    } finally {
      setBusy(false);
    }
  }

  async function handleCloseOrder() {
    if (!order) return;
    setBusy(true);
    setError(null);
    try {
      setCloseResult(await api.closeOrder(order.id));
    } catch (err) {
      setError(describeError(err));
    } finally {
      setBusy(false);
    }
  }

  function handleNewTable() {
    setOrder(null);
    setCloseResult(null);
    setSplitAmounts(null);
    setSplitParts(2);
    loadFloor();
  }

  return (
    <div className="app">
      <header className="app-header">
        <span className="app-brand">Brasa</span>
        <span className="app-tagline">{t('app.tagline')}</span>
        <LanguageToggle />
      </header>

      {error && <ErrorBanner message={error} onDismiss={() => setError(null)} />}

      <main>
        {closeResult ? (
          <Receipt result={closeResult} onNewTable={handleNewTable} />
        ) : order ? (
          <div className="ordering-layout">
            <MenuGrid categories={menu ?? []} onAddItem={handleAddItem} disabled={busy} />
            <OrderSummary
              order={order}
              splitParts={splitParts}
              onSplitPartsChange={setSplitParts}
              splitAmounts={splitAmounts}
              onPreviewSplit={handlePreviewSplit}
              onClose={handleCloseOrder}
              busy={busy}
            />
          </div>
        ) : (
          <TablePicker
            rooms={floor ?? []}
            busy={busy}
            onOpenTable={handleOpenTable}
            onClearTable={handleClearTable}
          />
        )}
      </main>
    </div>
  );
}

function describeError(err: unknown): string {
  if (err instanceof ApiError) {
    return err.code ? `${err.message} (${err.code})` : err.message;
  }
  return err instanceof Error ? err.message : i18n.t('error.generic');
}

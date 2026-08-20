import { BrandMark } from '@brasa/ui/components/BrandMark';
import { LanguageToggle } from '@brasa/ui/components/LanguageToggle';
import { useEffect, useState } from 'react';
import { useTranslation } from 'react-i18next';
import { api } from './api/client';
import { connectFloorHub } from './api/floorHub';
import type {
  CloseOrderResponse,
  MenuCategoryDto,
  MenuItemDto,
  MoneyDto,
  OrderDto,
  PreBillDto,
  RoomDto,
  StaffDto,
} from './api/types';
import { CouvertBar } from './components/CouvertBar';
import { ErrorBanner } from './components/ErrorBanner';
import { MenuGrid } from './components/MenuGrid';
import { ModifierPicker } from './components/ModifierPicker';
import { OrderSummary } from './components/OrderSummary';
import { PreBill } from './components/PreBill';
import { Receipt } from './components/Receipt';
import { StaffLogin } from './components/StaffLogin';
import { TablePicker } from './components/TablePicker';
import { TransferTablePicker } from './components/TransferTablePicker';
import { describeError } from './lib/describeError';
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
  const [preBill, setPreBill] = useState<PreBillDto | null>(null);
  const [transferPicker, setTransferPicker] = useState<RoomDto[] | null>(null);
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [pickerItem, setPickerItem] = useState<MenuItemDto | null>(null);
  const [siteId, setSiteId] = useState<string | null>(null);
  const [staffList, setStaffList] = useState<StaffDto[] | null>(null);
  const [currentStaff, setCurrentStaff] = useState<StaffDto | null>(null);

  useEffect(() => {
    api.getMenu().then(setMenu).catch((err) => setError(describeError(err)));
    loadFloor();

    // API-16: another terminal opening/clearing/transferring a table shows
    // up here without a manual refresh or a poll -- the floor picker was
    // the whole reason this codebase's first realtime channel exists.
    const floorHub = connectFloorHub(loadFloor);

    // WEB-07 -- same "first organization's first site" shortcut `admin`
    // already takes; no site-selector exists in either client. Signing in
    // doesn't gate anything here, so a failure to resolve a site just
    // means no login is offered, not a broken app.
    api
      .getOrganizations()
      .then((organizations) => {
        const first = organizations[0];
        if (!first) return null;
        return api.getSites(first.id);
      })
      .then((sites) => setSiteId(sites?.[0]?.id ?? null))
      .catch(() => setSiteId(null));

    return () => {
      void floorHub.stop();
    };
  }, []);

  useEffect(() => {
    if (!siteId) return;
    api.getStaff(siteId).then(setStaffList).catch(() => setStaffList(null));
  }, [siteId]);

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

  async function handleOpenTakeaway(label: string) {
    setBusy(true);
    setError(null);
    try {
      // Send a language-appropriate default ourselves rather than `null` —
      // the API has no notion of the caller's UI language (see ADR 0011's
      // "Server-sent error text" gap), so its own "Levantamento" default
      // would leak through untranslated in English mode.
      const trimmed = label.trim();
      setOrder(await api.openTakeawayOrder({ label: trimmed === '' ? t('floor.takeawayDefaultLabel') : trimmed }));
    } catch (err) {
      setError(describeError(err));
    } finally {
      setBusy(false);
    }
  }

  async function handleReenterTable(tableId: string) {
    setBusy(true);
    setError(null);
    try {
      const [openOrder] = await api.findOpenOrderForTable(tableId);
      if (!openOrder) {
        // The floor plan's own state was stale (someone closed/cleared
        // this table from another terminal since it last loaded) --
        // refresh rather than get stuck on a table picker that still
        // shows it as occupied.
        setError(t('floor.reenterNotFound'));
        loadFloor();
        return;
      }
      setOrder(await api.getOrder(openOrder.id));
    } catch (err) {
      setError(describeError(err));
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

  function handleSelectItem(item: MenuItemDto) {
    if (item.modifierGroups.length > 0) {
      setPickerItem(item);
      return;
    }
    void addLine(item.id, []);
  }

  async function handleConfirmModifiers(selectedModifierIds: string[]) {
    if (!pickerItem) return;
    await addLine(pickerItem.id, selectedModifierIds);
    setPickerItem(null);
  }

  async function addLine(menuItemId: string, selectedModifierIds: string[], quantity = 1) {
    if (!order) return;
    setBusy(true);
    setError(null);
    try {
      setOrder(await api.addLine(order.id, { menuItemId, quantity, selectedModifierIds }));
      setSplitAmounts(null);
    } catch (err) {
      setError(describeError(err));
    } finally {
      setBusy(false);
    }
  }

  function handleAddCouvert(item: MenuItemDto) {
    if (!order) return;
    void addLine(item.id, [], order.coverCount);
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

  async function handleSetLineNotes(lineId: string, notes: string | null) {
    if (!order) return;
    setBusy(true);
    setError(null);
    try {
      setOrder(await api.setLineNotes(order.id, lineId, { notes }));
    } catch (err) {
      setError(describeError(err));
    } finally {
      setBusy(false);
    }
  }

  async function handleSetLineQuantity(lineId: string, quantity: number) {
    if (!order) return;
    setBusy(true);
    setError(null);
    try {
      setOrder(await api.setLineQuantity(order.id, lineId, { quantity }));
      setSplitAmounts(null);
    } catch (err) {
      setError(describeError(err));
    } finally {
      setBusy(false);
    }
  }

  async function handleFireLines(course: string | null) {
    if (!order) return;
    setBusy(true);
    setError(null);
    try {
      setOrder(await api.fireLines(order.id, { course }));
    } catch (err) {
      setError(describeError(err));
    } finally {
      setBusy(false);
    }
  }

  async function handlePreBill() {
    if (!order) return;
    setBusy(true);
    setError(null);
    try {
      setPreBill(await api.getPreBill(order.id));
    } catch (err) {
      setError(describeError(err));
    } finally {
      setBusy(false);
    }
  }

  async function handleRequestBill() {
    if (!order) return;
    setBusy(true);
    setError(null);
    try {
      await api.requestBill(order.tableId);
      loadFloor(); // visible next time the floor plan is shown, not on this screen
    } catch (err) {
      setError(describeError(err));
    } finally {
      setBusy(false);
    }
  }

  async function handleOpenTransferPicker() {
    setBusy(true);
    setError(null);
    try {
      // Re-fetched fresh rather than reusing `floor` state, which was last
      // loaded before this table was even opened and may be stale.
      setTransferPicker(await api.getFloor());
    } catch (err) {
      setError(describeError(err));
    } finally {
      setBusy(false);
    }
  }

  async function handleTransferTable(newTableId: string) {
    if (!order) return;
    setBusy(true);
    setError(null);
    try {
      setOrder(await api.transferOrder(order.id, { newTableId }));
      setTransferPicker(null);
    } catch (err) {
      setError(describeError(err));
      // Someone else may have just taken that table — refresh the picker's list.
      try {
        setTransferPicker(await api.getFloor());
      } catch {
        setTransferPicker(null);
      }
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

  // Leaves whatever's currently on screen (an in-progress order, the
  // just-issued receipt, an open modifier/pre-bill/transfer modal) and
  // returns to the floor plan -- the one "go back" a waiter always has,
  // reused for both OrderSummary's own back button and the header brand
  // (clicking "Brasa" doubles as "take me home" everywhere in this app).
  // Never discards the order itself: it stays open server-side exactly as
  // findOpenOrderForTable/handleReenterTable re-finds it later.
  function handleBackToFloor() {
    setOrder(null);
    setCloseResult(null);
    setSplitAmounts(null);
    setSplitParts(2);
    setPreBill(null);
    setTransferPicker(null);
    setPickerItem(null);
    setError(null);
    loadFloor();
  }

  return (
    <div className="app">
      <header className="brasa-header">
        <button
          type="button"
          className="brasa-header-lockup"
          data-testid="header-home"
          aria-label={t('app.home')}
          onClick={handleBackToFloor}
        >
          <BrandMark />
          <span className="brasa-brand">Brasa</span>
          <span className="brasa-tagline">{t('app.tagline')}</span>
        </button>
        <StaffLogin
          siteId={siteId}
          staff={staffList}
          currentStaff={currentStaff}
          onSignedIn={setCurrentStaff}
          onSignOut={() => setCurrentStaff(null)}
        />
        <LanguageToggle />
      </header>

      {error && <ErrorBanner message={error} onDismiss={() => setError(null)} />}

      <main>
        {closeResult ? (
          <Receipt result={closeResult} onNewTable={handleBackToFloor} currentStaff={currentStaff} />
        ) : order ? (
          <div className="ordering-layout">
            <div className="menu-column">
              {!order.isTakeaway && (
                <CouvertBar
                  items={(menu ?? []).flatMap((c) => c.items).filter((item) => item.isCouvert)}
                  coverCount={order.coverCount}
                  disabled={busy}
                  onAdd={handleAddCouvert}
                />
              )}
              <MenuGrid categories={menu ?? []} onSelectItem={handleSelectItem} disabled={busy} isTakeaway={order.isTakeaway} />
            </div>
            <OrderSummary
              order={order}
              splitParts={splitParts}
              onSplitPartsChange={setSplitParts}
              splitAmounts={splitAmounts}
              onPreviewSplit={handlePreviewSplit}
              onSetLineNotes={handleSetLineNotes}
              onSetLineQuantity={handleSetLineQuantity}
              onFireLines={handleFireLines}
              onPreBill={handlePreBill}
              onRequestBill={handleRequestBill}
              onTransferTable={handleOpenTransferPicker}
              onClose={handleCloseOrder}
              onBack={handleBackToFloor}
              busy={busy}
            />
          </div>
        ) : (
          <TablePicker
            rooms={floor ?? []}
            busy={busy}
            onOpenTable={handleOpenTable}
            onClearTable={handleClearTable}
            onOpenTakeaway={handleOpenTakeaway}
            onReenterTable={handleReenterTable}
          />
        )}
      </main>

      {pickerItem && (
        <ModifierPicker
          item={pickerItem}
          busy={busy}
          onConfirm={handleConfirmModifiers}
          onCancel={() => setPickerItem(null)}
        />
      )}

      {preBill && <PreBill preBill={preBill} onClose={() => setPreBill(null)} />}

      {transferPicker && (
        <TransferTablePicker
          rooms={transferPicker}
          busy={busy}
          onSelect={handleTransferTable}
          onCancel={() => setTransferPicker(null)}
        />
      )}
    </div>
  );
}


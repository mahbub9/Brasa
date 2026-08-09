import { useState } from 'react';
import { useTranslation } from 'react-i18next';
import { api, ApiError } from '../api/client';
import type { AdminMenuCategoryDto, ImportMenuItemsResponse, MenuItemDto } from '../api/types';
import { formatMoney } from '../lib/money';

interface MenuManagerProps {
  categories: AdminMenuCategoryDto[];
  onReload: () => void;
  onErrorChange: (message: string | null) => void;
}

/**
 * WEB-10's menu editor slice (floor-plan editing is separate, not built
 * here). Deliberately has no "create category" or "create item" form —
 * neither endpoint exists yet (only bulk CSV import creates items), so this
 * only manages what already exists: toggle a category's visibility, 86/un-86
 * and reprice an item, delete one, or bulk-import more via the same CSV
 * pipeline CAT-17 already tested. Every mutation refetches GET /menu/all
 * (onReload) rather than reconciling local state by hand.
 */
export function MenuManager({ categories, onReload, onErrorChange }: MenuManagerProps) {
  const { t } = useTranslation();
  const [importing, setImporting] = useState(false);
  const [importResult, setImportResult] = useState<ImportMenuItemsResponse | null>(null);
  const [togglingCategoryId, setTogglingCategoryId] = useState<string | null>(null);

  async function handleImportFile(file: File) {
    setImporting(true);
    setImportResult(null);
    onErrorChange(null);
    try {
      const csv = await file.text();
      setImportResult(await api.importMenuItems(csv));
      onReload();
    } catch (err) {
      onErrorChange(err instanceof ApiError ? err.message : t('error.generic'));
    } finally {
      setImporting(false);
    }
  }

  async function toggleCategoryVisibility(category: AdminMenuCategoryDto) {
    setTogglingCategoryId(category.id);
    onErrorChange(null);
    try {
      await api.setCategoryVisibility(category.id, { isVisible: !category.isVisible });
      onReload();
    } catch (err) {
      onErrorChange(err instanceof ApiError ? err.message : t('error.generic'));
    } finally {
      setTogglingCategoryId(null);
    }
  }

  return (
    <div className="menu-manager">
      <section className="menu-import" data-testid="menu-import">
        <h2>{t('menu.importTitle')}</h2>
        <label className="menu-import-label">
          {t('menu.importChoose')}
          <input
            type="file"
            accept=".csv"
            data-testid="menu-import-input"
            disabled={importing}
            onChange={(e) => {
              const file = e.target.files?.[0];
              if (file) void handleImportFile(file);
              e.target.value = '';
            }}
          />
        </label>
        {importResult && (
          <div className="menu-import-result" data-testid="menu-import-result">
            <p>{t('menu.importCreated', { count: importResult.created })}</p>
            {importResult.errors.length > 0 && (
              <ul>
                {importResult.errors.map((rowError) => (
                  <li key={rowError.rowNumber}>
                    {t('menu.importRowError', { row: rowError.rowNumber, message: rowError.message })}
                  </li>
                ))}
              </ul>
            )}
          </div>
        )}
      </section>

      {categories.length === 0 && <p className="empty-state">{t('menu.empty')}</p>}

      {categories.map((category) => (
        <section key={category.id} className="menu-manager-category" data-testid={`category-${category.name}`}>
          <div className="menu-manager-category-heading">
            <h2>{category.name}</h2>
            <span className={category.isVisible ? 'badge badge-on' : 'badge badge-off'}>
              {category.isVisible ? t('menu.visible') : t('menu.hidden')}
            </span>
            <button
              type="button"
              data-testid={`toggle-category-${category.name}`}
              disabled={togglingCategoryId === category.id}
              onClick={() => toggleCategoryVisibility(category)}
            >
              {category.isVisible ? t('menu.hide') : t('menu.show')}
            </button>
          </div>

          {category.items.length === 0 ? (
            <p className="empty-state">{t('menu.noItems')}</p>
          ) : (
            <ul className="menu-manager-items">
              {category.items.map((item) => (
                <MenuManagerItem key={item.id} item={item} onReload={onReload} onErrorChange={onErrorChange} />
              ))}
            </ul>
          )}
        </section>
      ))}
    </div>
  );
}

interface MenuManagerItemProps {
  item: MenuItemDto;
  onReload: () => void;
  onErrorChange: (message: string | null) => void;
}

function MenuManagerItem({ item, onReload, onErrorChange }: MenuManagerItemProps) {
  const { t } = useTranslation();
  const [busy, setBusy] = useState(false);
  const [editingPrice, setEditingPrice] = useState(false);
  const [priceDraft, setPriceDraft] = useState(String(item.price.amount));
  const [editingTakeawayPrice, setEditingTakeawayPrice] = useState(false);
  const [takeawayPriceDraft, setTakeawayPriceDraft] = useState(String(item.takeawayPrice?.amount ?? ''));
  const [confirmingDelete, setConfirmingDelete] = useState(false);

  async function run(action: () => Promise<void>) {
    setBusy(true);
    onErrorChange(null);
    try {
      await action();
    } catch (err) {
      onErrorChange(err instanceof ApiError ? err.message : t('error.generic'));
    } finally {
      setBusy(false);
    }
  }

  function toggleAvailability() {
    void run(async () => {
      await api.setItemAvailability(item.id, { isAvailable: !item.isAvailable });
      onReload();
    });
  }

  function savePrice() {
    const parsed = Number(priceDraft);
    if (Number.isNaN(parsed)) {
      return;
    }
    void run(async () => {
      await api.setItemPrice(item.id, { price: parsed });
      setEditingPrice(false);
      onReload();
    });
  }

  function saveTakeawayPrice() {
    const trimmed = takeawayPriceDraft.trim();
    const parsed = trimmed === '' ? null : Number(trimmed);
    if (parsed !== null && Number.isNaN(parsed)) {
      return;
    }
    void run(async () => {
      await api.setItemTakeawayPrice(item.id, { price: parsed });
      setEditingTakeawayPrice(false);
      onReload();
    });
  }

  function clearTakeawayPrice() {
    void run(async () => {
      await api.setItemTakeawayPrice(item.id, { price: null });
      onReload();
    });
  }

  function confirmDelete() {
    void run(async () => {
      await api.deleteItem(item.id);
      onReload();
    });
  }

  return (
    <li className="menu-manager-item" data-testid={`item-${item.name}`}>
      <div className="menu-manager-item-row">
        <span className="menu-manager-item-name">{item.name}</span>

        {editingPrice ? (
          <span className="menu-manager-item-price-edit">
            <input
              type="number"
              step="0.01"
              min="0"
              value={priceDraft}
              data-testid={`price-input-${item.name}`}
              disabled={busy}
              onChange={(e) => setPriceDraft(e.target.value)}
            />
            <button type="button" data-testid={`price-save-${item.name}`} disabled={busy} onClick={savePrice}>
              {t('common.save')}
            </button>
            <button
              type="button"
              disabled={busy}
              onClick={() => {
                setEditingPrice(false);
                setPriceDraft(String(item.price.amount));
              }}
            >
              {t('common.cancel')}
            </button>
          </span>
        ) : (
          <button
            type="button"
            className="menu-manager-item-price"
            data-testid={`price-edit-${item.name}`}
            disabled={busy}
            onClick={() => setEditingPrice(true)}
          >
            {formatMoney(item.price)}
          </button>
        )}

        {editingTakeawayPrice ? (
          <span className="menu-manager-item-price-edit">
            <input
              type="number"
              step="0.01"
              min="0"
              placeholder={t('menu.sameAsDineIn')}
              value={takeawayPriceDraft}
              data-testid={`takeaway-price-input-${item.name}`}
              disabled={busy}
              onChange={(e) => setTakeawayPriceDraft(e.target.value)}
            />
            <button
              type="button"
              data-testid={`takeaway-price-save-${item.name}`}
              disabled={busy}
              onClick={saveTakeawayPrice}
            >
              {t('common.save')}
            </button>
            <button
              type="button"
              disabled={busy}
              onClick={() => {
                setEditingTakeawayPrice(false);
                setTakeawayPriceDraft(String(item.takeawayPrice?.amount ?? ''));
              }}
            >
              {t('common.cancel')}
            </button>
          </span>
        ) : item.takeawayPrice ? (
          <span className="menu-manager-item-takeaway-price">
            <button
              type="button"
              className="menu-manager-item-price"
              data-testid={`takeaway-price-edit-${item.name}`}
              disabled={busy}
              onClick={() => setEditingTakeawayPrice(true)}
            >
              {t('menu.takeaway')}: {formatMoney(item.takeawayPrice)}
            </button>
            <button
              type="button"
              data-testid={`takeaway-price-clear-${item.name}`}
              disabled={busy}
              onClick={clearTakeawayPrice}
            >
              {t('common.clear')}
            </button>
          </span>
        ) : (
          <button
            type="button"
            className="menu-manager-item-takeaway-price-add"
            data-testid={`takeaway-price-add-${item.name}`}
            disabled={busy}
            onClick={() => setEditingTakeawayPrice(true)}
          >
            + {t('menu.addTakeawayPrice')}
          </button>
        )}

        <span className={item.isAvailable ? 'badge badge-on' : 'badge badge-off'}>
          {item.isAvailable ? t('menu.available') : t('menu.unavailable')}
        </span>
        <button
          type="button"
          data-testid={`toggle-availability-${item.name}`}
          disabled={busy}
          onClick={toggleAvailability}
        >
          {item.isAvailable ? t('menu.markUnavailable') : t('menu.markAvailable')}
        </button>

        {confirmingDelete ? (
          <span className="menu-manager-item-delete-confirm">
            {t('menu.deleteConfirm')}
            <button type="button" data-testid={`delete-confirm-${item.name}`} disabled={busy} onClick={confirmDelete}>
              {t('common.yes')}
            </button>
            <button type="button" disabled={busy} onClick={() => setConfirmingDelete(false)}>
              {t('common.no')}
            </button>
          </span>
        ) : (
          <button
            type="button"
            data-testid={`delete-item-${item.name}`}
            disabled={busy}
            onClick={() => setConfirmingDelete(true)}
          >
            {t('menu.deleteItem')}
          </button>
        )}
      </div>
      {item.description && <p className="menu-manager-item-description">{item.description}</p>}
    </li>
  );
}

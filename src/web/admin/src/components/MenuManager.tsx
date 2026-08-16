import { formatMoney } from '@brasa/ui/lib/money';
import { useState } from 'react';
import { useTranslation } from 'react-i18next';
import { Badge } from '@brasa/ui/components/Badge';
import { Button } from '@brasa/ui/components/Button';
import { TextField } from '@brasa/ui/components/TextField';
import { api, apiOrigin, ApiError } from '../api/client';
import type { AdminMenuCategoryDto, ImportMenuItemsResponse, MenuItemDto } from '../api/types';

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
      // Routed by extension, not declared MIME type — browsers report
      // inconsistent content types for .xlsx (CAT-17's Excel half).
      const result = file.name.toLowerCase().endsWith('.xlsx')
        ? await api.importMenuItemsExcel(file)
        : await api.importMenuItems(await file.text());
      setImportResult(result);
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
            accept=".csv,.xlsx"
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
            <Badge tone={category.isVisible ? 'success' : 'danger'}>
              {category.isVisible ? t('menu.visible') : t('menu.hidden')}
            </Badge>
            <Button
              variant="secondary"
              data-testid={`toggle-category-${category.name}`}
              disabled={togglingCategoryId === category.id}
              onClick={() => toggleCategoryVisibility(category)}
            >
              {category.isVisible ? t('menu.hide') : t('menu.show')}
            </Button>
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

const WEEK_DAYS = ['Monday', 'Tuesday', 'Wednesday', 'Thursday', 'Friday', 'Saturday', 'Sunday'] as const;

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
  const [editingSchedule, setEditingSchedule] = useState(false);
  const [scheduleDaysDraft, setScheduleDaysDraft] = useState<string[]>(item.schedule?.daysOfWeek ?? []);
  const [scheduleStartDraft, setScheduleStartDraft] = useState(item.schedule?.startTime ?? '');
  const [scheduleEndDraft, setScheduleEndDraft] = useState(item.schedule?.endTime ?? '');
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

  async function handleUploadImage(file: File) {
    setBusy(true);
    onErrorChange(null);
    try {
      await api.uploadItemImage(item.id, file);
      onReload();
    } catch (err) {
      onErrorChange(err instanceof ApiError ? err.message : t('error.generic'));
    } finally {
      setBusy(false);
    }
  }

  function removeImage() {
    void run(async () => {
      await api.removeItemImage(item.id);
      onReload();
    });
  }

  function toggleAvailability() {
    void run(async () => {
      await api.setItemAvailability(item.id, { isAvailable: !item.isAvailable });
      onReload();
    });
  }

  function toggleCouvert() {
    void run(async () => {
      await api.setItemCouvert(item.id, { isCouvert: !item.isCouvert });
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

  function toggleScheduleDay(day: string) {
    setScheduleDaysDraft((prev) => (prev.includes(day) ? prev.filter((d) => d !== day) : [...prev, day]));
  }

  function saveSchedule() {
    if (scheduleDaysDraft.length === 0 || !scheduleStartDraft || !scheduleEndDraft) {
      return;
    }
    void run(async () => {
      await api.setItemSchedule(item.id, {
        daysOfWeek: scheduleDaysDraft,
        startTime: scheduleStartDraft,
        endTime: scheduleEndDraft,
      });
      setEditingSchedule(false);
      onReload();
    });
  }

  function clearSchedule() {
    void run(async () => {
      await api.setItemSchedule(item.id, { daysOfWeek: null, startTime: null, endTime: null });
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

        <span className="menu-manager-item-image">
          {item.imageUrl ? (
            <>
              <img
                src={`${apiOrigin}${item.imageUrl}`}
                alt=""
                className="menu-manager-item-thumb"
                data-testid={`item-image-${item.name}`}
              />
              <Button variant="ghost" data-testid={`remove-image-${item.name}`} disabled={busy} onClick={removeImage}>
                {t('menu.removeImage')}
              </Button>
            </>
          ) : (
            <label className="menu-manager-item-image-upload">
              {t('menu.addImage')}
              <input
                type="file"
                accept="image/jpeg,image/png,image/webp"
                data-testid={`upload-image-${item.name}`}
                disabled={busy}
                onChange={(e) => {
                  const file = e.target.files?.[0];
                  if (file) void handleUploadImage(file);
                  e.target.value = '';
                }}
              />
            </label>
          )}
        </span>

        {editingPrice ? (
          <span className="menu-manager-item-price-edit">
            <TextField
              type="number"
              step="0.01"
              min="0"
              value={priceDraft}
              data-testid={`price-input-${item.name}`}
              disabled={busy}
              onChange={(e) => setPriceDraft(e.target.value)}
            />
            <Button data-testid={`price-save-${item.name}`} disabled={busy} onClick={savePrice}>
              {t('common.save')}
            </Button>
            <Button
              variant="ghost"
              disabled={busy}
              onClick={() => {
                setEditingPrice(false);
                setPriceDraft(String(item.price.amount));
              }}
            >
              {t('common.cancel')}
            </Button>
          </span>
        ) : (
          <Button
            variant="ghost"
            className="menu-manager-item-price"
            data-testid={`price-edit-${item.name}`}
            disabled={busy}
            onClick={() => setEditingPrice(true)}
          >
            {formatMoney(item.price)}
          </Button>
        )}

        {editingTakeawayPrice ? (
          <span className="menu-manager-item-price-edit">
            <TextField
              type="number"
              step="0.01"
              min="0"
              placeholder={t('menu.sameAsDineIn')}
              value={takeawayPriceDraft}
              data-testid={`takeaway-price-input-${item.name}`}
              disabled={busy}
              onChange={(e) => setTakeawayPriceDraft(e.target.value)}
            />
            <Button data-testid={`takeaway-price-save-${item.name}`} disabled={busy} onClick={saveTakeawayPrice}>
              {t('common.save')}
            </Button>
            <Button
              variant="ghost"
              disabled={busy}
              onClick={() => {
                setEditingTakeawayPrice(false);
                setTakeawayPriceDraft(String(item.takeawayPrice?.amount ?? ''));
              }}
            >
              {t('common.cancel')}
            </Button>
          </span>
        ) : item.takeawayPrice ? (
          <span className="menu-manager-item-takeaway-price">
            <Button
              variant="ghost"
              className="menu-manager-item-price"
              data-testid={`takeaway-price-edit-${item.name}`}
              disabled={busy}
              onClick={() => setEditingTakeawayPrice(true)}
            >
              {t('menu.takeaway')}: {formatMoney(item.takeawayPrice)}
            </Button>
            <Button
              variant="ghost"
              data-testid={`takeaway-price-clear-${item.name}`}
              disabled={busy}
              onClick={clearTakeawayPrice}
            >
              {t('common.clear')}
            </Button>
          </span>
        ) : (
          <Button
            variant="ghost"
            className="menu-manager-item-takeaway-price-add"
            data-testid={`takeaway-price-add-${item.name}`}
            disabled={busy}
            onClick={() => setEditingTakeawayPrice(true)}
          >
            + {t('menu.addTakeawayPrice')}
          </Button>
        )}

        {editingSchedule ? (
          <span className="menu-manager-item-schedule-edit" data-testid={`schedule-edit-${item.name}`}>
            {WEEK_DAYS.map((day) => (
              <label key={day} className="menu-manager-schedule-day">
                <input
                  type="checkbox"
                  data-testid={`schedule-day-${day}-${item.name}`}
                  checked={scheduleDaysDraft.includes(day)}
                  disabled={busy}
                  onChange={() => toggleScheduleDay(day)}
                />
                {t(`menu.day.${day}`)}
              </label>
            ))}
            <TextField
              type="time"
              value={scheduleStartDraft}
              data-testid={`schedule-start-${item.name}`}
              disabled={busy}
              onChange={(e) => setScheduleStartDraft(e.target.value)}
            />
            <TextField
              type="time"
              value={scheduleEndDraft}
              data-testid={`schedule-end-${item.name}`}
              disabled={busy}
              onChange={(e) => setScheduleEndDraft(e.target.value)}
            />
            <Button data-testid={`schedule-save-${item.name}`} disabled={busy} onClick={saveSchedule}>
              {t('common.save')}
            </Button>
            <Button
              variant="ghost"
              disabled={busy}
              onClick={() => {
                setEditingSchedule(false);
                setScheduleDaysDraft(item.schedule?.daysOfWeek ?? []);
                setScheduleStartDraft(item.schedule?.startTime ?? '');
                setScheduleEndDraft(item.schedule?.endTime ?? '');
              }}
            >
              {t('common.cancel')}
            </Button>
          </span>
        ) : item.schedule ? (
          <span className="menu-manager-item-schedule">
            <Button
              variant="ghost"
              className="menu-manager-item-price"
              data-testid={`schedule-edit-open-${item.name}`}
              disabled={busy}
              onClick={() => setEditingSchedule(true)}
            >
              {item.schedule.daysOfWeek.map((d) => t(`menu.day.${d}`)).join(' ')} {item.schedule.startTime}–
              {item.schedule.endTime}
            </Button>
            <Button variant="ghost" data-testid={`schedule-clear-${item.name}`} disabled={busy} onClick={clearSchedule}>
              {t('common.clear')}
            </Button>
          </span>
        ) : (
          <Button
            variant="ghost"
            className="menu-manager-item-schedule-add"
            data-testid={`schedule-add-${item.name}`}
            disabled={busy}
            onClick={() => setEditingSchedule(true)}
          >
            + {t('menu.addSchedule')}
          </Button>
        )}

        <Badge tone={item.isAvailable ? 'success' : 'danger'}>
          {item.isAvailable ? t('menu.available') : t('menu.unavailable')}
        </Badge>
        <Button
          variant="secondary"
          data-testid={`toggle-availability-${item.name}`}
          disabled={busy}
          onClick={toggleAvailability}
        >
          {item.isAvailable ? t('menu.markUnavailable') : t('menu.markAvailable')}
        </Button>

        {item.isCouvert && <Badge tone="brand">{t('menu.couvert')}</Badge>}
        <Button variant="secondary" data-testid={`toggle-couvert-${item.name}`} disabled={busy} onClick={toggleCouvert}>
          {item.isCouvert ? t('menu.unmarkCouvert') : t('menu.markCouvert')}
        </Button>

        {confirmingDelete ? (
          <span className="menu-manager-item-delete-confirm">
            {t('menu.deleteConfirm')}
            <Button variant="danger" data-testid={`delete-confirm-${item.name}`} disabled={busy} onClick={confirmDelete}>
              {t('common.yes')}
            </Button>
            <Button variant="ghost" disabled={busy} onClick={() => setConfirmingDelete(false)}>
              {t('common.no')}
            </Button>
          </span>
        ) : (
          <Button
            variant="danger"
            data-testid={`delete-item-${item.name}`}
            disabled={busy}
            onClick={() => setConfirmingDelete(true)}
          >
            {t('menu.deleteItem')}
          </Button>
        )}
      </div>
      {item.description && <p className="menu-manager-item-description">{item.description}</p>}
    </li>
  );
}

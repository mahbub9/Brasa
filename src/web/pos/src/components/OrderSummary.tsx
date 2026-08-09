import { useState } from 'react';
import { useTranslation } from 'react-i18next';
import type { MoneyDto, OrderDto, OrderLineDto } from '../api/types';
import { formatMoney } from '../lib/money';

interface OrderSummaryProps {
  order: OrderDto;
  splitParts: number;
  onSplitPartsChange: (parts: number) => void;
  splitAmounts: MoneyDto[] | null;
  onPreviewSplit: () => void;
  onSetLineNotes: (lineId: string, notes: string | null) => void;
  onPreBill: () => void;
  onRequestBill: () => void;
  onTransferTable: () => void;
  onClose: () => void;
  busy: boolean;
}

export function OrderSummary({
  order,
  splitParts,
  onSplitPartsChange,
  splitAmounts,
  onPreviewSplit,
  onSetLineNotes,
  onPreBill,
  onRequestBill,
  onTransferTable,
  onClose,
  busy,
}: OrderSummaryProps) {
  const { t } = useTranslation();

  return (
    <aside className="order-summary">
      <div className="order-summary-heading">
        <h2>{order.tableLabel}</h2>
        <button type="button" className="transfer-table-trigger" data-testid="transfer-table-button" onClick={onTransferTable} disabled={busy}>
          {t('order.transferTable')}
        </button>
      </div>
      {!order.isTakeaway && <p className="covers">{t('order.covers', { count: order.coverCount })}</p>}

      {order.lines.length === 0 ? (
        <p className="empty-state">{t('order.empty')}</p>
      ) : (
        <ul className="order-lines">
          {order.lines.map((line) => (
            <li key={line.id}>
              <div className="order-line-row">
                <span className="order-line-qty">{line.quantity}×</span>
                <span className="order-line-name">{line.itemName}</span>
                <span className="order-line-total">{formatMoney(line.lineTotal)}</span>
              </div>
              {line.modifiers.length > 0 && (
                <ul className="order-line-modifiers">
                  {line.modifiers.map((modifier) => (
                    <li key={modifier.id}>
                      {modifier.name}
                      {modifier.priceDelta.amount !== 0 && ` (+${formatMoney(modifier.priceDelta)})`}
                    </li>
                  ))}
                </ul>
              )}
              <OrderLineNotes line={line} busy={busy} onSave={onSetLineNotes} />
            </li>
          ))}
        </ul>
      )}

      <div className="order-total">
        <span>{t('order.total')}</span>
        <strong data-testid="order-total">{formatMoney(order.total)}</strong>
      </div>

      <div className="split-preview">
        <label>
          {t('order.split')}
          <input
            type="number"
            min={1}
            data-testid="split-parts-input"
            value={splitParts}
            onChange={(e) => onSplitPartsChange(Math.max(1, Number(e.target.value)))}
          />
          {t('order.splitWays')}
        </label>
        <button
          type="button"
          data-testid="preview-split-button"
          onClick={onPreviewSplit}
          disabled={busy || order.lines.length === 0}
        >
          {t('order.previewSplit')}
        </button>
        {splitAmounts && (
          <ul className="split-amounts" data-testid="split-amounts">
            {splitAmounts.map((amount, index) => (
              <li key={index}>{formatMoney(amount)}</li>
            ))}
          </ul>
        )}
      </div>

      <button
        type="button"
        className="pre-bill-trigger"
        data-testid="pre-bill-button"
        onClick={onPreBill}
        disabled={busy || order.lines.length === 0}
      >
        {t('order.preBill')}
      </button>

      {!order.isTakeaway && (
        <button
          type="button"
          className="request-bill-trigger"
          data-testid="request-bill-button"
          onClick={onRequestBill}
          disabled={busy}
        >
          {t('order.requestBill')}
        </button>
      )}

      <button
        type="button"
        className="close-order"
        data-testid="close-order-button"
        onClick={onClose}
        disabled={busy || order.lines.length === 0}
      >
        {busy ? t('order.closing') : t('order.close')}
      </button>
    </aside>
  );
}

interface OrderLineNotesProps {
  line: OrderLineDto;
  busy: boolean;
  onSave: (lineId: string, notes: string | null) => void;
}

/**
 * Free-text kitchen note per line (ORD-06), added after the line is already
 * rung up — editing a line itself isn't built yet (ORD-03: add only until
 * I2), so this is scoped narrowly to notes rather than general line editing.
 */
function OrderLineNotes({ line, busy, onSave }: OrderLineNotesProps) {
  const { t } = useTranslation();
  const [editing, setEditing] = useState(false);
  const [draft, setDraft] = useState(line.notes ?? '');

  function startEditing() {
    setDraft(line.notes ?? '');
    setEditing(true);
  }

  function save() {
    const trimmed = draft.trim();
    onSave(line.id, trimmed === '' ? null : trimmed);
    setEditing(false);
  }

  if (editing) {
    return (
      <div className="order-line-notes-edit">
        <input
          type="text"
          value={draft}
          maxLength={300}
          placeholder={t('order.notesPlaceholder')}
          data-testid={`line-notes-input-${line.id}`}
          onChange={(e) => setDraft(e.target.value)}
        />
        <button type="button" data-testid={`line-notes-save-${line.id}`} disabled={busy} onClick={save}>
          {t('order.notesSave')}
        </button>
        <button type="button" disabled={busy} onClick={() => setEditing(false)}>
          {t('order.notesCancel')}
        </button>
      </div>
    );
  }

  return line.notes ? (
    <button
      type="button"
      className="order-line-notes-display"
      data-testid={`line-notes-${line.id}`}
      onClick={startEditing}
    >
      {t('order.notesLabel')}: {line.notes}
    </button>
  ) : (
    <button type="button" className="order-line-notes-add" data-testid={`add-note-${line.id}`} onClick={startEditing}>
      + {t('order.addNote')}
    </button>
  );
}

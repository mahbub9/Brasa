import { formatMoney } from '@brasa/ui/lib/money';
import { useState } from 'react';
import { useTranslation } from 'react-i18next';
import { Badge } from '@brasa/ui/components/Badge';
import { Button } from '@brasa/ui/components/Button';
import { Icon } from '@brasa/ui/components/Icon';
import { TextField } from '@brasa/ui/components/TextField';
import type { MoneyDto, OrderDto, OrderLineDto } from '../api/types';
import { formatTableLabel } from '../lib/tableLabel';

interface OrderSummaryProps {
  order: OrderDto;
  splitParts: number;
  onSplitPartsChange: (parts: number) => void;
  splitAmounts: MoneyDto[] | null;
  onPreviewSplit: () => void;
  onSetLineNotes: (lineId: string, notes: string | null) => void;
  onSetLineQuantity: (lineId: string, quantity: number) => void;
  onFireLines: (course: string | null) => void;
  onPreBill: () => void;
  onRequestBill: () => void;
  onTransferTable: () => void;
  onClose: () => void;
  onBack: () => void;
  busy: boolean;
}

export function OrderSummary({
  order,
  splitParts,
  onSplitPartsChange,
  splitAmounts,
  onPreviewSplit,
  onSetLineNotes,
  onSetLineQuantity,
  onFireLines,
  onPreBill,
  onRequestBill,
  onTransferTable,
  onClose,
  onBack,
  busy,
}: OrderSummaryProps) {
  const { t } = useTranslation();

  const unfiredCourses = [
    ...new Set(
      order.lines
        .filter((line) => !line.isFired && line.course)
        .map((line) => line.course as string),
    ),
  ];
  const hasUnfiredLines = order.lines.some((line) => !line.isFired);

  return (
    <aside className="order-summary">
      <div className="order-summary-heading">
        <Button
          variant="ghost"
          className="order-summary-back"
          data-testid="back-to-floor-button"
          onClick={onBack}
          disabled={busy}
        >
          <Icon name="back" /> {t('floor.backToFloor')}
        </Button>
        <h2>{formatTableLabel(order.tableLabel, t)}</h2>
        <Button variant="secondary" data-testid="transfer-table-button" onClick={onTransferTable} disabled={busy}>
          {t('order.transferTable')}
        </Button>
      </div>
      {!order.isTakeaway && <p className="covers">{t('order.covers', { count: order.coverCount })}</p>}

      {hasUnfiredLines && (
        <div className="fire-controls" data-testid="fire-controls">
          {unfiredCourses.map((course) => (
            <Button
              key={course}
              variant="secondary"
              data-testid={`fire-course-${course}`}
              disabled={busy}
              onClick={() => onFireLines(course)}
            >
              {t('order.fireCourse', { course: t(`order.course.${course}`) })}
            </Button>
          ))}
          <Button data-testid="fire-all-button" disabled={busy} onClick={() => onFireLines(null)}>
            {t('order.fireAll')}
          </Button>
        </div>
      )}

      {order.lines.length === 0 ? (
        <p className="empty-state">{t('order.empty')}</p>
      ) : (
        <ul className="order-lines">
          {order.lines.map((line) => (
            <li key={line.id}>
              <div className="order-line-row">
                <span className="order-line-qty-stepper">
                  <button
                    type="button"
                    aria-label={t('order.quantityDecrease')}
                    data-testid={`line-qty-decrease-${line.id}`}
                    disabled={busy || line.quantity <= 1}
                    onClick={() => onSetLineQuantity(line.id, line.quantity - 1)}
                  >
                    −
                  </button>
                  <span className="order-line-qty" data-testid={`line-qty-${line.id}`}>
                    {line.quantity}×
                  </span>
                  <button
                    type="button"
                    aria-label={t('order.quantityIncrease')}
                    data-testid={`line-qty-increase-${line.id}`}
                    disabled={busy}
                    onClick={() => onSetLineQuantity(line.id, line.quantity + 1)}
                  >
                    +
                  </button>
                </span>
                <span className="order-line-name">
                  {line.itemName}
                  {line.isFired && (
                    <Badge tone="brand" data-testid={`line-fired-${line.id}`}>
                      {t('order.fired')}
                    </Badge>
                  )}
                </span>
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
          <TextField
            type="number"
            min={1}
            data-testid="split-parts-input"
            value={splitParts}
            onChange={(e) => onSplitPartsChange(Math.max(1, Number(e.target.value)))}
          />
          {t('order.splitWays')}
        </label>
        <Button
          variant="secondary"
          data-testid="preview-split-button"
          onClick={onPreviewSplit}
          disabled={busy || order.lines.length === 0}
        >
          {t('order.previewSplit')}
        </Button>
        {splitAmounts && (
          <ul className="split-amounts" data-testid="split-amounts">
            {splitAmounts.map((amount, index) => (
              <li key={index}>{formatMoney(amount)}</li>
            ))}
          </ul>
        )}
      </div>

      <Button variant="secondary" data-testid="pre-bill-button" onClick={onPreBill} disabled={busy || order.lines.length === 0}>
        {t('order.preBill')}
      </Button>

      {!order.isTakeaway && (
        <Button variant="secondary" data-testid="request-bill-button" onClick={onRequestBill} disabled={busy}>
          {t('order.requestBill')}
        </Button>
      )}

      <Button data-testid="close-order-button" onClick={onClose} disabled={busy || order.lines.length === 0}>
        {busy ? t('order.closing') : t('order.close')}
      </Button>
    </aside>
  );
}

interface OrderLineNotesProps {
  line: OrderLineDto;
  busy: boolean;
  onSave: (lineId: string, notes: string | null) => void;
}

/** Free-text kitchen note per line (ORD-06), added after the line is already rung up. */
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
        <TextField
          type="text"
          value={draft}
          maxLength={300}
          placeholder={t('order.notesPlaceholder')}
          data-testid={`line-notes-input-${line.id}`}
          onChange={(e) => setDraft(e.target.value)}
        />
        <Button variant="secondary" data-testid={`line-notes-save-${line.id}`} disabled={busy} onClick={save}>
          {t('order.notesSave')}
        </Button>
        <Button variant="ghost" disabled={busy} onClick={() => setEditing(false)}>
          {t('order.notesCancel')}
        </Button>
      </div>
    );
  }

  return line.notes ? (
    <Button variant="ghost" className="order-line-notes-display" data-testid={`line-notes-${line.id}`} onClick={startEditing}>
      {t('order.notesLabel')}: {line.notes}
    </Button>
  ) : (
    <Button variant="ghost" className="order-line-notes-add" data-testid={`add-note-${line.id}`} onClick={startEditing}>
      <Icon name="add" /> {t('order.addNote')}
    </Button>
  );
}

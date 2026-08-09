import { useTranslation } from 'react-i18next';
import type { MoneyDto, OrderDto } from '../api/types';
import { formatMoney } from '../lib/money';

interface OrderSummaryProps {
  order: OrderDto;
  splitParts: number;
  onSplitPartsChange: (parts: number) => void;
  splitAmounts: MoneyDto[] | null;
  onPreviewSplit: () => void;
  onClose: () => void;
  busy: boolean;
}

export function OrderSummary({
  order,
  splitParts,
  onSplitPartsChange,
  splitAmounts,
  onPreviewSplit,
  onClose,
  busy,
}: OrderSummaryProps) {
  const { t } = useTranslation();

  return (
    <aside className="order-summary">
      <h2>{order.tableLabel}</h2>
      <p className="covers">{t('order.covers', { count: order.coverCount })}</p>

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

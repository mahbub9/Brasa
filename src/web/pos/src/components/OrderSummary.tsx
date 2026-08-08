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
  return (
    <aside className="order-summary">
      <h2>{order.tableLabel}</h2>
      <p className="covers">{order.coverCount} covers</p>

      {order.lines.length === 0 ? (
        <p className="empty-state">No items yet — tap the menu to add some.</p>
      ) : (
        <ul className="order-lines">
          {order.lines.map((line) => (
            <li key={line.id}>
              <span className="order-line-qty">{line.quantity}×</span>
              <span className="order-line-name">{line.itemName}</span>
              <span className="order-line-total">{formatMoney(line.lineTotal)}</span>
            </li>
          ))}
        </ul>
      )}

      <div className="order-total">
        <span>Total</span>
        <strong data-testid="order-total">{formatMoney(order.total)}</strong>
      </div>

      <div className="split-preview">
        <label>
          Split
          <input
            type="number"
            min={1}
            data-testid="split-parts-input"
            value={splitParts}
            onChange={(e) => onSplitPartsChange(Math.max(1, Number(e.target.value)))}
          />
          ways
        </label>
        <button
          type="button"
          data-testid="preview-split-button"
          onClick={onPreviewSplit}
          disabled={busy || order.lines.length === 0}
        >
          Preview split
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
        {busy ? 'Closing…' : 'Close & issue receipt'}
      </button>
    </aside>
  );
}

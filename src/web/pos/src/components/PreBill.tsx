import { useTranslation } from 'react-i18next';
import type { PreBillDto } from '../api/types';
import { formatMoney } from '../lib/money';

interface PreBillProps {
  preBill: PreBillDto;
  onClose: () => void;
}

/**
 * The pre-bill a table sees before paying — a *documento não fiscal*, not an
 * invoice (ORD-18/19). Deliberately has no document number, ATCUD or QR
 * anywhere in its markup: this is a preview computed from the order's
 * current lines, not a fiscal artefact. See docs/fiscal/README.md and
 * OrderDtos.cs's PreBillDto.
 */
export function PreBill({ preBill, onClose }: PreBillProps) {
  const { t } = useTranslation();

  return (
    <div className="pre-bill-backdrop" role="dialog" aria-modal="true" aria-label={t('preBill.title')}>
      <div className="pre-bill" data-testid="pre-bill">
        <h2>{t('preBill.title')}</h2>
        <p className="pre-bill-table">{preBill.tableLabel}</p>
        <p className="pre-bill-notice" data-testid="pre-bill-notice">
          {t('preBill.notice')}
        </p>

        <ul className="order-lines">
          {preBill.lines.map((line) => (
            <li key={line.id}>
              <div className="order-line-row">
                <span className="order-line-qty">{line.quantity}×</span>
                <span className="order-line-name">{line.itemName}</span>
                <span className="order-line-total">{formatMoney(line.lineTotal)}</span>
              </div>
            </li>
          ))}
        </ul>

        <dl className="pre-bill-vat-breakdown">
          {preBill.vatBreakdown.map((band) => (
            <div className="pre-bill-vat-row" key={band.vatRateFraction}>
              <dt>
                {t('preBill.vat')} {Math.round(band.vatRateFraction * 100)}%
              </dt>
              <dd>{formatMoney(band.vatAmount)}</dd>
            </div>
          ))}
        </dl>

        <div className="order-total">
          <span>{t('order.total')}</span>
          <strong data-testid="pre-bill-total">{formatMoney(preBill.total)}</strong>
        </div>

        {/* Always pt-PT — a timestamp on the printed slip, not UI chrome. Same
            reasoning as Receipt.tsx's issued date. */}
        <p className="pre-bill-generated">
          {t('preBill.generated')} {new Date(preBill.generatedAtUtc).toLocaleTimeString('pt-PT')}
        </p>

        <button type="button" data-testid="close-pre-bill" onClick={onClose}>
          {t('preBill.close')}
        </button>
      </div>
    </div>
  );
}

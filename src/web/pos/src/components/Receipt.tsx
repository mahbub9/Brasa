import { formatMoney } from '@brasa/ui/lib/money';
import { useTranslation } from 'react-i18next';
import { Button } from '@brasa/ui/components/Button';
import { CashPayment } from './CashPayment';
import type { CloseOrderResponse, StaffDto } from '../api/types';
import { formatTableLabel } from '../lib/tableLabel';

interface ReceiptProps {
  result: CloseOrderResponse;
  onNewTable: () => void;
  /** The signed-in staff member (WEB-07), if any — threaded down to CashPayment for tip attribution (PAY-06). */
  currentStaff: StaffDto | null;
}

export function Receipt({ result, onNewTable, currentStaff }: ReceiptProps) {
  const { t } = useTranslation();
  const { order, document } = result;

  return (
    <div className="receipt">
      <h1>{t('receipt.title')}</h1>
      <p className="receipt-table">{formatTableLabel(order.tableLabel, t)}</p>

      <dl className="receipt-fields">
        <dt>{t('receipt.document')}</dt>
        <dd data-testid="receipt-document-number">{document.documentNumber}</dd>
        <dt>{t('receipt.atcud')}</dt>
        <dd data-testid="receipt-atcud">{document.atcud}</dd>
        <dt>{t('receipt.net')}</dt>
        <dd data-testid="receipt-net">{formatMoney(document.netTotal)}</dd>
        <dt>{t('receipt.vat')}</dt>
        <dd data-testid="receipt-vat">{formatMoney(document.vatTotal)}</dd>
        <dt>{t('receipt.gross')}</dt>
        <dd className="receipt-gross" data-testid="receipt-gross">
          {formatMoney(document.grossTotal)}
        </dd>
        <dt>{t('receipt.issued')}</dt>
        {/* Always pt-PT, deliberately not tied to the UI language toggle —
            this is a fiscal document date, not UI chrome. Same reasoning as
            formatMoney in lib/money.ts. */}
        <dd>{new Date(document.issuedAtUtc).toLocaleString('pt-PT')}</dd>
      </dl>

      <p className="receipt-qr-payload" title={document.qrPayload}>
        {document.qrPayload}
      </p>
      <p className="receipt-note">
        {t('receipt.mockNotice')}{' '}
        <code>docs/architecture/decisions/0002-own-fiscal-engine.md</code>.
      </p>

      <CashPayment orderId={order.id} amountDue={order.total} currentStaff={currentStaff} />

      <Button onClick={onNewTable}>{t('receipt.newTable')}</Button>
    </div>
  );
}

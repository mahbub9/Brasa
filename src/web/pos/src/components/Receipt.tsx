import type { CloseOrderResponse } from '../api/types';
import { formatMoney } from '../lib/money';

interface ReceiptProps {
  result: CloseOrderResponse;
  onNewTable: () => void;
}

export function Receipt({ result, onNewTable }: ReceiptProps) {
  const { order, document } = result;

  return (
    <div className="receipt">
      <h1>Receipt issued</h1>
      <p className="receipt-table">{order.tableLabel}</p>

      <dl className="receipt-fields">
        <dt>Document</dt>
        <dd>{document.documentNumber}</dd>
        <dt>ATCUD</dt>
        <dd>{document.atcud}</dd>
        <dt>Net</dt>
        <dd>{formatMoney(document.netTotal)}</dd>
        <dt>VAT</dt>
        <dd>{formatMoney(document.vatTotal)}</dd>
        <dt>Gross</dt>
        <dd className="receipt-gross">{formatMoney(document.grossTotal)}</dd>
        <dt>Issued</dt>
        <dd>{new Date(document.issuedAtUtc).toLocaleString('pt-PT')}</dd>
      </dl>

      <p className="receipt-qr-payload" title={document.qrPayload}>
        {document.qrPayload}
      </p>
      <p className="receipt-note">
        Mock fiscal provider — this document has no legal value. See{' '}
        <code>docs/architecture/decisions/0002-own-fiscal-engine.md</code>.
      </p>

      <button type="button" onClick={onNewTable}>
        Open another table
      </button>
    </div>
  );
}

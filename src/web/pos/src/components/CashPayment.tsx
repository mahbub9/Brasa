import { useState } from 'react';
import { useTranslation } from 'react-i18next';
import { formatMoney } from '@brasa/ui/lib/money';
import { Button } from '@brasa/ui/components/Button';
import { TextField } from '@brasa/ui/components/TextField';
import { SelectField } from '@brasa/ui/components/SelectField';
import { describeError } from '../lib/describeError';
import { api } from '../api/client';
import type { MoneyDto, PaymentDto, PaymentMethod, StaffDto } from '../api/types';

interface CashPaymentProps {
  orderId: string;
  amountDue: MoneyDto;
  /** The signed-in staff member (WEB-07), if any — a tip is auto-credited to them (PAY-06). Not a picker: there is no UI to attribute a tip to anyone else. */
  currentStaff: StaffDto | null;
}

/**
 * PAY-01/02/03/05/06's own UI — records one or more tenders (cash or card)
 * against an already-closed order and shows the change due once fully
 * settled. Self-contained, the same "own local busy/error state, not
 * App.tsx's global one" shape <c>StaffLogin</c> already uses: by the time
 * <c>Receipt</c> renders, the order-mutation flow that owns App.tsx's
 * shared <c>busy</c> flag is already over, and a payment failure here
 * shouldn't disable buttons elsewhere on the screen.
 *
 * A tender smaller than what's owed is a valid partial payment (PAY-05):
 * the form stays open, showing the running remaining balance and the
 * tenders recorded so far, until a payment brings the balance to zero —
 * only then does this replace itself with the confirmed change (from
 * whichever tender actually settled it). A card tender (PAY-03) can never
 * overpay — the server rejects it, since a standalone TPA has no change to
 * give back — so nothing client-side needs to special-case that; the same
 * error banner used for every other rejection handles it.
 *
 * A tip is optional on every tender, not just the settling one, and is
 * separate from the balance entirely (PAY-06). Attribution is automatic —
 * whoever is signed in when the tip is recorded — since there is no
 * staff-picker here; an unattributed tip (nobody signed in) still records,
 * just with no `staffId`.
 */
export function CashPayment({ orderId, amountDue, currentStaff }: CashPaymentProps) {
  const { t } = useTranslation();
  const [method, setMethod] = useState<PaymentMethod>('Cash');
  const [amountTendered, setAmountTendered] = useState('');
  const [tipAmount, setTipAmount] = useState('');
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [payments, setPayments] = useState<PaymentDto[]>([]);

  const lastPayment = payments.length > 0 ? payments[payments.length - 1] : null;
  const remaining = lastPayment ? lastPayment.remainingBalance : amountDue;
  const isSettled = lastPayment !== null && remaining.amount <= 0;

  async function submit() {
    const parsedTendered = Number(amountTendered);
    if (Number.isNaN(parsedTendered) || parsedTendered <= 0) {
      return;
    }

    const parsedTip = tipAmount === '' ? 0 : Number(tipAmount);
    if (Number.isNaN(parsedTip) || parsedTip < 0) {
      return;
    }

    setBusy(true);
    setError(null);
    try {
      const recorded = await api.recordPayment(orderId, {
        method,
        amountTendered: parsedTendered,
        tipAmount: parsedTip,
        staffId: parsedTip > 0 ? (currentStaff?.id ?? null) : null,
      });
      setPayments((prev) => [...prev, recorded]);
      setAmountTendered('');
      setTipAmount('');
    } catch (err) {
      setError(describeError(err));
    } finally {
      setBusy(false);
    }
  }

  if (isSettled) {
    return (
      <div className="cash-payment cash-payment-done" data-testid="cash-payment-done">
        <p>{t('payment.recorded')}</p>
        <p className="cash-payment-change" data-testid="cash-payment-change">
          {t('payment.changeDue', { amount: formatMoney(lastPayment.change) })}
        </p>
        {lastPayment.tipAmount.amount > 0 && (
          <p className="cash-payment-tip" data-testid="cash-payment-tip-recorded">
            {t('payment.tipRecorded', { amount: formatMoney(lastPayment.tipAmount) })}
          </p>
        )}
      </div>
    );
  }

  return (
    <div className="cash-payment">
      <h2>{t('payment.title')}</h2>
      <p className="cash-payment-due" data-testid="cash-payment-remaining">
        {t('payment.amountDue', { amount: formatMoney(remaining) })}
      </p>
      {payments.length > 0 && (
        <div className="cash-payment-history" data-testid="cash-payment-history">
          <p className="cash-payment-history-title">{t('payment.historyTitle')}</p>
          <ul>
            {payments.map((recorded) => (
              <li key={recorded.id}>
                {t('payment.historyEntry', {
                  amount: formatMoney(recorded.amountTendered),
                  method: t(`payment.method.${recorded.method.toLowerCase()}`),
                })}
                {recorded.tipAmount.amount > 0 &&
                  ` — ${t('payment.tipHistoryEntry', { amount: formatMoney(recorded.tipAmount) })}`}
              </li>
            ))}
          </ul>
        </div>
      )}
      <div className="cash-payment-form">
        <SelectField
          aria-label={t('payment.methodLabel')}
          className="cash-payment-method"
          value={method}
          data-testid="cash-payment-method"
          disabled={busy}
          onChange={(e) => setMethod(e.target.value as PaymentMethod)}
        >
          <option value="Cash">{t('payment.method.cash')}</option>
          <option value="Card">{t('payment.method.card')}</option>
        </SelectField>
        <TextField
          type="number"
          step="0.01"
          min="0"
          inputMode="decimal"
          placeholder={t('payment.tenderedPlaceholder')}
          value={amountTendered}
          data-testid="cash-payment-tendered"
          disabled={busy}
          onChange={(e) => setAmountTendered(e.target.value)}
          onKeyDown={(e) => {
            if (e.key === 'Enter' && amountTendered !== '') void submit();
          }}
        />
        <TextField
          type="number"
          step="0.01"
          min="0"
          inputMode="decimal"
          placeholder={t('payment.tipPlaceholder')}
          value={tipAmount}
          data-testid="cash-payment-tip"
          disabled={busy}
          onChange={(e) => setTipAmount(e.target.value)}
          onKeyDown={(e) => {
            if (e.key === 'Enter' && amountTendered !== '') void submit();
          }}
        />
        <Button data-testid="cash-payment-submit" disabled={busy || amountTendered === ''} onClick={() => void submit()}>
          {t('payment.record')}
        </Button>
      </div>
      {currentStaff && (
        <p className="cash-payment-tip-attribution">{t('payment.tipAttributedTo', { name: currentStaff.name })}</p>
      )}
      {error && (
        <p className="cash-payment-error" data-testid="cash-payment-error">
          {error}
        </p>
      )}
    </div>
  );
}

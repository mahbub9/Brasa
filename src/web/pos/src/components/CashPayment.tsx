import { useState } from 'react';
import { useTranslation } from 'react-i18next';
import { formatMoney } from '@brasa/ui/lib/money';
import { Button } from '@brasa/ui/components/Button';
import { TextField } from '@brasa/ui/components/TextField';
import { describeError } from '../lib/describeError';
import { api } from '../api/client';
import type { MoneyDto, PaymentDto } from '../api/types';

interface CashPaymentProps {
  orderId: string;
  amountDue: MoneyDto;
}

/**
 * PAY-01/02's own UI — records a cash tender against an already-closed
 * order and shows the change due. Self-contained, the same "own local
 * busy/error state, not App.tsx's global one" shape <c>StaffLogin</c>
 * already uses: by the time <c>Receipt</c> renders, the order-mutation flow
 * that owns App.tsx's shared <c>busy</c> flag is already over, and a
 * payment failure here shouldn't disable buttons elsewhere on the screen.
 * Full payment only — no partial tender (PAY-05) — so once one succeeds,
 * this replaces its own form with the confirmed change rather than
 * offering to record a second one.
 */
export function CashPayment({ orderId, amountDue }: CashPaymentProps) {
  const { t } = useTranslation();
  const [amountTendered, setAmountTendered] = useState('');
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [payment, setPayment] = useState<PaymentDto | null>(null);

  async function submit() {
    const parsed = Number(amountTendered);
    if (Number.isNaN(parsed) || parsed <= 0) {
      return;
    }

    setBusy(true);
    setError(null);
    try {
      setPayment(await api.recordPayment(orderId, { method: 'Cash', amountTendered: parsed }));
    } catch (err) {
      setError(describeError(err));
    } finally {
      setBusy(false);
    }
  }

  if (payment) {
    return (
      <div className="cash-payment cash-payment-done" data-testid="cash-payment-done">
        <p>{t('payment.recorded')}</p>
        <p className="cash-payment-change" data-testid="cash-payment-change">
          {t('payment.changeDue', { amount: formatMoney(payment.change) })}
        </p>
      </div>
    );
  }

  return (
    <div className="cash-payment">
      <h2>{t('payment.title')}</h2>
      <p className="cash-payment-due">{t('payment.amountDue', { amount: formatMoney(amountDue) })}</p>
      <div className="cash-payment-form">
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
        <Button data-testid="cash-payment-submit" disabled={busy || amountTendered === ''} onClick={() => void submit()}>
          {t('payment.record')}
        </Button>
      </div>
      {error && (
        <p className="cash-payment-error" data-testid="cash-payment-error">
          {error}
        </p>
      )}
    </div>
  );
}

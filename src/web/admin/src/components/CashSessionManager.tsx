import { useCallback, useEffect, useState } from 'react';
import { useTranslation } from 'react-i18next';
import { formatMoney } from '@brasa/ui/lib/money';
import { Badge } from '@brasa/ui/components/Badge';
import { Button } from '@brasa/ui/components/Button';
import { SelectField } from '@brasa/ui/components/SelectField';
import { TextField } from '@brasa/ui/components/TextField';
import { api, ApiError } from '../api/client';
import type {
  CashMovementDirection,
  CashMovementDto,
  CashSessionDto,
  CashSessionVarianceDto,
  StaffDto,
  TerminalDto,
} from '../api/types';

interface CashSessionManagerProps {
  /** The site to manage cash sessions at. `null` until App.tsx resolves one — see its own remarks on why there's no site-selector yet. */
  siteId: string | null;
  onErrorChange: (message: string | null) => void;
}

/**
 * PAY-08's own admin surface — *abertura de caixa*, one terminal at a time.
 * Every terminal registered at the site (IDN-01) gets its own row showing
 * whether it currently has an open session; opening one needs a staff
 * member (any role — no manager gate, the same "confirm before
 * constructing" shape FLR-06's section assignment already uses) and a
 * starting float. Closing is a bare status flip today — a blind count and
 * variance report are PAY-10/11's own later task, not this.
 */
export function CashSessionManager({ siteId, onErrorChange }: CashSessionManagerProps) {
  const { t } = useTranslation();
  const [terminals, setTerminals] = useState<TerminalDto[] | null>(null);
  const [staff, setStaff] = useState<StaffDto[] | null>(null);
  const [sessions, setSessions] = useState<Record<string, CashSessionDto | null>>({});

  const load = useCallback(() => {
    if (!siteId) {
      return;
    }

    Promise.all([api.getTerminals(siteId), api.getStaff(siteId)])
      .then(async ([terminalList, staffList]) => {
        setTerminals(terminalList);
        setStaff(staffList);

        const entries = await Promise.all(
          terminalList.map(
            async (terminal) => [terminal.id, await api.getCurrentCashSession(terminal.id)] as const,
          ),
        );
        setSessions(Object.fromEntries(entries));
      })
      .catch((err: unknown) => onErrorChange(err instanceof ApiError ? err.message : t('error.generic')));
  }, [siteId, onErrorChange, t]);

  useEffect(() => {
    load();
  }, [load]);

  function reloadOne(terminalId: string) {
    api
      .getCurrentCashSession(terminalId)
      .then((session) => setSessions((prev) => ({ ...prev, [terminalId]: session })))
      .catch((err: unknown) => onErrorChange(err instanceof ApiError ? err.message : t('error.generic')));
  }

  if (!siteId || !terminals || !staff) {
    return <p className="admin-loading">{t('app.loading')}</p>;
  }

  return (
    <div className="cash-session-manager">
      {terminals.length === 0 && <p className="empty-state">{t('cashSession.noTerminals')}</p>}

      {terminals.length > 0 && (
        <ul className="cash-session-list">
          {terminals.map((terminal) => (
            <TerminalRow
              key={terminal.id}
              terminal={terminal}
              session={sessions[terminal.id] ?? null}
              staff={staff}
              onReload={() => reloadOne(terminal.id)}
              onErrorChange={onErrorChange}
            />
          ))}
        </ul>
      )}
    </div>
  );
}

interface TerminalRowProps {
  terminal: TerminalDto;
  session: CashSessionDto | null;
  staff: StaffDto[];
  onReload: () => void;
  onErrorChange: (message: string | null) => void;
}

function TerminalRow({ terminal, session, staff, onReload, onErrorChange }: TerminalRowProps) {
  const { t } = useTranslation();
  const [busy, setBusy] = useState(false);
  const [opening, setOpening] = useState(false);
  const [staffId, setStaffId] = useState(staff[0]?.id ?? '');
  const [openingFloat, setOpeningFloat] = useState('');

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

  function openSession() {
    void run(async () => {
      await api.openCashSession({ terminalId: terminal.id, staffId, openingFloat: Number(openingFloat) });
      setOpening(false);
      setOpeningFloat('');
      onReload();
    });
  }

  function closeSession() {
    if (!session) {
      return;
    }

    void run(async () => {
      await api.closeCashSession(session.id);
      onReload();
    });
  }

  return (
    <li className="cash-session-row" data-testid={`cash-session-terminal-${terminal.label}`}>
      <div className="cash-session-row-summary">
        <span className="cash-session-terminal-label">{terminal.label}</span>
        {session?.isOpen ? (
          <>
            <Badge tone="brand" data-testid={`cash-session-open-${terminal.label}`}>
              {t('cashSession.open')}
            </Badge>
            <span className="cash-session-details">
              {t('cashSession.openedBy', {
                name: session.openedByStaffName,
                float: formatMoney(session.openingFloat),
              })}
            </span>
          </>
        ) : (
          <Badge tone="neutral">{t('cashSession.closed')}</Badge>
        )}
      </div>

      {session?.isOpen ? (
        <>
          <Button
            variant="secondary"
            data-testid={`cash-session-close-${terminal.label}`}
            disabled={busy}
            onClick={closeSession}
          >
            {t('cashSession.closeSession')}
          </Button>
          <CashCountPanel session={session} staff={staff} onReload={onReload} onErrorChange={onErrorChange} />
          <CashMovementsPanel session={session} staff={staff} onErrorChange={onErrorChange} />
        </>
      ) : opening ? (
        <div className="cash-session-open-form">
          <SelectField
            aria-label={t('cashSession.staff')}
            value={staffId}
            data-testid={`cash-session-staff-${terminal.label}`}
            disabled={busy}
            onChange={(e) => setStaffId(e.target.value)}
          >
            {staff.map((member) => (
              <option key={member.id} value={member.id}>
                {member.name}
              </option>
            ))}
          </SelectField>
          <TextField
            type="number"
            step="0.01"
            min="0"
            inputMode="decimal"
            placeholder={t('cashSession.openingFloatPlaceholder')}
            value={openingFloat}
            data-testid={`cash-session-float-${terminal.label}`}
            disabled={busy}
            onChange={(e) => setOpeningFloat(e.target.value)}
          />
          <Button
            data-testid={`cash-session-open-save-${terminal.label}`}
            disabled={busy || staffId === '' || openingFloat === ''}
            onClick={openSession}
          >
            {t('common.save')}
          </Button>
          <Button variant="ghost" disabled={busy} onClick={() => setOpening(false)}>
            {t('common.cancel')}
          </Button>
        </div>
      ) : (
        <Button
          variant="secondary"
          data-testid={`cash-session-open-${terminal.label}-start`}
          disabled={busy || staff.length === 0}
          onClick={() => setOpening(true)}
        >
          {t('cashSession.openSession')}
        </Button>
      )}
      {/* Shown regardless of open/closed -- variance is meaningful the moment a count exists (PAY-10), whether or not the session has been closed since. */}
      {session?.countedAmount && <CashVarianceReport session={session} onErrorChange={onErrorChange} />}
    </li>
  );
}

interface CashCountPanelProps {
  session: CashSessionDto;
  staff: StaffDto[];
  onReload: () => void;
  onErrorChange: (message: string | null) => void;
}

/**
 * PAY-10's own admin surface — a blind cash count against an open session,
 * at most once. "Blind" because nothing here shows an expected total to
 * compare against (no variance calculation exists yet — PAY-11). Once
 * recorded, this collapses to a read-only line; there's no edit path,
 * matching `CashSession.RecordCount`'s own "at most once" domain rule.
 */
function CashCountPanel({ session, staff, onReload, onErrorChange }: CashCountPanelProps) {
  const { t } = useTranslation();
  const [counting, setCounting] = useState(false);
  const [busy, setBusy] = useState(false);
  const [staffId, setStaffId] = useState(staff[0]?.id ?? '');
  const [countedAmount, setCountedAmount] = useState('');

  function submit() {
    setBusy(true);
    onErrorChange(null);
    api
      .recordCashCount(session.id, { staffId, countedAmount: Number(countedAmount) })
      .then(() => {
        setCounting(false);
        setCountedAmount('');
        onReload();
      })
      .catch((err: unknown) => onErrorChange(err instanceof ApiError ? err.message : t('error.generic')))
      .finally(() => setBusy(false));
  }

  if (session.countedAmount) {
    return (
      <p className="cash-count-recorded" data-testid={`cash-count-recorded-${session.terminalLabel}`}>
        {t('cashSession.counted', {
          amount: formatMoney(session.countedAmount),
          name: session.countedByStaffName,
        })}
      </p>
    );
  }

  if (!counting) {
    return (
      <Button
        variant="ghost"
        data-testid={`cash-count-start-${session.terminalLabel}`}
        disabled={staff.length === 0}
        onClick={() => setCounting(true)}
      >
        {t('cashSession.recordCount')}
      </Button>
    );
  }

  return (
    <div className="cash-count-form">
      <SelectField
        aria-label={t('cashSession.staff')}
        value={staffId}
        data-testid={`cash-count-staff-${session.terminalLabel}`}
        disabled={busy}
        onChange={(e) => setStaffId(e.target.value)}
      >
        {staff.map((member) => (
          <option key={member.id} value={member.id}>
            {member.name}
          </option>
        ))}
      </SelectField>
      <TextField
        type="number"
        step="0.01"
        min="0"
        inputMode="decimal"
        placeholder={t('cashSession.countedAmountPlaceholder')}
        value={countedAmount}
        data-testid={`cash-count-amount-${session.terminalLabel}`}
        disabled={busy}
        onChange={(e) => setCountedAmount(e.target.value)}
      />
      <Button
        data-testid={`cash-count-save-${session.terminalLabel}`}
        disabled={busy || countedAmount === ''}
        onClick={submit}
      >
        {t('common.save')}
      </Button>
      <Button variant="ghost" disabled={busy} onClick={() => setCounting(false)}>
        {t('common.cancel')}
      </Button>
    </div>
  );
}

interface CashMovementsPanelProps {
  session: CashSessionDto;
  staff: StaffDto[];
  onErrorChange: (message: string | null) => void;
}

/**
 * PAY-09's own admin surface — pay-ins/pay-outs against an open session,
 * always with a reason. Nested inside `TerminalRow`, visible only while
 * that terminal's session is open — a movement makes no sense against a
 * closed one, and the API rejects it (`cash_movement.session_closed`).
 */
function CashMovementsPanel({ session, staff, onErrorChange }: CashMovementsPanelProps) {
  const { t } = useTranslation();
  const [movements, setMovements] = useState<CashMovementDto[] | null>(null);
  const [adding, setAdding] = useState(false);
  const [busy, setBusy] = useState(false);
  const [direction, setDirection] = useState<CashMovementDirection>('PayOut');
  const [amount, setAmount] = useState('');
  const [reason, setReason] = useState('');
  const [staffId, setStaffId] = useState(staff[0]?.id ?? '');

  const load = useCallback(() => {
    api
      .getCashMovements(session.id)
      .then(setMovements)
      .catch((err: unknown) => onErrorChange(err instanceof ApiError ? err.message : t('error.generic')));
  }, [session.id, onErrorChange, t]);

  useEffect(() => {
    load();
  }, [load]);

  function submit() {
    setBusy(true);
    onErrorChange(null);
    api
      .recordCashMovement(session.id, { direction, amount: Number(amount), reason, staffId })
      .then(() => {
        setAmount('');
        setReason('');
        setAdding(false);
        load();
      })
      .catch((err: unknown) => onErrorChange(err instanceof ApiError ? err.message : t('error.generic')))
      .finally(() => setBusy(false));
  }

  return (
    <div className="cash-movements-panel" data-testid={`cash-movements-${session.terminalLabel}`}>
      {movements && movements.length > 0 && (
        <ul className="cash-movements-list">
          {movements.map((movement) => (
            <li key={movement.id}>
              {t(`cashSession.direction.${movement.direction}`)}: {formatMoney(movement.amount)} —{' '}
              {movement.reason} ({movement.recordedByStaffName})
            </li>
          ))}
        </ul>
      )}

      {!adding ? (
        <Button
          variant="ghost"
          data-testid={`cash-movement-add-${session.terminalLabel}`}
          disabled={staff.length === 0}
          onClick={() => setAdding(true)}
        >
          {t('cashSession.addMovement')}
        </Button>
      ) : (
        <div className="cash-movement-form">
          <SelectField
            aria-label={t('cashSession.direction.label')}
            value={direction}
            data-testid={`cash-movement-direction-${session.terminalLabel}`}
            disabled={busy}
            onChange={(e) => setDirection(e.target.value as CashMovementDirection)}
          >
            <option value="PayOut">{t('cashSession.direction.PayOut')}</option>
            <option value="PayIn">{t('cashSession.direction.PayIn')}</option>
          </SelectField>
          <TextField
            type="number"
            step="0.01"
            min="0"
            inputMode="decimal"
            placeholder={t('cashSession.amountPlaceholder')}
            value={amount}
            data-testid={`cash-movement-amount-${session.terminalLabel}`}
            disabled={busy}
            onChange={(e) => setAmount(e.target.value)}
          />
          <TextField
            type="text"
            placeholder={t('cashSession.reasonPlaceholder')}
            value={reason}
            data-testid={`cash-movement-reason-${session.terminalLabel}`}
            disabled={busy}
            onChange={(e) => setReason(e.target.value)}
          />
          <SelectField
            aria-label={t('cashSession.staff')}
            value={staffId}
            data-testid={`cash-movement-staff-${session.terminalLabel}`}
            disabled={busy}
            onChange={(e) => setStaffId(e.target.value)}
          >
            {staff.map((member) => (
              <option key={member.id} value={member.id}>
                {member.name}
              </option>
            ))}
          </SelectField>
          <Button
            data-testid={`cash-movement-save-${session.terminalLabel}`}
            disabled={busy || amount === '' || reason.trim() === ''}
            onClick={submit}
          >
            {t('common.save')}
          </Button>
          <Button variant="ghost" disabled={busy} onClick={() => setAdding(false)}>
            {t('common.cancel')}
          </Button>
        </div>
      )}
    </div>
  );
}

interface CashVarianceReportProps {
  session: CashSessionDto;
  onErrorChange: (message: string | null) => void;
}

/**
 * PAY-11's own admin surface — a computed *fecho de caixa* variance report,
 * fetched fresh on every render rather than derived client-side from the
 * session/movements already in memory here, since the expected amount also
 * depends on cash `Payment`s this screen never loads. Shown whenever a
 * blind count exists (PAY-10), regardless of whether the session has since
 * been closed — variance doesn't stop being meaningful just because the
 * till was closed out.
 */
function CashVarianceReport({ session, onErrorChange }: CashVarianceReportProps) {
  const { t } = useTranslation();
  const [variance, setVariance] = useState<CashSessionVarianceDto | null>(null);

  const load = useCallback(() => {
    api
      .getCashSessionVariance(session.id)
      .then(setVariance)
      .catch((err: unknown) => onErrorChange(err instanceof ApiError ? err.message : t('error.generic')));
  }, [session.id, onErrorChange, t]);

  useEffect(() => {
    load();
  }, [load]);

  if (!variance) {
    return null;
  }

  const varianceAmount = variance.variance;
  const isBalanced = varianceAmount !== null && varianceAmount.amount === 0;

  return (
    <div className="cash-variance-report" data-testid={`cash-variance-${session.terminalLabel}`}>
      <p className="cash-variance-line">
        {t('cashSession.expectedAmount', { amount: formatMoney(variance.expectedAmount) })}
      </p>
      {varianceAmount !== null && (
        <p className="cash-variance-line">
          <Badge tone={isBalanced ? 'neutral' : 'danger'} data-testid={`cash-variance-badge-${session.terminalLabel}`}>
            {t('cashSession.variance', { amount: formatMoney(varianceAmount) })}
          </Badge>
        </p>
      )}
    </div>
  );
}

import { useCallback, useEffect, useState } from 'react';
import { useTranslation } from 'react-i18next';
import { formatMoney } from '@brasa/ui/lib/money';
import { Badge } from '@brasa/ui/components/Badge';
import { Button } from '@brasa/ui/components/Button';
import { SelectField } from '@brasa/ui/components/SelectField';
import { TextField } from '@brasa/ui/components/TextField';
import { api, ApiError } from '../api/client';
import type { CashSessionDto, StaffDto, TerminalDto } from '../api/types';

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
        <Button
          variant="secondary"
          data-testid={`cash-session-close-${terminal.label}`}
          disabled={busy}
          onClick={closeSession}
        >
          {t('cashSession.closeSession')}
        </Button>
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
    </li>
  );
}

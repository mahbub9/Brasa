import { useState } from 'react';
import { useTranslation } from 'react-i18next';
import { api, ApiError } from '../api/client';
import type { StaffDto } from '../api/types';

interface StaffLoginProps {
  siteId: string | null;
  staff: StaffDto[] | null;
  currentStaff: StaffDto | null;
  onSignedIn: (staff: StaffDto) => void;
  onSignOut: () => void;
}

/**
 * WEB-07's own staff PIN sign-in, unblocked by IDN-08/09/11. Deliberately
 * non-blocking — signing in here doesn't gate anything the rest of the app
 * does, the same "mechanism before the trigger" shape IDN-08/09's own PIN
 * verification shipped in ahead of IDN-11 (its own eventual first real
 * consumer). Gating pos behind a login screen would touch every existing
 * E2E spec that drives it directly from a fresh `page.goto('/')` — a much
 * larger, separate change than "a real, working sign-in exists."
 */
export function StaffLogin({ siteId, staff, currentStaff, onSignedIn, onSignOut }: StaffLoginProps) {
  const { t } = useTranslation();
  const [open, setOpen] = useState(false);

  if (currentStaff) {
    return (
      <div className="staff-login-badge">
        <span data-testid="staff-signed-in">{t('staff.signedInAs', { name: currentStaff.name })}</span>
        <button type="button" data-testid="staff-sign-out" onClick={onSignOut}>
          {t('staff.signOut')}
        </button>
      </div>
    );
  }

  return (
    <>
      <button
        type="button"
        className="staff-login-open"
        data-testid="staff-sign-in-open"
        disabled={!siteId}
        onClick={() => setOpen(true)}
      >
        {t('staff.signIn')}
      </button>
      {open && (
        <StaffLoginModal
          staff={staff ?? []}
          onSignedIn={(signedIn) => {
            setOpen(false);
            onSignedIn(signedIn);
          }}
          onCancel={() => setOpen(false)}
        />
      )}
    </>
  );
}

interface StaffLoginModalProps {
  staff: StaffDto[];
  onSignedIn: (staff: StaffDto) => void;
  onCancel: () => void;
}

function StaffLoginModal({ staff, onSignedIn, onCancel }: StaffLoginModalProps) {
  const { t } = useTranslation();
  const [selected, setSelected] = useState<StaffDto | null>(null);
  const [pin, setPin] = useState('');
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState<string | null>(null);

  function reset() {
    setSelected(null);
    setPin('');
    setError(null);
  }

  async function submit() {
    if (!selected) return;
    setBusy(true);
    setError(null);
    try {
      const verified = await api.verifyStaffPin(selected.id, { pin });
      onSignedIn(verified);
    } catch (err) {
      if (err instanceof ApiError && err.code === 'identity.staff_locked') {
        setError(t('staff.locked'));
      } else if (err instanceof ApiError && err.code === 'identity.pin_incorrect') {
        setError(t('staff.pinIncorrect'));
      } else {
        setError(t('error.generic'));
      }
      setPin('');
    } finally {
      setBusy(false);
    }
  }

  return (
    <div className="staff-login-backdrop" role="dialog" aria-modal="true" aria-label={t('staff.signIn')}>
      <div className="staff-login-modal" data-testid="staff-login-modal">
        <h2>{t('staff.signIn')}</h2>

        {!selected ? (
          <ul className="staff-login-list">
            {staff.length === 0 && <p className="empty-state">{t('staff.empty')}</p>}
            {staff.map((member) => (
              <li key={member.id}>
                <button
                  type="button"
                  data-testid={`staff-login-option-${member.name}`}
                  disabled={member.isLocked}
                  title={member.isLocked ? t('staff.locked') : undefined}
                  onClick={() => setSelected(member)}
                >
                  {member.name}
                </button>
              </li>
            ))}
          </ul>
        ) : (
          <div className="staff-login-pin-form">
            <p>{selected.name}</p>
            <input
              type="password"
              inputMode="numeric"
              autoFocus
              value={pin}
              placeholder={t('staff.pinPlaceholder')}
              data-testid="staff-login-pin"
              disabled={busy}
              onChange={(e) => setPin(e.target.value)}
              onKeyDown={(e) => {
                if (e.key === 'Enter' && pin !== '') void submit();
              }}
            />
            {error && (
              <p className="staff-login-error" data-testid="staff-login-error">
                {error}
              </p>
            )}
            <button type="button" data-testid="staff-login-submit" disabled={busy || pin === ''} onClick={() => void submit()}>
              {t('staff.signIn')}
            </button>
            <button type="button" disabled={busy} onClick={reset}>
              {t('staff.back')}
            </button>
          </div>
        )}

        <button type="button" data-testid="staff-login-cancel" onClick={onCancel}>
          {t('staff.cancel')}
        </button>
      </div>
    </div>
  );
}

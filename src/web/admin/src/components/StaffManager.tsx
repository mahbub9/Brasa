import { useCallback, useEffect, useState } from 'react';
import { useTranslation } from 'react-i18next';
import { api, ApiError } from '../api/client';
import type { StaffDto, StaffRole } from '../api/types';

interface StaffManagerProps {
  /** The site to manage staff at. `null` until App.tsx resolves one — see its own remarks on why there's no site-selector yet. */
  siteId: string | null;
  onErrorChange: (message: string | null) => void;
}

const ROLES: StaffRole[] = ['Staff', 'Manager'];

/**
 * IDN-08/09's own admin surface — create staff, see who's locked out, reset
 * a PIN. No delete/deactivate yet, the same "create + list only" narrow
 * first slice IDN-01 itself shipped as. No "verify a PIN" tester here
 * either — that's `pos`'s eventual sign-in screen (WEB-07, not built), not
 * a back-office concern.
 */
export function StaffManager({ siteId, onErrorChange }: StaffManagerProps) {
  const { t } = useTranslation();
  const [staff, setStaff] = useState<StaffDto[] | null>(null);

  const loadStaff = useCallback(() => {
    if (!siteId) {
      return;
    }

    api
      .getStaff(siteId)
      .then(setStaff)
      .catch((err: unknown) => onErrorChange(err instanceof ApiError ? err.message : t('error.generic')));
  }, [siteId, onErrorChange, t]);

  useEffect(() => {
    loadStaff();
  }, [loadStaff]);

  if (!siteId) {
    return <p className="admin-loading">{t('app.loading')}</p>;
  }

  if (!staff) {
    return <p className="admin-loading">{t('app.loading')}</p>;
  }

  return (
    <div className="staff-manager">
      {staff.length === 0 && <p className="empty-state">{t('staff.empty')}</p>}

      {staff.length > 0 && (
        <ul className="staff-list">
          {staff.map((member) => (
            <StaffRow key={member.id} staff={member} onReload={loadStaff} onErrorChange={onErrorChange} />
          ))}
        </ul>
      )}

      <AddStaffForm siteId={siteId} onReload={loadStaff} onErrorChange={onErrorChange} />
    </div>
  );
}

interface StaffRowProps {
  staff: StaffDto;
  onReload: () => void;
  onErrorChange: (message: string | null) => void;
}

function StaffRow({ staff, onReload, onErrorChange }: StaffRowProps) {
  const { t } = useTranslation();
  const [busy, setBusy] = useState(false);
  const [resetting, setResetting] = useState(false);
  const [newPin, setNewPin] = useState('');

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

  function saveNewPin() {
    void run(async () => {
      await api.setStaffPin(staff.id, { pin: newPin });
      setNewPin('');
      setResetting(false);
      onReload();
    });
  }

  return (
    <li className="staff-row" data-testid={`staff-${staff.name}`}>
      <div className="staff-row-summary">
        <span className="staff-row-name">{staff.name}</span>
        <span className={`badge ${staff.role === 'Manager' ? 'badge-on' : 'badge-neutral'}`}>
          {t(`staff.roleOption.${staff.role}`)}
        </span>
        {staff.isLocked && (
          <span className="badge badge-off" data-testid={`staff-locked-${staff.name}`}>
            {t('staff.locked')}
          </span>
        )}
      </div>

      {resetting ? (
        <div className="staff-row-reset">
          <input
            type="text"
            inputMode="numeric"
            placeholder={t('staff.newPinPlaceholder')}
            value={newPin}
            data-testid={`staff-new-pin-${staff.name}`}
            disabled={busy}
            onChange={(e) => setNewPin(e.target.value)}
          />
          <button
            type="button"
            data-testid={`staff-save-pin-${staff.name}`}
            disabled={busy || newPin.trim() === ''}
            onClick={saveNewPin}
          >
            {t('common.save')}
          </button>
          <button
            type="button"
            disabled={busy}
            onClick={() => {
              setNewPin('');
              setResetting(false);
            }}
          >
            {t('common.cancel')}
          </button>
        </div>
      ) : (
        <button
          type="button"
          data-testid={`staff-reset-pin-${staff.name}`}
          disabled={busy}
          onClick={() => setResetting(true)}
        >
          {t('staff.resetPin')}
        </button>
      )}
    </li>
  );
}

interface AddStaffFormProps {
  siteId: string;
  onReload: () => void;
  onErrorChange: (message: string | null) => void;
}

function AddStaffForm({ siteId, onReload, onErrorChange }: AddStaffFormProps) {
  const { t } = useTranslation();
  const [adding, setAdding] = useState(false);
  const [busy, setBusy] = useState(false);
  const [name, setName] = useState('');
  const [role, setRole] = useState<StaffRole>('Staff');
  const [pin, setPin] = useState('');

  function reset() {
    setName('');
    setRole('Staff');
    setPin('');
    setAdding(false);
  }

  function submit() {
    setBusy(true);
    onErrorChange(null);
    api
      .createStaff(siteId, { name, role, pin })
      .then(() => {
        reset();
        onReload();
      })
      .catch((err: unknown) => onErrorChange(err instanceof ApiError ? err.message : t('error.generic')))
      .finally(() => setBusy(false));
  }

  if (!adding) {
    return (
      <button type="button" className="staff-manager-add" data-testid="add-staff" onClick={() => setAdding(true)}>
        + {t('staff.addStaff')}
      </button>
    );
  }

  return (
    <div className="staff-manager-add-form">
      <input
        type="text"
        placeholder={t('staff.namePlaceholder')}
        value={name}
        data-testid="new-staff-name"
        disabled={busy}
        onChange={(e) => setName(e.target.value)}
      />
      <select
        value={role}
        aria-label={t('staff.role')}
        data-testid="new-staff-role"
        disabled={busy}
        onChange={(e) => setRole(e.target.value as StaffRole)}
      >
        {ROLES.map((option) => (
          <option key={option} value={option}>
            {t(`staff.roleOption.${option}`)}
          </option>
        ))}
      </select>
      <input
        type="text"
        inputMode="numeric"
        placeholder={t('staff.pinPlaceholder')}
        value={pin}
        data-testid="new-staff-pin"
        disabled={busy}
        onChange={(e) => setPin(e.target.value)}
      />
      <button
        type="button"
        data-testid="new-staff-save"
        disabled={busy || name.trim() === '' || pin.trim() === ''}
        onClick={submit}
      >
        {t('common.save')}
      </button>
      <button type="button" disabled={busy} onClick={reset}>
        {t('common.cancel')}
      </button>
    </div>
  );
}

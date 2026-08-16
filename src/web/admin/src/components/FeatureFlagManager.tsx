import { useCallback, useEffect, useState } from 'react';
import { useTranslation } from 'react-i18next';
import { Badge } from '@brasa/ui/components/Badge';
import { Button } from '@brasa/ui/components/Button';
import { TextField } from '@brasa/ui/components/TextField';
import { api, ApiError } from '../api/client';
import type { FeatureFlagDto } from '../api/types';

const ALL_PLATFORMS = 'all';

interface FeatureFlagManagerProps {
  onErrorChange: (message: string | null) => void;
}

/**
 * IDN-16's own admin surface — create a flag, see every key/platform
 * combination configured for the tenant, toggle one on or off. No delete —
 * the same "create + list only" narrow first slice IDN-01 itself shipped
 * as; turning a flag off already covers "stop gating on this," and nothing
 * in this codebase reads a flag yet to make removing the row itself matter.
 * Grouped by key in the list, since a key with a platform override reads as
 * one flag with an exception, not two unrelated rows.
 */
export function FeatureFlagManager({ onErrorChange }: FeatureFlagManagerProps) {
  const { t } = useTranslation();
  const [flags, setFlags] = useState<FeatureFlagDto[] | null>(null);

  const loadFlags = useCallback(() => {
    api
      .getFeatureFlags()
      .then(setFlags)
      .catch((err: unknown) => onErrorChange(err instanceof ApiError ? err.message : t('error.generic')));
  }, [onErrorChange, t]);

  useEffect(() => {
    loadFlags();
  }, [loadFlags]);

  if (!flags) {
    return <p className="admin-loading">{t('app.loading')}</p>;
  }

  const keys = [...new Set(flags.map((flag) => flag.key))].sort();

  return (
    <div className="feature-flag-manager">
      {keys.length === 0 && <p className="empty-state">{t('featureFlags.empty')}</p>}

      {keys.length > 0 && (
        <ul className="feature-flag-list">
          {keys.map((key) => (
            <li key={key} className="feature-flag-group" data-testid={`flag-group-${key}`}>
              <span className="feature-flag-key">{key}</span>
              <ul className="feature-flag-rows">
                {flags
                  .filter((flag) => flag.key === key)
                  .sort((a, b) => a.platform.localeCompare(b.platform))
                  .map((flag) => (
                    <FeatureFlagRow key={flag.id} flag={flag} onReload={loadFlags} onErrorChange={onErrorChange} />
                  ))}
              </ul>
            </li>
          ))}
        </ul>
      )}

      <AddFeatureFlagForm onReload={loadFlags} onErrorChange={onErrorChange} />
    </div>
  );
}

interface FeatureFlagRowProps {
  flag: FeatureFlagDto;
  onReload: () => void;
  onErrorChange: (message: string | null) => void;
}

function FeatureFlagRow({ flag, onReload, onErrorChange }: FeatureFlagRowProps) {
  const { t } = useTranslation();
  const [busy, setBusy] = useState(false);

  function toggle() {
    setBusy(true);
    onErrorChange(null);
    api
      .setFeatureFlag(flag.key, { platform: flag.platform, isEnabled: !flag.isEnabled })
      .then(() => onReload())
      .catch((err: unknown) => onErrorChange(err instanceof ApiError ? err.message : t('error.generic')))
      .finally(() => setBusy(false));
  }

  return (
    <li className="feature-flag-row" data-testid={`flag-${flag.key}-${flag.platform}`}>
      <span className="feature-flag-platform">
        {flag.platform === ALL_PLATFORMS ? t('featureFlags.allPlatforms') : flag.platform}
      </span>
      <Badge tone={flag.isEnabled ? 'success' : 'danger'}>
        {flag.isEnabled ? t('featureFlags.enabled') : t('featureFlags.disabled')}
      </Badge>
      <Button variant="secondary" data-testid={`flag-toggle-${flag.key}-${flag.platform}`} disabled={busy} onClick={toggle}>
        {flag.isEnabled ? t('featureFlags.disable') : t('featureFlags.enable')}
      </Button>
    </li>
  );
}

interface AddFeatureFlagFormProps {
  onReload: () => void;
  onErrorChange: (message: string | null) => void;
}

function AddFeatureFlagForm({ onReload, onErrorChange }: AddFeatureFlagFormProps) {
  const { t } = useTranslation();
  const [adding, setAdding] = useState(false);
  const [busy, setBusy] = useState(false);
  const [key, setKey] = useState('');
  const [platform, setPlatform] = useState('');
  const [isEnabled, setIsEnabled] = useState(true);

  function reset() {
    setKey('');
    setPlatform('');
    setIsEnabled(true);
    setAdding(false);
  }

  function submit() {
    setBusy(true);
    onErrorChange(null);
    api
      .setFeatureFlag(key, { platform: platform.trim() === '' ? undefined : platform, isEnabled })
      .then(() => {
        reset();
        onReload();
      })
      .catch((err: unknown) => onErrorChange(err instanceof ApiError ? err.message : t('error.generic')))
      .finally(() => setBusy(false));
  }

  if (!adding) {
    return (
      <Button variant="secondary" className="feature-flag-manager-add" data-testid="add-flag" onClick={() => setAdding(true)}>
        + {t('featureFlags.addFlag')}
      </Button>
    );
  }

  return (
    <div className="feature-flag-manager-add-form">
      <TextField
        type="text"
        placeholder={t('featureFlags.keyPlaceholder')}
        value={key}
        data-testid="new-flag-key"
        disabled={busy}
        onChange={(e) => setKey(e.target.value)}
      />
      <TextField
        type="text"
        placeholder={t('featureFlags.platformPlaceholder')}
        value={platform}
        data-testid="new-flag-platform"
        disabled={busy}
        onChange={(e) => setPlatform(e.target.value)}
      />
      <label className="feature-flag-manager-add-enabled">
        <input
          type="checkbox"
          checked={isEnabled}
          data-testid="new-flag-enabled"
          disabled={busy}
          onChange={(e) => setIsEnabled(e.target.checked)}
        />
        {t('featureFlags.enabled')}
      </label>
      <Button data-testid="new-flag-save" disabled={busy || key.trim() === ''} onClick={submit}>
        {t('common.save')}
      </Button>
      <Button variant="ghost" disabled={busy} onClick={reset}>
        {t('common.cancel')}
      </Button>
    </div>
  );
}

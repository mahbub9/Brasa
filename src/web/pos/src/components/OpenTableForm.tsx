import { useState } from 'react';
import { useTranslation } from 'react-i18next';

interface OpenTableFormProps {
  onOpen: (tableLabel: string, coverCount: number) => void;
  busy: boolean;
}

export function OpenTableForm({ onOpen, busy }: OpenTableFormProps) {
  const { t } = useTranslation();
  const [tableLabel, setTableLabel] = useState('');
  const [coverCount, setCoverCount] = useState(2);

  return (
    <form
      className="open-table-form"
      onSubmit={(e) => {
        e.preventDefault();
        if (tableLabel.trim().length === 0) {
          return;
        }
        onOpen(tableLabel.trim(), coverCount);
      }}
    >
      <h1>{t('openTable.title')}</h1>
      <label>
        {t('openTable.tableLabel')}
        <input
          type="text"
          placeholder={t('openTable.tablePlaceholder')}
          value={tableLabel}
          onChange={(e) => setTableLabel(e.target.value)}
          autoFocus
        />
      </label>
      <label>
        {t('openTable.covers')}
        <input
          type="number"
          min={1}
          value={coverCount}
          onChange={(e) => setCoverCount(Math.max(1, Number(e.target.value)))}
        />
      </label>
      <button type="submit" disabled={busy || tableLabel.trim().length === 0}>
        {busy ? t('openTable.submitBusy') : t('openTable.submit')}
      </button>
    </form>
  );
}

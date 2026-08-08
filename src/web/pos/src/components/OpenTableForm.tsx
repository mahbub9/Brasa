import { useState } from 'react';

interface OpenTableFormProps {
  onOpen: (tableLabel: string, coverCount: number) => void;
  busy: boolean;
}

export function OpenTableForm({ onOpen, busy }: OpenTableFormProps) {
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
      <h1>Abrir mesa</h1>
      <label>
        Table
        <input
          type="text"
          placeholder="e.g. Mesa 12"
          value={tableLabel}
          onChange={(e) => setTableLabel(e.target.value)}
          autoFocus
        />
      </label>
      <label>
        Covers
        <input
          type="number"
          min={1}
          value={coverCount}
          onChange={(e) => setCoverCount(Math.max(1, Number(e.target.value)))}
        />
      </label>
      <button type="submit" disabled={busy || tableLabel.trim().length === 0}>
        {busy ? 'Opening…' : 'Open table'}
      </button>
    </form>
  );
}

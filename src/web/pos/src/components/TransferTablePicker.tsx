import { useTranslation } from 'react-i18next';
import type { RoomDto } from '../api/types';
import { formatTableLabel } from '../lib/tableLabel';

interface TransferTablePickerProps {
  rooms: RoomDto[];
  busy: boolean;
  onSelect: (tableId: string) => void;
  onCancel: () => void;
}

/**
 * Table picker for ORD-12 — moving an open order to a different table
 * mid-service. Only ever lists tables currently `Free`; the floor snapshot
 * is re-fetched right before this opens (see App.tsx's onTransferTable), but
 * the API is still the final word — a 409 here just means someone else beat
 * this request to the table, the same race `openOrderOnAnyFreeTable` handles
 * on the initial-seating side.
 */
export function TransferTablePicker({ rooms, busy, onSelect, onCancel }: TransferTablePickerProps) {
  const { t } = useTranslation();
  const roomsWithFreeTables = rooms
    .map((room) => ({ ...room, tables: room.tables.filter((table) => table.state === 'Free') }))
    .filter((room) => room.tables.length > 0);

  return (
    <div className="transfer-picker-backdrop" role="dialog" aria-modal="true" aria-label={t('transfer.title')}>
      <div className="transfer-picker">
        <h2>{t('transfer.title')}</h2>

        {roomsWithFreeTables.length === 0 ? (
          <p className="empty-state">{t('transfer.noFreeTables')}</p>
        ) : (
          roomsWithFreeTables.map((room) => (
            <section key={room.id} className="transfer-picker-room">
              <h3>{room.name}</h3>
              <div className="transfer-picker-tables">
                {room.tables.map((table) => (
                  <button
                    key={table.id}
                    type="button"
                    data-testid={`transfer-target-${table.label}`}
                    disabled={busy}
                    onClick={() => onSelect(table.id)}
                  >
                    {formatTableLabel(table.label, t)}
                  </button>
                ))}
              </div>
            </section>
          ))
        )}

        <button type="button" data-testid="cancel-transfer" onClick={onCancel} disabled={busy}>
          {t('transfer.cancel')}
        </button>
      </div>
    </div>
  );
}

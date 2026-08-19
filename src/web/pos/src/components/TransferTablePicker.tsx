import { useTranslation } from 'react-i18next';
import { Button } from '@brasa/ui/components/Button';
import { Modal, ModalActions } from '@brasa/ui/components/Modal';
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
    <Modal title={t('transfer.title')} className="transfer-picker">
      {roomsWithFreeTables.length === 0 ? (
        <p className="empty-state">{t('transfer.noFreeTables')}</p>
      ) : (
        roomsWithFreeTables.map((room) => (
          <section key={room.id} className="transfer-picker-room">
            <h3 className="brasa-eyebrow">{room.name}</h3>
            <div className="transfer-picker-tables">
              {room.tables.map((table) => (
                <Button
                  key={table.id}
                  variant="secondary"
                  data-testid={`transfer-target-${table.label}`}
                  disabled={busy}
                  onClick={() => onSelect(table.id)}
                >
                  {formatTableLabel(table.label, t)}
                </Button>
              ))}
            </div>
          </section>
        ))
      )}

      <ModalActions>
        <Button variant="ghost" data-testid="cancel-transfer" onClick={onCancel} disabled={busy}>
          {t('transfer.cancel')}
        </Button>
      </ModalActions>
    </Modal>
  );
}

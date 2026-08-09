import { useState } from 'react';
import { useTranslation } from 'react-i18next';
import type { RoomDto, TableDto } from '../api/types';

interface TablePickerProps {
  rooms: RoomDto[];
  busy: boolean;
  onOpenTable: (tableId: string, coverCount: number) => void;
  onClearTable: (tableId: string) => void;
}

/**
 * I1's table picker — a static grid per room, not the drag-and-drop editor
 * (FLR-03, not built). Table.PositionX/Y exist in the API response for that
 * future editor; this screen deliberately ignores them and lays tables out
 * in a plain responsive grid instead.
 */
export function TablePicker({ rooms, busy, onOpenTable, onClearTable }: TablePickerProps) {
  const { t } = useTranslation();
  const [selectedTableId, setSelectedTableId] = useState<string | null>(null);
  const [coverCount, setCoverCount] = useState(2);

  if (rooms.length === 0) {
    return <p className="empty-state">{t('floor.empty')}</p>;
  }

  function handleTableClick(table: TableDto) {
    if (table.state === 'Free') {
      setSelectedTableId(table.id);
      setCoverCount(2);
    } else if (table.state === 'Dirty') {
      onClearTable(table.id);
    }
  }

  return (
    <div className="table-picker">
      <h1>{t('floor.title')}</h1>
      {rooms.map((room) => (
        <section key={room.id} className="floor-room">
          <h2>{room.name}</h2>
          <div className="floor-tables">
            {room.tables.map((table) => (
              <div key={table.id} className={`floor-table floor-table-${table.state}`}>
                <button
                  type="button"
                  data-testid={`table-${table.label}`}
                  disabled={busy || table.state === 'Occupied' || table.state === 'BillRequested'}
                  onClick={() => handleTableClick(table)}
                >
                  <span className="floor-table-label">{table.label}</span>
                  <span className="floor-table-seats">{t('floor.seats', { count: table.seats })}</span>
                  <span className="floor-table-state">{t(`floor.state.${table.state}`)}</span>
                </button>

                {selectedTableId === table.id && (
                  <div className="floor-table-confirm" data-testid="table-confirm">
                    <label>
                      {t('floor.covers')}
                      <input
                        type="number"
                        min={1}
                        value={coverCount}
                        onChange={(e) => setCoverCount(Math.max(1, Number(e.target.value)))}
                      />
                    </label>
                    <button
                      type="button"
                      data-testid="confirm-open-table"
                      disabled={busy}
                      onClick={() => onOpenTable(table.id, coverCount)}
                    >
                      {busy ? t('floor.opening') : t('floor.open')}
                    </button>
                  </div>
                )}
              </div>
            ))}
          </div>
        </section>
      ))}
    </div>
  );
}

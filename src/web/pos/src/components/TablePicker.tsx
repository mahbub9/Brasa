import { useState } from 'react';
import { useTranslation } from 'react-i18next';
import { Badge } from '@brasa/ui/components/Badge';
import type { BadgeTone } from '@brasa/ui/components/Badge';
import { Button } from '@brasa/ui/components/Button';
import { TextField } from '@brasa/ui/components/TextField';
import type { RoomDto, TableDto } from '../api/types';
import { formatTableLabel } from '../lib/tableLabel';

const STATE_TONE: Record<TableDto['state'], BadgeTone> = {
  Free: 'success',
  Occupied: 'danger',
  BillRequested: 'warning',
  Dirty: 'neutral',
};

interface TablePickerProps {
  rooms: RoomDto[];
  busy: boolean;
  onOpenTable: (tableId: string, coverCount: number) => void;
  onClearTable: (tableId: string) => void;
  onOpenTakeaway: (label: string) => void;
}

/**
 * I1's table picker — a static grid per room, not the drag-and-drop editor
 * (FLR-03, not built). Table.PositionX/Y exist in the API response for that
 * future editor; this screen deliberately ignores them and lays tables out
 * in a plain responsive grid instead.
 */
export function TablePicker({ rooms, busy, onOpenTable, onClearTable, onOpenTakeaway }: TablePickerProps) {
  const { t } = useTranslation();
  const [selectedTableId, setSelectedTableId] = useState<string | null>(null);
  const [coverCount, setCoverCount] = useState(2);
  const [showTakeaway, setShowTakeaway] = useState(false);
  const [takeawayLabel, setTakeawayLabel] = useState('');

  // A floor heading only earns its place once a tenant's own rooms
  // actually span more than one level (FLR-07) -- most restaurants are
  // single-storey, so the common case renders exactly as it always has.
  // Each floor's rooms keep the backend's own DisplayOrder sequence;
  // only the floor groups themselves are sorted, ascending.
  const showFloors = new Set(rooms.map((r) => r.floorLevel)).size > 1;
  const floorGroups: { floorLevel: number; rooms: RoomDto[] }[] = showFloors
    ? [...new Set(rooms.map((r) => r.floorLevel))]
        .sort((a, b) => a - b)
        .map((floorLevel) => ({ floorLevel, rooms: rooms.filter((r) => r.floorLevel === floorLevel) }))
    : [{ floorLevel: 0, rooms }];

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

      <div className="takeaway-picker">
        {showTakeaway ? (
          <div className="takeaway-confirm" data-testid="takeaway-confirm">
            <TextField
              type="text"
              placeholder={t('floor.takeawayLabelPlaceholder')}
              value={takeawayLabel}
              onChange={(e) => setTakeawayLabel(e.target.value)}
              data-testid="takeaway-label-input"
            />
            <Button
              data-testid="confirm-open-takeaway"
              disabled={busy}
              onClick={() => {
                onOpenTakeaway(takeawayLabel);
                setTakeawayLabel('');
                setShowTakeaway(false);
              }}
            >
              {busy ? t('floor.opening') : t('floor.open')}
            </Button>
          </div>
        ) : (
          <Button variant="secondary" data-testid="new-takeaway-button" disabled={busy} onClick={() => setShowTakeaway(true)}>
            {t('floor.newTakeaway')}
          </Button>
        )}
      </div>

      {rooms.length === 0 && <p className="empty-state">{t('floor.empty')}</p>}
      {floorGroups.map((group) => (
        <div key={group.floorLevel} className="floor-level-group">
          {showFloors && (
            <h1 className="floor-level-heading" data-testid={`floor-level-${group.floorLevel}`}>
              {t('floor.floorBadge', { level: group.floorLevel })}
            </h1>
          )}
          {group.rooms.map((room) => (
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
                      <span className="floor-table-label">{formatTableLabel(table.label, t)}</span>
                      <span className="floor-table-seats">{t('floor.seats', { count: table.seats })}</span>
                      <Badge tone={STATE_TONE[table.state]} className="floor-table-state">
                        {t(`floor.state.${table.state}`)}
                      </Badge>
                    </button>

                    {selectedTableId === table.id && (
                      <div className="floor-table-confirm" data-testid="table-confirm">
                        <label>
                          {t('floor.covers')}
                          <TextField
                            type="number"
                            min={1}
                            value={coverCount}
                            onChange={(e) => setCoverCount(Math.max(1, Number(e.target.value)))}
                          />
                        </label>
                        <Button data-testid="confirm-open-table" disabled={busy} onClick={() => onOpenTable(table.id, coverCount)}>
                          {busy ? t('floor.opening') : t('floor.open')}
                        </Button>
                      </div>
                    )}
                  </div>
                ))}
              </div>
            </section>
          ))}
        </div>
      ))}
    </div>
  );
}

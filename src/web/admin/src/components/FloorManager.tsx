import { useState } from 'react';
import { useTranslation } from 'react-i18next';
import { api, ApiError } from '../api/client';
import type { RoomDto, TableDto, TableShape } from '../api/types';

interface FloorManagerProps {
  rooms: RoomDto[];
  onReload: () => void;
  onErrorChange: (message: string | null) => void;
}

const SHAPES: TableShape[] = ['Round', 'Square', 'Rectangle'];

/**
 * FLR-03's first slice — table and room CRUD, not the drag-and-drop canvas
 * its own backlog title names. Position and shape are plain number/select
 * inputs here, the same "mechanism before the visual affordance" call
 * WEB-10 made for the menu editor.
 */
export function FloorManager({ rooms, onReload, onErrorChange }: FloorManagerProps) {
  const { t } = useTranslation();

  // A "Floor N" badge only earns its place once a tenant's own rooms
  // actually span more than one level (FLR-07) -- most restaurants are
  // single-storey, and showing "Floor 0" on every room when there is only
  // ever one floor would be pure noise, not information.
  const showFloors = new Set(rooms.map((r) => r.floorLevel)).size > 1;

  return (
    <div className="floor-manager">
      {rooms.length === 0 && <p className="empty-state">{t('floor.empty')}</p>}

      {rooms.map((room) => (
        <section key={room.id} className="floor-manager-room" data-testid={`room-${room.name}`}>
          <FloorManagerRoomHeading room={room} showFloors={showFloors} onReload={onReload} onErrorChange={onErrorChange} />

          {room.tables.length === 0 ? (
            <p className="empty-state">{t('floor.noTables')}</p>
          ) : (
            <ul className="floor-manager-tables">
              {room.tables.map((table) => (
                <FloorManagerTable key={table.id} table={table} onReload={onReload} onErrorChange={onErrorChange} />
              ))}
            </ul>
          )}

          <AddTableForm roomId={room.id} onReload={onReload} onErrorChange={onErrorChange} />
        </section>
      ))}

      <AddRoomForm nextDisplayOrder={rooms.length} onReload={onReload} onErrorChange={onErrorChange} />
    </div>
  );
}

interface FloorManagerRoomHeadingProps {
  room: RoomDto;
  showFloors: boolean;
  onReload: () => void;
  onErrorChange: (message: string | null) => void;
}

function FloorManagerRoomHeading({ room, showFloors, onReload, onErrorChange }: FloorManagerRoomHeadingProps) {
  const { t } = useTranslation();
  const [busy, setBusy] = useState(false);
  const [editing, setEditing] = useState(false);
  const [nameDraft, setNameDraft] = useState(room.name);
  const [floorLevelDraft, setFloorLevelDraft] = useState(String(room.floorLevel));
  const [confirmingDelete, setConfirmingDelete] = useState(false);
  const canDelete = room.tables.length === 0;

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

  function saveRename() {
    const floorLevel = Number(floorLevelDraft);
    if (Number.isNaN(floorLevel)) {
      return;
    }
    void run(async () => {
      await api.updateRoom(room.id, { name: nameDraft, displayOrder: room.displayOrder, floorLevel });
      setEditing(false);
      onReload();
    });
  }

  function confirmDelete() {
    void run(async () => {
      await api.deleteRoom(room.id);
      onReload();
    });
  }

  if (editing) {
    return (
      <div className="floor-manager-room-heading-edit">
        <input
          type="text"
          value={nameDraft}
          data-testid={`room-name-input-${room.name}`}
          disabled={busy}
          onChange={(e) => setNameDraft(e.target.value)}
        />
        <input
          type="number"
          value={floorLevelDraft}
          aria-label={t('floor.floorLevel')}
          data-testid={`room-floor-level-input-${room.name}`}
          disabled={busy}
          onChange={(e) => setFloorLevelDraft(e.target.value)}
        />
        <button type="button" data-testid={`room-name-save-${room.name}`} disabled={busy} onClick={saveRename}>
          {t('common.save')}
        </button>
        <button
          type="button"
          disabled={busy}
          onClick={() => {
            setNameDraft(room.name);
            setFloorLevelDraft(String(room.floorLevel));
            setEditing(false);
          }}
        >
          {t('common.cancel')}
        </button>
      </div>
    );
  }

  return (
    <div className="floor-manager-room-heading">
      <button
        type="button"
        className="floor-manager-room-name"
        data-testid={`room-name-edit-${room.name}`}
        disabled={busy}
        onClick={() => {
          setNameDraft(room.name);
          setFloorLevelDraft(String(room.floorLevel));
          setEditing(true);
        }}
      >
        <h2>{room.name}</h2>
        {showFloors && (
          <span className="badge badge-neutral" data-testid={`room-floor-badge-${room.name}`}>
            {t('floor.floorBadge', { level: room.floorLevel })}
          </span>
        )}
      </button>

      {confirmingDelete ? (
        <span className="floor-manager-room-delete-confirm">
          {t('menu.deleteConfirm')}
          <button type="button" data-testid={`room-delete-confirm-${room.name}`} disabled={busy} onClick={confirmDelete}>
            {t('common.yes')}
          </button>
          <button type="button" disabled={busy} onClick={() => setConfirmingDelete(false)}>
            {t('common.no')}
          </button>
        </span>
      ) : (
        <button
          type="button"
          data-testid={`room-delete-${room.name}`}
          disabled={busy || !canDelete}
          title={canDelete ? undefined : t('floor.cannotDeleteRoomNotEmpty')}
          onClick={() => setConfirmingDelete(true)}
        >
          {t('floor.deleteRoom')}
        </button>
      )}
    </div>
  );
}

interface AddRoomFormProps {
  nextDisplayOrder: number;
  onReload: () => void;
  onErrorChange: (message: string | null) => void;
}

function AddRoomForm({ nextDisplayOrder, onReload, onErrorChange }: AddRoomFormProps) {
  const { t } = useTranslation();
  const [adding, setAdding] = useState(false);
  const [busy, setBusy] = useState(false);
  const [name, setName] = useState('');
  const [floorLevel, setFloorLevel] = useState('0');

  function reset() {
    setName('');
    setFloorLevel('0');
    setAdding(false);
  }

  function submit() {
    const floorLevelNumber = Number(floorLevel);
    if (Number.isNaN(floorLevelNumber)) {
      return;
    }
    setBusy(true);
    onErrorChange(null);
    api
      .createRoom({ name, displayOrder: nextDisplayOrder, floorLevel: floorLevelNumber })
      .then(() => {
        reset();
        onReload();
      })
      .catch((err: unknown) => onErrorChange(err instanceof ApiError ? err.message : t('error.generic')))
      .finally(() => setBusy(false));
  }

  if (!adding) {
    return (
      <button type="button" className="floor-manager-add-room" data-testid="add-room" onClick={() => setAdding(true)}>
        + {t('floor.addRoom')}
      </button>
    );
  }

  return (
    <div className="floor-manager-add-room-form">
      <input
        type="text"
        placeholder={t('floor.roomNamePlaceholder')}
        value={name}
        data-testid="new-room-name"
        disabled={busy}
        onChange={(e) => setName(e.target.value)}
      />
      <input
        type="number"
        value={floorLevel}
        aria-label={t('floor.floorLevel')}
        data-testid="new-room-floor-level"
        disabled={busy}
        onChange={(e) => setFloorLevel(e.target.value)}
      />
      <button type="button" data-testid="new-room-save" disabled={busy || name.trim() === ''} onClick={submit}>
        {t('common.save')}
      </button>
      <button type="button" disabled={busy} onClick={reset}>
        {t('common.cancel')}
      </button>
    </div>
  );
}

interface FloorManagerTableProps {
  table: TableDto;
  onReload: () => void;
  onErrorChange: (message: string | null) => void;
}

function FloorManagerTable({ table, onReload, onErrorChange }: FloorManagerTableProps) {
  const { t } = useTranslation();
  const [busy, setBusy] = useState(false);
  const [editing, setEditing] = useState(false);
  const [labelDraft, setLabelDraft] = useState(table.label);
  const [seatsDraft, setSeatsDraft] = useState(String(table.seats));
  const [shapeDraft, setShapeDraft] = useState<TableShape>(table.shape);
  const [xDraft, setXDraft] = useState(String(table.positionX));
  const [yDraft, setYDraft] = useState(String(table.positionY));
  const [confirmingDelete, setConfirmingDelete] = useState(false);

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

  function startEditing() {
    setLabelDraft(table.label);
    setSeatsDraft(String(table.seats));
    setShapeDraft(table.shape);
    setXDraft(String(table.positionX));
    setYDraft(String(table.positionY));
    setEditing(true);
  }

  function saveEdit() {
    const seats = Number(seatsDraft);
    const positionX = Number(xDraft);
    const positionY = Number(yDraft);
    if (Number.isNaN(seats) || Number.isNaN(positionX) || Number.isNaN(positionY)) {
      return;
    }
    void run(async () => {
      await api.updateTable(table.id, { label: labelDraft, seats, positionX, positionY, shape: shapeDraft });
      setEditing(false);
      onReload();
    });
  }

  function confirmDelete() {
    void run(async () => {
      await api.deleteTable(table.id);
      onReload();
    });
  }

  const canDelete = table.state === 'Free';

  return (
    <li className="floor-manager-table" data-testid={`table-${table.label}`}>
      {editing ? (
        <div className="floor-manager-table-edit">
          <input
            type="text"
            value={labelDraft}
            data-testid={`table-label-input-${table.label}`}
            disabled={busy}
            onChange={(e) => setLabelDraft(e.target.value)}
          />
          <input
            type="number"
            min="1"
            value={seatsDraft}
            data-testid={`table-seats-input-${table.label}`}
            disabled={busy}
            onChange={(e) => setSeatsDraft(e.target.value)}
          />
          <select
            value={shapeDraft}
            data-testid={`table-shape-input-${table.label}`}
            disabled={busy}
            onChange={(e) => setShapeDraft(e.target.value as TableShape)}
          >
            {SHAPES.map((shape) => (
              <option key={shape} value={shape}>
                {t(`floor.shapeOption.${shape}`)}
              </option>
            ))}
          </select>
          <input
            type="number"
            value={xDraft}
            aria-label={t('floor.positionX')}
            data-testid={`table-x-input-${table.label}`}
            disabled={busy}
            onChange={(e) => setXDraft(e.target.value)}
          />
          <input
            type="number"
            value={yDraft}
            aria-label={t('floor.positionY')}
            data-testid={`table-y-input-${table.label}`}
            disabled={busy}
            onChange={(e) => setYDraft(e.target.value)}
          />
          <button type="button" data-testid={`table-save-${table.label}`} disabled={busy} onClick={saveEdit}>
            {t('common.save')}
          </button>
          <button type="button" disabled={busy} onClick={() => setEditing(false)}>
            {t('common.cancel')}
          </button>
        </div>
      ) : (
        <div className="floor-manager-table-row">
          <button
            type="button"
            className="floor-manager-table-label"
            data-testid={`table-edit-${table.label}`}
            disabled={busy}
            onClick={startEditing}
          >
            {table.label}
          </button>
          <span className="floor-manager-table-detail">{t('floor.seatsCount', { count: table.seats })}</span>
          <span className="floor-manager-table-detail">{t(`floor.shapeOption.${table.shape}`)}</span>
          <span className={`badge badge-${canDelete ? 'on' : 'off'}`}>{t(`floor.state.${table.state}`)}</span>

          {confirmingDelete ? (
            <span className="floor-manager-table-delete-confirm">
              {t('menu.deleteConfirm')}
              <button
                type="button"
                data-testid={`table-delete-confirm-${table.label}`}
                disabled={busy}
                onClick={confirmDelete}
              >
                {t('common.yes')}
              </button>
              <button type="button" disabled={busy} onClick={() => setConfirmingDelete(false)}>
                {t('common.no')}
              </button>
            </span>
          ) : (
            <button
              type="button"
              data-testid={`table-delete-${table.label}`}
              disabled={busy || !canDelete}
              title={canDelete ? undefined : t('floor.cannotDeleteNotFree')}
              onClick={() => setConfirmingDelete(true)}
            >
              {t('floor.deleteTable')}
            </button>
          )}
        </div>
      )}
    </li>
  );
}

interface AddTableFormProps {
  roomId: string;
  onReload: () => void;
  onErrorChange: (message: string | null) => void;
}

function AddTableForm({ roomId, onReload, onErrorChange }: AddTableFormProps) {
  const { t } = useTranslation();
  const [adding, setAdding] = useState(false);
  const [busy, setBusy] = useState(false);
  const [label, setLabel] = useState('');
  const [seats, setSeats] = useState('2');
  const [shape, setShape] = useState<TableShape>('Round');

  function reset() {
    setLabel('');
    setSeats('2');
    setShape('Round');
    setAdding(false);
  }

  function submit() {
    const seatsNumber = Number(seats);
    if (Number.isNaN(seatsNumber)) {
      return;
    }
    setBusy(true);
    onErrorChange(null);
    api
      .createTable(roomId, { label, seats: seatsNumber, positionX: 0, positionY: 0, shape })
      .then(() => {
        reset();
        onReload();
      })
      .catch((err: unknown) => onErrorChange(err instanceof ApiError ? err.message : t('error.generic')))
      .finally(() => setBusy(false));
  }

  if (!adding) {
    return (
      <button type="button" className="floor-manager-add-table" data-testid={`add-table-${roomId}`} onClick={() => setAdding(true)}>
        + {t('floor.addTable')}
      </button>
    );
  }

  return (
    <div className="floor-manager-add-table-form">
      <input
        type="text"
        placeholder={t('floor.tableLabelPlaceholder')}
        value={label}
        data-testid={`new-table-label-${roomId}`}
        disabled={busy}
        onChange={(e) => setLabel(e.target.value)}
      />
      <input
        type="number"
        min="1"
        value={seats}
        data-testid={`new-table-seats-${roomId}`}
        disabled={busy}
        onChange={(e) => setSeats(e.target.value)}
      />
      <select
        value={shape}
        data-testid={`new-table-shape-${roomId}`}
        disabled={busy}
        onChange={(e) => setShape(e.target.value as TableShape)}
      >
        {SHAPES.map((s) => (
          <option key={s} value={s}>
            {t(`floor.shapeOption.${s}`)}
          </option>
        ))}
      </select>
      <button type="button" data-testid={`new-table-save-${roomId}`} disabled={busy || label.trim() === ''} onClick={submit}>
        {t('common.save')}
      </button>
      <button type="button" disabled={busy} onClick={reset}>
        {t('common.cancel')}
      </button>
    </div>
  );
}

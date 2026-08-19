// Mirrors src/backend/Brasa.Api/Contracts/*.cs — only the fields the admin
// shell actually reads or sends. See src/web/pos/src/api/types.ts for the
// fuller shape; duplicated rather than shared until a change actually needs
// sharing across clients — see that file's own comment (WEB-03/API-15).

export interface MoneyDto {
  amount: number;
  currency: string;
}

/**
 * A menu item's recurring day/time availability window (CAT-11) — a *prato
 * do dia*. `daysOfWeek` entries are English DayOfWeek names ("Monday"),
 * Monday first; `startTime`/`endTime` are local "HH:mm", start inclusive,
 * end exclusive.
 */
export interface MenuItemScheduleDto {
  daysOfWeek: string[];
  startTime: string;
  endTime: string;
}

export interface MenuItemDto {
  id: string;
  name: string;
  description: string | null;
  /** Path to an uploaded photo (CAT-02), e.g. "/uploads/menu-items/{id}.jpg", or null if none set. */
  imageUrl: string | null;
  price: MoneyDto;
  takeawayPrice: MoneyDto | null;
  vatRatePercent: number;
  isAlcoholic: boolean;
  isAvailable: boolean;
  schedule: MenuItemScheduleDto | null;
  isCouvert: boolean;
  allergens: string[];
}

/**
 * Unlike the POS-facing `MenuCategoryDto`, this carries `isVisible` — it
 * only ever comes from `GET /menu/all`, which deliberately returns hidden
 * categories and unavailable items too, so admin can show (and un-hide)
 * what `GET /menu` would otherwise filter out entirely. See
 * CatalogEndpoints.GetMenuAllAsync's own remarks.
 */
export interface AdminMenuCategoryDto {
  id: string;
  name: string;
  displayOrder: number;
  isVisible: boolean;
  items: MenuItemDto[];
}

export interface UpdateMenuItemAvailabilityRequest {
  isAvailable: boolean;
}

export interface UpdateMenuItemPriceRequest {
  price: number;
}

export interface UpdateMenuItemTakeawayPriceRequest {
  price: number | null;
}

/** All three null/empty clears the schedule; all three required together to set one (CAT-11). */
export interface UpdateMenuItemScheduleRequest {
  daysOfWeek: string[] | null;
  startTime: string | null;
  endTime: string | null;
}

export interface UpdateMenuItemCouvertRequest {
  isCouvert: boolean;
}

export interface UpdateMenuCategoryVisibilityRequest {
  isVisible: boolean;
}

export interface ImportMenuItemsRowError {
  rowNumber: number;
  message: string;
}

export interface ImportMenuItemsResponse {
  created: number;
  errors: ImportMenuItemsRowError[];
}

export type TableState = 'Free' | 'Occupied' | 'BillRequested' | 'Dirty';
export type TableShape = 'Round' | 'Square' | 'Rectangle';

export interface TableDto {
  id: string;
  roomId: string;
  label: string;
  seats: number;
  positionX: number;
  positionY: number;
  shape: TableShape;
  state: TableState;
}

export interface RoomDto {
  id: string;
  name: string;
  displayOrder: number;
  /** Which physical storey this room sits on (FLR-07) -- 0 ground floor, positive above it, negative below. */
  floorLevel: number;
  /** The waiter assigned to this room as their section (FLR-06), or null if unassigned. Both null together. */
  assignedStaffId: string | null;
  assignedStaffName: string | null;
  tables: TableDto[];
}

export interface CreateTableRequest {
  label: string;
  seats: number;
  positionX: number;
  positionY: number;
  shape: TableShape;
}

export interface UpdateTableRequest {
  label: string;
  seats: number;
  positionX: number;
  positionY: number;
  shape: TableShape;
}

export interface CreateRoomRequest {
  name: string;
  displayOrder: number;
  floorLevel: number;
}

export interface UpdateRoomRequest {
  name: string;
  displayOrder: number;
  floorLevel: number;
}

/** Assigns (or clears, with null) a room's section waiter (FLR-06). */
export interface AssignRoomSectionRequest {
  staffId: string | null;
}

/** The top of the Organization -> Site -> Terminal hierarchy (IDN-01). */
export interface OrganizationDto {
  id: string;
  name: string;
}

/** A physical restaurant location. */
export interface SiteDto {
  id: string;
  organizationId: string;
  name: string;
  region: string;
}

export type StaffRole = 'Staff' | 'Manager';

/** A staff member who can sign in with a PIN (IDN-08/09). Never carries the PIN itself. */
export interface StaffDto {
  id: string;
  siteId: string;
  name: string;
  role: StaffRole;
  isLocked: boolean;
}

export interface CreateStaffRequest {
  name: string;
  role: StaffRole;
  pin: string;
}

export interface StaffPinRequest {
  pin: string;
}

/** A physical POS device registered at a site (IDN-01). No pairing/auth yet. */
export interface TerminalDto {
  id: string;
  siteId: string;
  label: string;
}

/**
 * A cash session — *abertura de caixa* (PAY-08) — a staff member's starting
 * float declaration against a terminal. Purely a record; nothing else in
 * this codebase requires one to exist yet. `terminalLabel`/
 * `openedByStaffName` are resolved server-side, never editable here.
 */
export interface CashSessionDto {
  id: string;
  terminalId: string;
  terminalLabel: string;
  openedByStaffId: string;
  openedByStaffName: string;
  openingFloat: MoneyDto;
  openedAtUtc: string;
  closedAtUtc: string | null;
  isOpen: boolean;
}

export interface OpenCashSessionRequest {
  terminalId: string;
  staffId: string;
  openingFloat: number;
}

export type CashMovementDirection = 'PayIn' | 'PayOut';

/** A pay-in or pay-out against an open cash session (PAY-09) — always requires a reason. */
export interface CashMovementDto {
  id: string;
  cashSessionId: string;
  direction: CashMovementDirection;
  amount: MoneyDto;
  reason: string;
  recordedByStaffId: string;
  recordedByStaffName: string;
  recordedAtUtc: string;
}

export interface RecordCashMovementRequest {
  direction: CashMovementDirection;
  amount: number;
  reason: string;
  staffId: string;
}

/** A per-tenant, optionally per-platform on/off switch (IDN-16). `platform` is never empty — `"all"` means every platform. */
export interface FeatureFlagDto {
  id: string;
  key: string;
  platform: string;
  isEnabled: boolean;
}

export interface SetFeatureFlagRequest {
  platform?: string;
  isEnabled: boolean;
}

/** RFC 9457 problem response shape used for every API failure. */
export interface ProblemDetails {
  title?: string;
  status?: number;
  detail?: string;
  code?: string;
}

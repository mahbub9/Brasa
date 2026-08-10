// Mirrors src/backend/Brasa.Api/Contracts/*.cs — only the fields the admin
// shell actually reads or sends. See src/web/pos/src/api/types.ts for the
// fuller shape; duplicated rather than shared until a change actually needs
// sharing across clients — see that file's own comment (WEB-03/API-15).

export interface MoneyDto {
  amount: number;
  currency: string;
}

export interface MenuItemDto {
  id: string;
  name: string;
  description: string | null;
  price: MoneyDto;
  takeawayPrice: MoneyDto | null;
  vatRatePercent: number;
  isAlcoholic: boolean;
  isAvailable: boolean;
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
}

export interface UpdateRoomRequest {
  name: string;
  displayOrder: number;
}

/** RFC 9457 problem response shape used for every API failure. */
export interface ProblemDetails {
  title?: string;
  status?: number;
  detail?: string;
  code?: string;
}

// Mirrors src/backend/Brasa.Api/Contracts/*.cs. Kept hand-written for I0 — a
// generated client from the OpenAPI document (docs/architecture/api-contract.md)
// replaces this once more than one client app needs it.

export interface MoneyDto {
  amount: number;
  currency: string;
}

export interface MenuItemDto {
  id: string;
  name: string;
  price: MoneyDto;
  vatRatePercent: number;
  isAlcoholic: boolean;
  isAvailable: boolean;
}

export interface MenuCategoryDto {
  id: string;
  name: string;
  displayOrder: number;
  items: MenuItemDto[];
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

export interface OpenOrderRequest {
  tableId: string;
  coverCount: number;
}

export interface AddLineRequest {
  menuItemId: string;
  quantity: number;
}

export interface OrderLineDto {
  id: string;
  menuItemId: string;
  itemName: string;
  unitPrice: MoneyDto;
  quantity: number;
  lineTotal: MoneyDto;
}

export interface OrderDto {
  id: string;
  tableId: string;
  tableLabel: string;
  coverCount: number;
  status: 'Open' | 'Closed';
  total: MoneyDto;
  lines: OrderLineDto[];
}

export interface FiscalDocumentDto {
  documentNumber: string;
  atcud: string;
  netTotal: MoneyDto;
  vatTotal: MoneyDto;
  grossTotal: MoneyDto;
  qrPayload: string;
  issuedAtUtc: string;
}

export interface CloseOrderResponse {
  order: OrderDto;
  document: FiscalDocumentDto;
}

/** RFC 9457 problem response shape used for every API failure. */
export interface ProblemDetails {
  title?: string;
  status?: number;
  detail?: string;
  code?: string;
}

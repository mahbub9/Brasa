// Mirrors src/backend/Brasa.Api/Contracts/*.cs. Kept hand-written for I0 — a
// generated client from the OpenAPI document (docs/architecture/api-contract.md)
// replaces this once more than one client app needs it.

export interface MoneyDto {
  amount: number;
  currency: string;
}

export interface ModifierDto {
  id: string;
  name: string;
  priceDelta: MoneyDto;
}

export interface ModifierGroupDto {
  id: string;
  name: string;
  isRequired: boolean;
  minSelect: number;
  maxSelect: number;
  modifiers: ModifierDto[];
}

export interface MenuItemDto {
  id: string;
  name: string;
  price: MoneyDto;
  vatRatePercent: number;
  isAlcoholic: boolean;
  isAvailable: boolean;
  modifierGroups: ModifierGroupDto[];
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
  selectedModifierIds?: string[];
}

export interface SetLineNotesRequest {
  notes: string | null;
}

export interface TransferOrderRequest {
  newTableId: string;
}

export interface OrderLineModifierDto {
  id: string;
  name: string;
  priceDelta: MoneyDto;
}

export interface OrderLineDto {
  id: string;
  menuItemId: string;
  itemName: string;
  unitPrice: MoneyDto;
  quantity: number;
  modifiers: OrderLineModifierDto[];
  lineTotal: MoneyDto;
  notes: string | null;
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

/** One VAT band's subtotal on a pre-bill, e.g. the 13% and 23% lines shown separately. */
export interface VatBreakdownDto {
  vatRateFraction: number;
  netTotal: MoneyDto;
  vatAmount: MoneyDto;
  grossTotal: MoneyDto;
}

/**
 * A pre-bill preview — a *documento não fiscal*, never an invoice. No
 * document number, no ATCUD, no QR: nothing is issued when this is fetched,
 * so it is safe to request repeatedly (a "reprint"). See ADR notes in
 * OrderDtos.cs (ORD-18/19).
 */
export interface PreBillDto {
  orderId: string;
  tableLabel: string;
  coverCount: number;
  lines: OrderLineDto[];
  vatBreakdown: VatBreakdownDto[];
  total: MoneyDto;
  generatedAtUtc: string;
  documentKind: 'documento_nao_fiscal';
}

/** RFC 9457 problem response shape used for every API failure. */
export interface ProblemDetails {
  title?: string;
  status?: number;
  detail?: string;
  code?: string;
}

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
  description: string | null;
  /** Path to an uploaded photo (CAT-02), e.g. "/uploads/menu-items/{id}.jpg", relative to the API's own origin, not `/api/v1`. Null if none set. */
  imageUrl: string | null;
  price: MoneyDto;
  takeawayPrice: MoneyDto | null;
  vatRatePercent: number;
  isAlcoholic: boolean;
  isAvailable: boolean;
  isCouvert: boolean;
  allergens: string[];
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
  /** Which physical storey this room sits on (FLR-07) -- 0 ground floor, positive above it, negative below. */
  floorLevel: number;
  /** The waiter assigned to this room as their section (FLR-06), or null if unassigned. Both null together. */
  assignedStaffId: string | null;
  assignedStaffName: string | null;
  tables: TableDto[];
}

export interface OpenOrderRequest {
  tableId: string;
  coverCount: number;
}

export interface OpenTakeawayOrderRequest {
  label: string | null;
}

export interface AddLineRequest {
  menuItemId: string;
  quantity: number;
  selectedModifierIds?: string[];
}

export interface SetLineNotesRequest {
  notes: string | null;
}

export interface SetLineQuantityRequest {
  quantity: number;
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
  /** Which course the item was served at, snapshotted at add-time (ORD-07). Null if the item had none. */
  course: string | null;
  /** Whether this line has been sent to the kitchen (ORD-07/08). */
  isFired: boolean;
  firedAtUtc: string | null;
}

/** Request body to send unfired lines to the kitchen (ORD-07/08). Null course fires everything still pending. */
export interface FireOrderLinesRequest {
  course: string | null;
}

export interface OrderDto {
  id: string;
  tableId: string;
  tableLabel: string;
  coverCount: number;
  isTakeaway: boolean;
  status: 'Open' | 'Closed' | 'Merged';
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

/**
 * A tender recorded against an order (PAY-01/02/05). Only `'Cash'` exists
 * today. `amountDue` is what was still owed at the moment of this payment,
 * not always the order's full total — a partial tender is valid
 * (`amountApplied` equals `amountTendered`, `remainingBalance` stays above
 * zero); an overpaying tender settles the balance and returns `change`.
 */
export interface PaymentDto {
  id: string;
  orderId: string;
  method: 'Cash';
  amountDue: MoneyDto;
  amountTendered: MoneyDto;
  amountApplied: MoneyDto;
  change: MoneyDto;
  remainingBalance: MoneyDto;
  paidAtUtc: string;
}

export interface RecordPaymentRequest {
  method: 'Cash';
  amountTendered: number;
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

/** Top of the Organization -> Site -> Terminal hierarchy (IDN-01). */
export interface OrganizationDto {
  id: string;
  name: string;
}

/** A physical restaurant location. Region is a PortugueseRegion name (IDN-01). */
export interface SiteDto {
  id: string;
  organizationId: string;
  name: string;
  region: string;
}

/** A staff member who can sign in with a PIN (IDN-08/09). Never carries the PIN or its hash. */
export interface StaffDto {
  id: string;
  siteId: string;
  name: string;
  role: 'Staff' | 'Manager';
  isLocked: boolean;
}

export interface StaffPinRequest {
  pin: string;
}

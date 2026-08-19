// Minimal mirror of src/backend/Brasa.Api/Contracts/*.cs — only the fields
// the test builders below actually touch. Not the full shape; see
// src/web/pos/src/api/types.ts for that.

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

/** A menu item's recurring day/time availability window (CAT-11) — a *prato do dia*. Null means always available. */
export interface MenuItemScheduleDto {
  daysOfWeek: string[];
  startTime: string;
  endTime: string;
}

/** A price change queued to take effect at a future instant (CAT-16). */
export interface ScheduledPriceDto {
  newPrice: MoneyDto;
  effectiveFromUtc: string;
  isActive: boolean;
}

export interface MenuItemDto {
  id: string;
  name: string;
  description: string | null;
  /** Path to an uploaded photo (CAT-02), relative to the API's own origin, not `/api/v1`. Null if none set. */
  imageUrl: string | null;
  price: MoneyDto;
  takeawayPrice: MoneyDto | null;
  vatRatePercent: number;
  isAlcoholic: boolean;
  course: string | null;
  station: string | null;
  schedule: MenuItemScheduleDto | null;
  isCouvert: boolean;
  scheduledPrice: ScheduledPriceDto | null;
  allergens: string[];
  modifierGroups: ModifierGroupDto[];
}

export interface MenuCategoryDto {
  id: string;
  name: string;
  items: MenuItemDto[];
}

/** From GET /menu/all — unlike MenuCategoryDto (always pre-filtered to visible), this carries isVisible and its items carry isAvailable. */
export interface AdminMenuItemDto extends MenuItemDto {
  isAvailable: boolean;
}

export interface AdminMenuCategoryDto {
  id: string;
  name: string;
  displayOrder: number;
  isVisible: boolean;
  items: AdminMenuItemDto[];
}

/** Top of the Organization/Site/Terminal hierarchy (IDN-01). */
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

/** A physical POS device registered at a site. No pairing/auth yet (IDN-01). */
export interface TerminalDto {
  id: string;
  siteId: string;
  label: string;
}

/** A cash session — *abertura de caixa* (PAY-08) — a starting float declaration against a terminal. */
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
  countedAmount: MoneyDto | null;
  countedByStaffId: string | null;
  countedByStaffName: string | null;
  countedAtUtc: string | null;
}

/** A pay-in or pay-out against an open cash session (PAY-09) — always requires a reason. */
export interface CashMovementDto {
  id: string;
  cashSessionId: string;
  direction: 'PayIn' | 'PayOut';
  amount: MoneyDto;
  reason: string;
  recordedByStaffId: string;
  recordedByStaffName: string;
  recordedAtUtc: string;
}

/** A staff member who can sign in with a PIN (IDN-08/09). Never carries the PIN or its hash. */
export interface StaffDto {
  id: string;
  siteId: string;
  name: string;
  role: 'Staff' | 'Manager';
  isLocked: boolean;
}

/** A per-site price list (CAT-05), with every entry it currently holds. */
export interface PriceListDto {
  id: string;
  siteId: string;
  name: string;
  entries: PriceListEntryDto[];
}

/** One item's overridden price within a price list. */
export interface PriceListEntryDto {
  id: string;
  menuItemId: string;
  price: MoneyDto;
}

/** The price an item resolves to at a site right now (CAT-05) — a list override, or the item's ordinary price. */
export interface EffectivePriceDto {
  menuItemId: string;
  price: MoneyDto;
  isOverridden: boolean;
}

/** A fixed-price bundle of menu items (CAT-10), with every component it currently holds. */
export interface ComboDto {
  id: string;
  name: string;
  price: MoneyDto;
  components: ComboComponentDto[];
}

/** One item within a combo — always exactly one unit. */
export interface ComboComponentDto {
  id: string;
  menuItemId: string;
}

export interface ImportMenuItemsRowError {
  rowNumber: number;
  message: string;
}

export interface ImportMenuItemsResponse {
  created: number;
  errors: ImportMenuItemsRowError[];
}

export interface OrderLineDto {
  id: string;
  itemName: string;
  quantity: number;
  discountType: string | null;
  discountValue: number | null;
  discountAmount: MoneyDto;
  lineTotal: MoneyDto;
  notes: string | null;
  isVoided: boolean;
  voidReason: string | null;
  /** Which course the item was served at, snapshotted at add-time (ORD-07). Null if the item had none. */
  course: string | null;
  /** Whether this line has been sent to the kitchen (ORD-07/08). */
  isFired: boolean;
  firedAtUtc: string | null;
}

export interface OrderDto {
  id: string;
  tableId: string;
  tableLabel: string;
  coverCount: number;
  isTakeaway: boolean;
  status: string;
  discountType: string | null;
  discountValue: number | null;
  discountAmount: MoneyDto;
  total: MoneyDto;
  lines: OrderLineDto[];
}

export interface OrderSummaryDto {
  id: string;
  tableId: string;
  tableLabel: string;
  coverCount: number;
  status: string;
  total: MoneyDto;
  lineCount: number;
  openedAtUtc: string;
  closedAtUtc: string | null;
}

export interface VatBreakdownDto {
  vatRateFraction: number;
  netTotal: MoneyDto;
  vatAmount: MoneyDto;
  grossTotal: MoneyDto;
}

export interface PreBillDto {
  orderId: string;
  tableLabel: string;
  coverCount: number;
  lines: { id: string; itemName: string; lineTotal: MoneyDto; isVoided: boolean }[];
  vatBreakdown: VatBreakdownDto[];
  total: MoneyDto;
  generatedAtUtc: string;
  documentKind: string;
}

export interface SplitByItemLineDto {
  lineId: string;
  itemName: string;
  quantity: number;
  total: MoneyDto;
}

export interface SplitByItemGroupDto {
  lines: SplitByItemLineDto[];
  total: MoneyDto;
}

export interface SplitByItemResponse {
  groups: SplitByItemGroupDto[];
}

export interface FiscalDocumentDto {
  documentNumber: string;
  atcud: string;
  grossTotal: MoneyDto;
}

export interface CloseOrderResponse {
  order: OrderDto;
  document: FiscalDocumentDto;
}

export interface PaymentDto {
  id: string;
  orderId: string;
  method: 'Cash' | 'Card';
  amountDue: MoneyDto;
  amountTendered: MoneyDto;
  amountApplied: MoneyDto;
  change: MoneyDto;
  remainingBalance: MoneyDto;
  tipAmount: MoneyDto;
  attributedStaffId: string | null;
  attributedStaffName: string | null;
  paidAtUtc: string;
}

export interface TableDto {
  id: string;
  roomId: string;
  label: string;
  seats: number;
  positionX: number;
  positionY: number;
  shape: 'Round' | 'Square' | 'Rectangle';
  state: 'Free' | 'Occupied' | 'BillRequested' | 'Dirty';
  /** Non-null when pushed together with others as one seating unit (FLR-05). */
  groupId: string | null;
}

/** An effective-dated VAT rate for one (alcohol band, channel, region) combination (CAT-07/08). */
export interface TaxRuleDto {
  id: string;
  isAlcoholic: boolean;
  isTakeaway: boolean;
  region: string;
  vatRatePercent: number;
  effectiveFromUtc: string;
  effectiveToUtc: string | null;
}

/** The rate resolved for one combination at one instant (CAT-08). */
export interface ResolvedTaxRuleDto {
  vatRatePercent: number;
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

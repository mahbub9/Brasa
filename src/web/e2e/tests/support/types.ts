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

export interface MenuItemDto {
  id: string;
  name: string;
  price: MoneyDto;
  modifierGroups: ModifierGroupDto[];
}

export interface MenuCategoryDto {
  id: string;
  name: string;
  items: MenuItemDto[];
}

export interface OrderLineDto {
  id: string;
  itemName: string;
  quantity: number;
  notes: string | null;
}

export interface OrderDto {
  id: string;
  tableId: string;
  tableLabel: string;
  coverCount: number;
  isTakeaway: boolean;
  status: string;
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
  lines: { itemName: string; lineTotal: MoneyDto }[];
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

export interface TableDto {
  id: string;
  label: string;
  state: 'Free' | 'Occupied' | 'BillRequested' | 'Dirty';
}

export interface RoomDto {
  id: string;
  name: string;
  tables: TableDto[];
}

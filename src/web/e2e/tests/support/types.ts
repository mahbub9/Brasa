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

export interface OrderDto {
  id: string;
  tableId: string;
  tableLabel: string;
  status: string;
  total: MoneyDto;
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

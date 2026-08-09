// Mirrors src/backend/Brasa.Api/Contracts/*.cs — only the fields this shell's
// Overview screen actually reads. See src/web/pos/src/api/types.ts for the
// fuller shape; duplicated rather than shared until a second client makes
// web/sdk (WEB-03/API-15) worth building — see that file's own comment.

export interface MenuItemDto {
  id: string;
  isAvailable: boolean;
}

export interface MenuCategoryDto {
  id: string;
  name: string;
  items: MenuItemDto[];
}

export type TableState = 'Free' | 'Occupied' | 'BillRequested' | 'Dirty';

export interface TableDto {
  id: string;
  state: TableState;
}

export interface RoomDto {
  id: string;
  name: string;
  tables: TableDto[];
}

/** RFC 9457 problem response shape used for every API failure. */
export interface ProblemDetails {
  title?: string;
  status?: number;
  detail?: string;
  code?: string;
}

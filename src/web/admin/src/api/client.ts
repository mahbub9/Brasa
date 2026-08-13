import type {
  AdminMenuCategoryDto,
  AssignRoomSectionRequest,
  CreateRoomRequest,
  CreateStaffRequest,
  CreateTableRequest,
  ImportMenuItemsResponse,
  MenuItemDto,
  OrganizationDto,
  ProblemDetails,
  RoomDto,
  SiteDto,
  StaffDto,
  StaffPinRequest,
  TableDto,
  UpdateMenuCategoryVisibilityRequest,
  UpdateMenuItemAvailabilityRequest,
  UpdateMenuItemCouvertRequest,
  UpdateMenuItemPriceRequest,
  UpdateMenuItemScheduleRequest,
  UpdateMenuItemTakeawayPriceRequest,
  UpdateRoomRequest,
  UpdateTableRequest,
} from './types';

// http://localhost:5216 is the "http" launch profile in
// src/backend/Brasa.Api/Properties/launchSettings.json.
const API_BASE_URL = import.meta.env.VITE_API_BASE_URL ?? 'http://localhost:5216/api/v1';

/** Thrown for any non-2xx response, carrying the server's stable error code. */
export class ApiError extends Error {
  readonly status: number;
  readonly code?: string;

  constructor(status: number, code: string | undefined, message: string) {
    super(message);
    this.name = 'ApiError';
    this.status = status;
    this.code = code;
  }
}

async function request<T>(path: string, init?: RequestInit): Promise<T> {
  const response = await fetch(`${API_BASE_URL}${path}`, {
    ...init,
    headers: {
      Accept: 'application/json',
      ...(init?.body ? { 'Content-Type': 'application/json' } : {}),
      ...init?.headers,
    },
  });

  if (!response.ok) {
    const problem: ProblemDetails = await response.json().catch(() => ({}));
    throw new ApiError(response.status, problem.code, problem.title ?? response.statusText);
  }

  if (response.status === 204) {
    return undefined as T;
  }

  return (await response.json()) as T;
}

/** Every mutating call gets its own key — see docs/architecture/api-contract.md (API-05). */
function newIdempotencyKey(): string {
  return crypto.randomUUID();
}

function put<T>(path: string, body: unknown): Promise<T> {
  return request<T>(path, {
    method: 'PUT',
    headers: { 'Idempotency-Key': newIdempotencyKey() },
    body: JSON.stringify(body),
  });
}

function post<T>(path: string, body?: unknown): Promise<T> {
  return request<T>(path, {
    method: 'POST',
    headers: { 'Idempotency-Key': newIdempotencyKey() },
    body: body === undefined ? undefined : JSON.stringify(body),
  });
}

function del<T>(path: string): Promise<T> {
  return request<T>(path, {
    method: 'DELETE',
    headers: { 'Idempotency-Key': newIdempotencyKey() },
  });
}

export const api = {
  getMenu: () => request<AdminMenuCategoryDto[]>('/menu/all'),

  getFloor: () => request<RoomDto[]>('/floor'),

  setItemAvailability: (itemId: string, body: UpdateMenuItemAvailabilityRequest) =>
    put<MenuItemDto>(`/menu/items/${itemId}/availability`, body),

  setItemPrice: (itemId: string, body: UpdateMenuItemPriceRequest) =>
    put<MenuItemDto>(`/menu/items/${itemId}/price`, body),

  setItemTakeawayPrice: (itemId: string, body: UpdateMenuItemTakeawayPriceRequest) =>
    put<MenuItemDto>(`/menu/items/${itemId}/takeaway-price`, body),

  setItemSchedule: (itemId: string, body: UpdateMenuItemScheduleRequest) =>
    put<MenuItemDto>(`/menu/items/${itemId}/schedule`, body),

  setItemCouvert: (itemId: string, body: UpdateMenuItemCouvertRequest) =>
    put<MenuItemDto>(`/menu/items/${itemId}/couvert`, body),

  setCategoryVisibility: (categoryId: string, body: UpdateMenuCategoryVisibilityRequest) =>
    put(`/menu/categories/${categoryId}/visibility`, body),

  deleteItem: (itemId: string) => del<void>(`/menu/items/${itemId}`),

  importMenuItems: (csv: string) => post<ImportMenuItemsResponse>('/menu/items/import', { csv }),

  createTable: (roomId: string, body: CreateTableRequest) => post<TableDto>(`/rooms/${roomId}/tables`, body),

  updateTable: (tableId: string, body: UpdateTableRequest) => put<TableDto>(`/tables/${tableId}`, body),

  deleteTable: (tableId: string) => del<void>(`/tables/${tableId}`),

  createRoom: (body: CreateRoomRequest) => post<RoomDto>('/rooms', body),

  updateRoom: (roomId: string, body: UpdateRoomRequest) => put<RoomDto>(`/rooms/${roomId}`, body),

  deleteRoom: (roomId: string) => del<void>(`/rooms/${roomId}`),

  assignRoomSection: (roomId: string, body: AssignRoomSectionRequest) => put<RoomDto>(`/rooms/${roomId}/section`, body),

  getOrganizations: () => request<OrganizationDto[]>('/organizations'),

  getSites: (organizationId: string) => request<SiteDto[]>(`/organizations/${organizationId}/sites`),

  getStaff: (siteId: string) => request<StaffDto[]>(`/sites/${siteId}/staff`),

  createStaff: (siteId: string, body: CreateStaffRequest) => post<StaffDto>(`/sites/${siteId}/staff`, body),

  setStaffPin: (staffId: string, body: StaffPinRequest) => put<StaffDto>(`/staff/${staffId}/pin`, body),
};

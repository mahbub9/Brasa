import type {
  AdminMenuCategoryDto,
  AssignRoomSectionRequest,
  CashMovementDto,
  CashSessionDto,
  CashSessionVarianceDto,
  CreateRoomRequest,
  CreateStaffRequest,
  CreateTableRequest,
  FeatureFlagDto,
  ImportMenuItemsResponse,
  MenuItemDto,
  OpenCashSessionRequest,
  OrganizationDto,
  ProblemDetails,
  RecordCashCountRequest,
  RecordCashMovementRequest,
  RoomDto,
  SetFeatureFlagRequest,
  SiteDto,
  StaffDto,
  StaffPinRequest,
  TableDto,
  TerminalDto,
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

/**
 * The API's own origin, without the `/api/v1` suffix — `MenuItemDto.imageUrl`
 * (CAT-02) is a path relative to the API host root (`/uploads/menu-items/...`,
 * served by `UseStaticFiles`), not under `/api/v1` like every JSON endpoint,
 * so a client needs this to build a fetchable `<img src>`.
 */
export const apiOrigin = new URL(API_BASE_URL).origin;

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
  // A FormData body (image upload) must NOT get an explicit Content-Type --
  // the browser sets its own multipart boundary, which a hardcoded
  // 'application/json' would silently break.
  const isFormData = init?.body instanceof FormData;
  const response = await fetch(`${API_BASE_URL}${path}`, {
    ...init,
    headers: {
      Accept: 'application/json',
      ...(init?.body && !isFormData ? { 'Content-Type': 'application/json' } : {}),
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

/** Multipart upload (CAT-02's menu item photo) -- see request()'s own FormData handling. */
function postForm<T>(path: string, formData: FormData): Promise<T> {
  return request<T>(path, {
    method: 'POST',
    headers: { 'Idempotency-Key': newIdempotencyKey() },
    body: formData,
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

  uploadItemImage: (itemId: string, file: File) => {
    const formData = new FormData();
    formData.append('file', file);
    return postForm<MenuItemDto>(`/menu/items/${itemId}/image`, formData);
  },

  removeItemImage: (itemId: string) => del<MenuItemDto>(`/menu/items/${itemId}/image`),

  importMenuItems: (csv: string) => post<ImportMenuItemsResponse>('/menu/items/import', { csv }),

  importMenuItemsExcel: (file: File) => {
    const formData = new FormData();
    formData.append('file', file);
    return postForm<ImportMenuItemsResponse>('/menu/items/import/excel', formData);
  },

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

  getFeatureFlags: () => request<FeatureFlagDto[]>('/feature-flags'),

  setFeatureFlag: (key: string, body: SetFeatureFlagRequest) => put<FeatureFlagDto>(`/feature-flags/${key}`, body),

  getTerminals: (siteId: string) => request<TerminalDto[]>(`/sites/${siteId}/terminals`),

  getCurrentCashSession: (terminalId: string) =>
    request<CashSessionDto | null>(`/terminals/${terminalId}/cash-sessions/current`),

  openCashSession: (body: OpenCashSessionRequest) => post<CashSessionDto>('/cash-sessions', body),

  closeCashSession: (cashSessionId: string) => post<CashSessionDto>(`/cash-sessions/${cashSessionId}/close`),

  getCashMovements: (cashSessionId: string) => request<CashMovementDto[]>(`/cash-sessions/${cashSessionId}/movements`),

  recordCashMovement: (cashSessionId: string, body: RecordCashMovementRequest) =>
    post<CashMovementDto>(`/cash-sessions/${cashSessionId}/movements`, body),

  recordCashCount: (cashSessionId: string, body: RecordCashCountRequest) =>
    post<CashSessionDto>(`/cash-sessions/${cashSessionId}/count`, body),

  getCashSessionVariance: (cashSessionId: string) =>
    request<CashSessionVarianceDto>(`/cash-sessions/${cashSessionId}/variance`),
};
